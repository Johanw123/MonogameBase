#!/usr/bin/env python3
"""Record a human playthrough: video with only the game's audio, plus timings and a sound log.

  tools/record_run.py test                new run "test": build, play, report
  tools/record_run.py --resume RUN_DIR    play another session of a run (after a break)
  tools/record_run.py --report RUN_DIR    write the reports again

The game runs with --record-run RUN_DIR (Capture/RunRecording.cs): its own save in RUN_DIR
(a fresh start that never touches your progress), no music, no Steam, and RUN_DIR/events.jsonl
with every milestone and every sound. gpu-screen-recorder records the monitor the game opens on
(found through niri; the game is fullscreen by default), mouse cursor included, with only the
game's audio, so a browser playing in the background stays out. --source window records just the
game window instead, through niri's dynamic cast target and the screen-share portal: the first
time, a dialog asks what to share; pick "Dynamic Cast Target" (not a window: that choice is
saved and later runs would record it). Other gpu-screen-recorder processes, such as
gsr-ui's instant replay, share the GPU's video encoder and starve the recording (two 4K replays
left it about one new frame a second), so gsr-ui's replay is paused for the session and started
again afterwards (--keep-replay to not). Quit the game normally to finish; the recording stops
and the reports are written:

  timings.md                 playtime to each milestone, the runs, every prestige point
  summary.json               the same numbers for scripts
  timeline.csv               every milestone and purchase, with playtime and video time
  session-01.mov             the recording: HEVC video, PCM audio (imports in Resolve on Linux)
  session-01.game-audio.wav  the game audio from the recording
  session-01.sounds.csv      every sound effect: video time, file, volume, pitch, pan
  session-01.sfx.wav         the sound effects rebuilt from the log, nothing else in it
  session-01.markers.edl     milestones as Resolve timeline markers, for the clip at 01:00:00:00
  autoplay-profile.json      each run's purchase order and the talent order, plus how you played
                             (shopping rhythm, clicks, belt, extraction timing), for autoplay's
                             "profile" setting (Capture/Autoplay.cs): a stand-in that plays like you

Each run is played on one build: a new run builds Release and copies it into RUN_DIR/build,
so other work rebuilding bin/ cannot change it, and --resume plays the same build.
Playtime counts time on the gameplay screen while the game is not paused, summed over sessions.
"""
from __future__ import annotations

import argparse
import csv
import importlib.util
import json
import shutil
import signal
import subprocess
import sys
import time
import wave
from datetime import datetime
from pathlib import Path

GAME_ROOT = Path(__file__).resolve().parents[1]
SFX_SCRIPT = GAME_ROOT / 'Marketing/Skills/beyond-the-belt-shorts/scripts/sfx.py'
DEFAULT_ROOT = Path.home() / 'Videos' / 'btb-runs'
APP_AUDIO = 'UntitledGemGame'  # the game's PipeWire stream name (the executable's name)
PORTAL_TOKEN = Path.home() / '.config' / 'gpu-screen-recorder' / 'btb-record-run-token'
RATE = 48000

# First occurrence of these is a milestone in timings.md: (key, label, test).
MILESTONES = [
    ('first_upgrade', 'First upgrade', lambda e: e['type'] == 'buy'),
    ('first_power_cell', 'First power cell', lambda e: e['type'] == 'power_cell'),
    ('first_system_talent', 'First ship system talent', lambda e: e['type'] == 'system'),
    ('first_point', 'First prestige point', lambda e: e['type'] == 'point'),
    ('first_prestige', 'First prestige (extraction starts)', lambda e: e['type'] == 'prestige_sequence'),
    ('first_talent', 'First prestige talent', lambda e: e['type'] == 'talent'),
    ('second_run', 'Second run starts', lambda e: e['type'] == 'run_start'),
    ('first_signal_scan', 'First signal scan', lambda e: e['type'] == 'signal_scan'),
    ('first_fracture', 'First core fracture', lambda e: e['type'] == 'fracture'),
    ('shell_broken', 'Planet shell broken', lambda e: e['type'] == 'shell_broken'),
    ('demo_end', 'Demo ending', lambda e: e['type'] == 'demo_end'),
    ('escape', 'Warp escape', lambda e: e['type'] == 'escape'),
]

NOT_MILESTONES = {'sound', 'click', 'belt', 'command', 'sample'}

# Resolve marker colours for the EDL; other event types get no marker.
MARKER_COLOURS = {'point': 'Yellow', 'prestige_sequence': 'Red', 'run_start': 'Green', 'talent_screen': 'Purple',
                  'fracture': 'Cyan', 'shell_broken': 'Cyan', 'demo_end': 'Pink', 'warp_sequence': 'Pink'}


# ---------------------------------------------------------------- formatting

def hms(seconds: float) -> str:
    seconds = max(0.0, seconds)
    h, rest = divmod(int(seconds), 3600)
    m, s = divmod(rest, 60)
    return f'{h}:{m:02}:{s:02}'


def video_clock(seconds: float | None) -> str:
    if seconds is None:
        return ''
    sign = '-' if seconds < 0 else ''
    seconds = abs(seconds)
    h, rest = divmod(seconds, 3600)
    m, s = divmod(rest, 60)
    return f'{sign}{int(h):02}:{int(m):02}:{s:06.3f}'


def timecode(seconds: float, fps: int, hour: int = 1) -> str:
    frames = int(round(seconds * fps)) + hour * 3600 * fps
    f = frames % fps
    s = frames // fps
    return f'{s // 3600:02}:{s // 60 % 60:02}:{s % 60:02}:{f:02}'


def describe(event: dict) -> str:
    kind = event['type']
    if kind in ('buy', 'talent', 'system'):
        return f"{event.get('name') or event.get('id')} {event.get('level', '')}".strip()
    if kind == 'refund':
        return f"Refund {event.get('name') or event.get('id')} (level {event.get('level')} left)"
    if kind == 'point':
        return f"Prestige point {event.get('count')}"
    if kind == 'prestige_sequence':
        n = event.get('count')
        return f"Extraction starts ({n} point{'' if n == 1 else 's'} this run)"
    if kind == 'warp_sequence':
        return 'Warp escape starts'
    if kind == 'run_start':
        return f"Run {event.get('count')} starts"
    if kind == 'talent_screen':
        return 'Talent screen'
    if kind == 'extract':
        return f"Extraction {event.get('count')} complete"
    if kind == 'power_cell':
        return f"Power cell {event.get('count')}"
    if kind == 'fracture':
        return f"Core fracture {event.get('count')}"
    if kind == 'signal_scan':
        return f"Signal scan {event.get('count')}"
    return {'shell_broken': 'Planet shell broken', 'demo_end': 'Demo ending', 'escape': 'Warp escape',
            'session': 'Session starts', 'gameplay': 'Gameplay starts', 'quit': 'Quit'}.get(kind, kind)


# ---------------------------------------------------------------- helpers

def run_quiet(cmd: list[str], **kwargs) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, capture_output=True, text=True, **kwargs)


def niri(*args: str):
    if not shutil.which('niri'):
        return None
    try:
        out = run_quiet(['niri', 'msg', '--json', *args], timeout=5)
        return json.loads(out.stdout) if out.returncode == 0 else None
    except (subprocess.TimeoutExpired, json.JSONDecodeError):
        return None


def probe_duration(path: Path) -> float | None:
    out = run_quiet(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', str(path)])
    try:
        return float(out.stdout.strip())
    except ValueError:
        return None


def git_info() -> dict:
    head = run_quiet(['git', 'rev-parse', 'HEAD'], cwd=GAME_ROOT).stdout.strip()
    dirty = [l for l in run_quiet(['git', 'status', '--porcelain', '--', '.'], cwd=GAME_ROOT).stdout.splitlines() if l]
    return {'commit': head, 'uncommitted_files': len(dirty)}


def load_events(run: Path) -> list[dict]:
    path = run / 'events.jsonl'
    events = []
    if not path.exists():
        return events
    for line in path.read_text(errors='replace').splitlines():
        try:
            events.append(json.loads(line))
        except json.JSONDecodeError:
            pass  # a line cut short when the game was killed
    return events


def split_sessions(events: list[dict]) -> list[dict]:
    sessions = []
    for event in events:
        if event['type'] == 'session':
            sessions.append({'number': event['session'], 'start': event, 'events': []})
        elif sessions:
            sessions[-1]['events'].append(event)
    return sessions


# ---------------------------------------------------------------- recording

def build_snapshot(run: Path) -> None:
    print('Building Release...')
    out = run_quiet(['dotnet', 'build', 'UntitledGemGame.csproj', '-c', 'Release'], cwd=GAME_ROOT)
    if out.returncode != 0:
        print('\n'.join(l for l in out.stdout.splitlines() if ' error ' in l) or out.stdout[-3000:], file=sys.stderr)
        sys.exit('Build failed.')
    copy_build(run)
    diff = run_quiet(['git', 'diff', 'HEAD', '--', '.'], cwd=GAME_ROOT).stdout
    if diff:
        (run / 'build' / 'uncommitted.diff').write_text(diff)


def copy_build(run: Path) -> None:
    source = GAME_ROOT / 'bin/Release/net10.0'
    if not (source / 'UntitledGemGame').exists():
        sys.exit(f'No Release build in {source}.')
    # The top-level linux-x64/ is left over from RID-specific builds; runtimes/linux-x64 is needed.
    shutil.copytree(source, run / 'build', symlinks=True,
                    ignore=lambda folder, names: ['linux-x64'] if Path(folder) == source else [])


def other_recorders() -> list[str]:
    """Running gpu-screen-recorder processes (gsr-ui's instant replay, say). They share the GPU's
    video encoder: two 4K replays left a recording about one new frame a second."""
    out = run_quiet(['pgrep', '-a', '-f', '^gpu-screen-recorder ']).stdout
    return [line for line in out.splitlines() if line.strip()]


def pause_replay() -> bool:
    """Stop gsr-ui's instant replay for the session; True when it was running (resume_replay starts it
    again). Exits, with the replay started again, when another recorder keeps running: gsr-ui-cli only
    reaches one gsr-ui, so a second gsr-ui instance or a recorder started by hand stays."""
    if not other_recorders():
        return False
    if shutil.which('gsr-ui-cli'):
        run_quiet(['gsr-ui-cli', 'stop-replay'], timeout=10)
        deadline = time.time() + 5
        while other_recorders() and time.time() < deadline:
            time.sleep(0.25)
    if other_recorders():
        resume_replay()
        sys.exit('Could not stop this screen recording, which would starve the run\'s recording:\n  '
                 + '\n  '.join(line[:140] for line in other_recorders())
                 + '\nStop it (a second gsr-ui running?) and start again.')
    print('Paused gsr-ui\'s instant replay for this session.')
    return True


def resume_replay() -> None:
    if shutil.which('gsr-ui-cli'):
        run_quiet(['gsr-ui-cli', 'start-replay'], timeout=10)


def game_window(game: subprocess.Popen, timeout: float = 120) -> dict | None:
    """The game's niri window, once it opens (None without niri)."""
    deadline = time.time() + timeout
    while time.time() < deadline and game.poll() is None:
        windows = niri('windows')
        if windows is None:
            return None
        window = next((w for w in windows if w.get('pid') == game.pid), None)
        if window:
            return window
        time.sleep(0.5)
    return None


def monitor_of(window: dict | None) -> str:
    """The output a niri window is on, else the first monitor gpu-screen-recorder lists."""
    if window:
        workspace = next((w for w in niri('workspaces') or [] if w['id'] == window.get('workspace_id')), None)
        if workspace and workspace.get('output'):
            return workspace['output']
    monitors = run_quiet(['gpu-screen-recorder', '--list-monitors']).stdout.split()
    if not monitors:
        sys.exit('No monitor to record; pass --source with a monitor name (gpu-screen-recorder --list-monitors).')
    return monitors[0].split('|')[0]


def cast_window(window: dict) -> None:
    run_quiet(['niri', 'msg', 'action', 'set-dynamic-cast-window', '--id', str(window['id'])], timeout=5)


def check_fullscreen(window: dict | None, output: str) -> None:
    outputs = niri('outputs') or {}
    logical = (outputs.get(output) or {}).get('logical') or {}
    size = (window or {}).get('layout', {}).get('window_size')
    if size and logical and (size[0] < logical.get('width', 0) or size[1] < logical.get('height', 0)):
        print(f'  note: the game window is {size[0]}x{size[1]}, smaller than {output}; the video shows the whole '
              f'monitor. Use fullscreen (the game default) for clean footage.')


def start_recorder(args, stem: Path, game: subprocess.Popen) -> subprocess.Popen | None:
    """Start gpu-screen-recorder and wait for its first frame. Sources: "window" (niri's dynamic cast
    target, pointed at the game's window, through the screen-share portal), "portal" (whatever is picked
    in the screen-share dialog), "monitor" (the one the game window is on) or a monitor name. Monitors
    are captured through KMS, with no dialog."""
    video = stem.with_suffix('.mkv')
    window = game_window(game)
    if game.poll() is not None:
        return None
    source = args.source
    if source == 'window' and window is None:
        print('  note: could not find the game window through niri; pick it in the screen-share dialog instead.')
        source = 'portal'
    if source == 'monitor':
        source = monitor_of(window)
    if source in ('window', 'portal'):
        if source == 'window':
            cast_window(window)
        target = ['-w', 'portal', '-restore-portal-session', 'yes', '-portal-session-token-filepath', str(PORTAL_TOKEN)]
        if not PORTAL_TOKEN.exists():
            pick = '"Dynamic Cast Target"' if source == 'window' else 'the game window or its monitor'
            print(f'A screen-share dialog will open: pick {pick}. It is remembered for later sessions.')
    else:
        target = ['-w', source]
    cmd = ['gpu-screen-recorder', *target, '-c', 'mkv', '-k', args.codec, '-f', str(args.fps), '-fm', 'cfr',
           '-q', args.quality, '-a', f'app:{APP_AUDIO}', '-ac', 'opus', '-ab', '256', '-cursor', 'yes',
           '-write-first-frame-ts', 'yes', '-o', str(video)]
    recorder = subprocess.Popen(cmd, stdout=stem.with_suffix('.recorder.log').open('w'), stderr=subprocess.STDOUT,
                                start_new_session=True)
    stamps = (Path(str(video) + '.ts'), video.with_suffix('.ts'))
    deadline = time.time() + 300
    while not any(p.exists() for p in stamps):
        if recorder.poll() is not None or game.poll() is not None or time.time() > deadline:
            print(f'  WARNING: the recording did not start (see {stem.with_suffix(".recorder.log")}); '
                  f'the run is still logged.', file=sys.stderr)
            if recorder.poll() is None:
                recorder.send_signal(signal.SIGINT)
            return recorder
        time.sleep(0.2)
    if source == 'window':
        cast_window(window)  # in case the cast began on another target
    elif source != 'portal':
        check_fullscreen(window, source)
    what = 'the game window' if source == 'window' else 'the picked screen' if source == 'portal' else f'monitor {source}'
    print(f'Recording {what} at {args.fps} fps with only the game\'s audio. Quit the game to finish.')
    stem.with_suffix('.video.json').write_text(json.dumps(
        {'source': source, 'fps': args.fps, 'codec': args.codec, 'quality': args.quality}, indent=2))
    return recorder


def check_encoder(args) -> None:
    if args.no_video or not args.keep_replay or not other_recorders():
        return
    sys.exit('Another screen recording is running:\n  ' + '\n  '.join(line[:140] for line in other_recorders())
             + '\nIt shares the GPU\'s video encoder and starves this recording. Stop it, or leave out --keep-replay '
               'to pause gsr-ui\'s replay for the session.')


def record_session(run: Path, args, replay_paused: bool) -> None:
    """Play one session. replay_paused: gsr-ui's replay was paused for it, start it again after."""
    session = sum(1 for e in load_events(run) if e['type'] == 'session') + 1
    stem = run / f'session-{session:02}'
    try:
        if not (run / 'build' / 'UntitledGemGame').exists():
            sys.exit(f'{run / "build"} has no game; this run has no build to play.')
        run_info = json.loads((run / 'run.json').read_text())
        run_info.setdefault('sessions', []).append({'session': session, **git_info(),
                                                    'started': datetime.now().isoformat(timespec='seconds')})
        (run / 'run.json').write_text(json.dumps(run_info, indent=2))
        print(f'Session {session} of "{run_info["name"]}" in {run}')
        play_session(run, args, stem)
    finally:
        if replay_paused:
            resume_replay()
            print('Started gsr-ui\'s instant replay again.')
    finish_video(stem, args.keep_mkv)
    report(run)


def play_session(run: Path, args, stem: Path) -> None:
    exe = run / 'build' / 'UntitledGemGame'
    game_log = stem.with_suffix('.game.log').open('w')
    game = subprocess.Popen([str(exe), '--record-run', str(run)], cwd=GAME_ROOT, stdout=game_log,
                            stderr=subprocess.STDOUT, start_new_session=True)
    recorder = None
    try:
        if not args.no_video:
            recorder = start_recorder(args, stem, game)
        else:
            print('Logging without video. Quit the game to finish.')
        try:
            game.wait(args.stop_after)
        except subprocess.TimeoutExpired:
            print(f'Stopping the game after {args.stop_after:g} s (--stop-after)...')
            stop_game(game)
    except KeyboardInterrupt:
        print('\nStopping the game...')
    finally:
        if game.poll() is None:
            stop_game(game)
        if recorder and recorder.poll() is None:
            time.sleep(0.5)  # the last sound's tail
            recorder.send_signal(signal.SIGINT)
            try:
                recorder.wait(180)
            except subprocess.TimeoutExpired:
                recorder.kill()
        game_log.close()
    if game.returncode not in (0, None):
        print(f'  note: the game exited with code {game.returncode} (log: {stem.with_suffix(".game.log")})')


def stop_game(game: subprocess.Popen) -> None:
    game.terminate()
    try:
        game.wait(30)
    except subprocess.TimeoutExpired:
        game.kill()
        game.wait()


def finish_video(stem: Path, keep_mkv: bool) -> None:
    """Keep the first-frame stamp, make the Resolve-friendly .mov and the audio WAV."""
    mkv, mov = stem.with_suffix('.mkv'), stem.with_suffix('.mov')
    info_path = stem.with_suffix('.video.json')
    for stamp in (Path(str(mkv) + '.ts'), mkv.with_suffix('.ts')):
        if stamp.exists() and info_path.exists():
            # "monotonic_microsec realtime_microsec" header, then the values.
            values = stamp.read_text().split()[-2:]
            info = json.loads(info_path.read_text())
            info['first_frame_us'], info['first_frame_realtime_us'] = int(values[0]), int(values[1])
            info_path.write_text(json.dumps(info, indent=2))
            stamp.unlink()
    if not mkv.exists():
        return
    print('Converting the recording for Resolve (video copied, audio to PCM)...')
    convert = run_quiet(['ffmpeg', '-y', '-v', 'error', '-i', str(mkv), '-map', '0:v:0', '-map', '0:a?', '-c:v', 'copy',
                         '-c:a', 'pcm_s16le', '-ar', str(RATE), str(mov)])
    audio = run_quiet(['ffmpeg', '-y', '-v', 'error', '-i', str(mkv), '-map', '0:a:0', '-c:a', 'pcm_s16le',
                       '-ar', str(RATE), str(stem.with_suffix('.game-audio.wav'))])
    a, b = probe_duration(mkv), probe_duration(mov)
    if convert.returncode == 0 and a and b and abs(a - b) < 0.5:
        if not keep_mkv:
            mkv.unlink()
    else:
        print(f'  note: could not convert {mkv.name} ({convert.stderr.strip()[:300]}); kept it as is.', file=sys.stderr)
    if audio.returncode != 0:
        print(f'  note: no game audio in the recording ({audio.stderr.strip()[:200]})', file=sys.stderr)


# ---------------------------------------------------------------- reports

def load_sfx_module():
    spec = importlib.util.spec_from_file_location('btb_sfx', SFX_SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def render_sfx(sounds: list[tuple[float, dict]], duration: float, out: Path) -> None:
    """Mix the logged sounds at their video times, a minute at a time (a full run would not fit in memory
    at once). Same processing as the shorts' sfx.py, so stems from both sound alike."""
    import numpy as np
    sfx = load_sfx_module()
    block, margin = 60 * RATE, 30 * RATE
    total = int(duration * RATE)
    buffer = np.zeros((block + margin, 2), dtype=np.float32)
    index, missing = 0, set()
    with wave.open(str(out), 'wb') as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        for start in range(0, total, block):
            while index < len(sounds) and int(round(sounds[index][0] * RATE)) < start + block:
                t, event = sounds[index]
                index += 1
                try:
                    sound = sfx.load(event['file'])
                except FileNotFoundError:
                    missing.add(event['file'])
                    continue
                raw = max(0.0, float(event.get('volume') or 1.0))
                volume = min(1.0, (raw ** 0.45) * 1.05) if raw > 0 else 0.0
                sound = sfx.pitched(sound, event.get('pitch') or 0.0)[:margin] * volume
                angle = (event.get('pan', 0.0) + 1) * np.pi / 4
                sound = sound * np.array([np.cos(angle), np.sin(angle)], dtype=np.float32) * np.sqrt(2)
                offset = int(round(t * RATE)) - start
                if offset < 0:
                    sound, offset = sound[-offset:], 0
                buffer[offset:offset + len(sound)] += sound[:len(buffer) - offset]
            count = min(block, total - start)
            chunk = np.tanh(buffer[:count] * 1.15) * 0.92
            wav.writeframes((np.clip(chunk, -1, 1) * 32767).astype('<i2').tobytes())
            buffer = np.concatenate([buffer[block:], np.zeros((block, 2), dtype=np.float32)])
    for asset in sorted(missing):
        print(f'  warning: sound {asset} not found, left out of the SFX stem', file=sys.stderr)


def sync_offset(recorded: Path, rebuilt: Path, seconds: float = 300) -> tuple[float, float] | None:
    """How much later the recorded game audio is than the log says, in seconds, and how sure (0-1):
    cross-correlates the onsets of the recording and of the SFX rebuilt from the log."""
    import numpy as np

    def onsets(path: Path):
        with wave.open(str(path)) as wav:
            frames = wav.readframes(int(min(wav.getnframes(), seconds * wav.getframerate())))
            channels, rate = wav.getnchannels(), wav.getframerate()
        audio = np.frombuffer(frames, dtype='<i2').astype(np.float32).reshape(-1, channels).mean(axis=1)
        step = rate // 1000  # 1 ms envelope
        envelope = np.abs(audio[:len(audio) // step * step]).reshape(-1, step).mean(axis=1)
        rise = np.clip(np.diff(envelope, prepend=envelope[:1]), 0, None)
        return rise - rise.mean()

    if not recorded.exists() or not rebuilt.exists():
        return None
    a, b = onsets(recorded), onsets(rebuilt)
    n = min(len(a), len(b))
    if n < 2000 or not a[:n].any() or not b[:n].any():
        return None
    a, b = a[:n], b[:n]
    size = 1 << (2 * n - 1).bit_length()
    corr = np.fft.irfft(np.fft.rfft(a, size) * np.conj(np.fft.rfft(b, size)), size)
    limit = 2000  # search +-2 s
    lags = np.concatenate([corr[:limit + 1], corr[-limit:]])
    shifts = np.concatenate([np.arange(limit + 1), np.arange(-limit, 0)])
    best = int(np.argmax(lags))
    confidence = float(lags[best] / (np.sqrt((a * a).sum() * (b * b).sum()) + 1e-9))
    return shifts[best] / 1000.0, confidence


def report(run: Path) -> None:
    events = load_events(run)
    if not events:
        sys.exit(f'No events in {run / "events.jsonl"}.')
    run_info = json.loads((run / 'run.json').read_text()) if (run / 'run.json').exists() else {'name': run.name}
    sessions = split_sessions(events)

    # Video time of every event, per session.
    for session in sessions:
        stem = run / f'session-{session["number"]:02}'
        info_path = stem.with_suffix('.video.json')
        info = json.loads(info_path.read_text()) if info_path.exists() else {}
        session['stem'], session['video'] = stem, info
        first = info.get('first_frame_us')
        for event in [session['start'], *session['events']]:
            event['session'] = session['number']
            event['video_t'] = (event['us'] - first) / 1e6 if first else None
        last = session['events'][-1] if session['events'] else session['start']
        session['wall'] = (last['us'] - session['start']['us']) / 1e6

    # The timeline leaves out the sounds, the input and the economy samples.
    milestones_events = [e for s in sessions for e in s['events'] if e['type'] not in NOT_MILESTONES]
    playtime = max(e['play'] for e in events)
    wall = sum(s['wall'] for s in sessions)

    def row(event):
        return {'playtime': round(event['play'], 3), 'session': event['session'],
                'video_time': None if event['video_t'] is None else round(event['video_t'], 3)}

    milestones = {}
    for key, label, test in MILESTONES:
        first = next((e for e in milestones_events if test(e)), None)
        if first:
            milestones[key] = {'label': label, 'detail': describe(first), **row(first)}

    # Runs: from gameplay start (run 1) or each run_start to the next extraction sequence.
    runs, current = [], {'run': 1, 'start': 0.0, 'points': 0, 'purchases': 0}
    for event in milestones_events:
        if event['type'] == 'point':
            current['points'] += 1
        elif event['type'] in ('buy', 'system', 'power_cell'):
            current['purchases'] += 1
        elif event['type'] == 'prestige_sequence':
            current['extracted'] = event['play']
        elif event['type'] == 'run_start':
            runs.append(current)
            current = {'run': int(event.get('count') or len(runs) + 1), 'start': event['play'], 'points': 0,
                       'purchases': 0}
    runs.append(current)
    points = [{'point': e.get('count'), **row(e)} for e in milestones_events if e['type'] == 'point']

    summary = {'name': run_info.get('name'), 'build': run_info.get('build'), 'sessions': len(sessions),
               'playtime': round(playtime, 3), 'wall_clock': round(wall, 3), 'milestones': milestones,
               'runs': runs, 'points': points,
               'purchases': {k: sum(1 for e in milestones_events if e['type'] == k)
                             for k in ('buy', 'talent', 'system', 'power_cell', 'signal_scan')}}
    (run / 'summary.json').write_text(json.dumps(summary, indent=2))

    with (run / 'timeline.csv').open('w', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['playtime', 'playtime_hms', 'session', 'video_time', 'type', 'what', 'id', 'level', 'cost',
                         'currency', 'count'])
        for e in milestones_events:
            writer.writerow([round(e['play'], 3), hms(e['play']), e['session'], video_clock(e['video_t']), e['type'],
                             describe(e), e.get('id', ''), e.get('level', ''), e.get('cost', ''), e.get('currency', ''),
                             e.get('count', '')])

    build = run_info.get('build') or {}
    lines = [f'# Run "{run_info.get("name")}"', '',
             f'Started {run_info.get("created", "?")} · build `{(build.get("commit") or "?")[:10]}`'
             + (f' with {build["uncommitted_files"]} uncommitted files' if build.get('uncommitted_files') else '')
             + f' · {len(sessions)} session{"s" if len(sessions) != 1 else ""}', '',
             f'**Playtime {hms(playtime)}** (gameplay screen, unpaused) · wall clock {hms(wall)}', '',
             '## Milestones', '', '| Milestone | Playtime | What | Session | Video time |', '|---|---|---|---|---|']
    for key, label, _ in MILESTONES:
        m = milestones.get(key)
        lines.append(f'| {label} | {hms(m["playtime"]) if m else "—"} | {m["detail"] if m else ""} | '
                     f'{m["session"] if m else ""} | {video_clock(m["video_time"]) if m else ""} |')
    lines += ['', '## Runs', '', '| Run | Starts | Extraction starts | Length | Points | Purchases |', '|---|---|---|---|---|---|']
    for r in runs:
        end = r.get('extracted')
        lines.append(f'| {r["run"]} | {hms(r["start"])} | {hms(end) if end is not None else "(still going)"} | '
                     f'{hms((end if end is not None else playtime) - r["start"])} | {r["points"]} | {r["purchases"]} |')
    if points:
        lines += ['', '## Prestige points', '', '| Point | Playtime | Since previous |', '|---|---|---|']
        previous = 0.0
        for p in points:
            lines.append(f'| {p["point"]} | {hms(p["playtime"])} | {hms(p["playtime"] - previous)} |')
            previous = p['playtime']
    counts = summary['purchases']
    lines += ['', f'Purchases: {counts["buy"]} upgrades, {counts["power_cell"]} power cells, {counts["system"]} system '
              f'talents, {counts["talent"]} prestige talents, {counts["signal_scan"]} signal scans. Every one is in '
              f'timeline.csv.']
    style = play_style(events)
    write_profile(run, run_info.get('name') or run.name, events, style)
    lines += style_lines(style) + ['']
    for session in sessions:
        write_session_files(session)
    for session in sessions:
        sounds = [e for e in session['events'] if e['type'] == 'sound']
        text = f'Session {session["number"]}: {hms(session["wall"])} wall clock, {len(sounds)} sounds'
        stem = session['stem']
        if session['video'].get('first_frame_us') is not None:
            text += f', video {stem.name}.mov'
            sync = sync_offset(stem.with_suffix('.game-audio.wav'), stem.with_suffix('.sfx.wav'))
            if sync:
                offset, confidence = sync
                text += (f'. Sync check: the recorded audio is {offset * 1000:+.0f} ms from the log'
                         f' (match {confidence:.2f}; audio output latency is normal, a large offset with a good'
                         f' match means the video times are off by that much)')
        else:
            text += ', no video'
        lines.append(text)
    (run / 'timings.md').write_text('\n'.join(lines) + '\n')
    print(f'\nReport: {run / "timings.md"}')
    print('\n'.join(lines[4:6]))


def median(values: list[float]) -> float | None:
    values = sorted(values)
    if not values:
        return None
    middle = len(values) // 2
    return values[middle] if len(values) % 2 else (values[middle - 1] + values[middle]) / 2


def play_style(events: list[dict]) -> dict:
    """How the player played, in the terms of autoplay's settings (Capture/Autoplay.cs).

    Logs from before input logging have no click, belt or command events; those are read from
    the sounds instead (a planet shot or a gem click per click, the belt's placing sound)."""
    marks = [e for e in events if e['type'] != 'sound']
    playtime = max((e['play'] for e in events), default=0)
    purchases = [e['play'] for e in marks if e['type'] in ('buy', 'system', 'power_cell')]
    visits: list[list[float]] = []
    for t in purchases:  # purchases less than 8 s apart are one visit to the terminal
        if visits and t - visits[-1][-1] < 8:
            visits[-1].append(t)
        else:
            visits.append([t])
    style: dict = {'shop_every': median([b[0] - a[-1] for a, b in zip(visits, visits[1:])]),
                   'purchases_per_visit': median([len(v) for v in visits])}

    sounds = [e for e in events if e['type'] == 'sound']
    if any(e['type'] in ('click', 'belt', 'command', 'sample') for e in marks):
        clicked = [e for e in marks if e['type'] == 'click']
        clicks = [e['play'] for e in clicked]
        presses = [e['play'] for e in clicked if not e.get('held')]
        belts = [e['play'] for e in marks if e['type'] == 'belt']
        commands = sum(1 for e in marks if e['type'] == 'command')
        style['source'] = 'input log'
    else:
        shots = sorted(e['play'] for e in sounds if 'CannonFireManual' in e['file']
                       or e['file'].endswith('Menu/Abstract1.wav') and abs(e['pitch']) < 0.2)
        clicks = []
        for t in shots:  # one gem click sound per gem: a click is the first within 60 ms
            if not clicks or t - clicks[-1] > 0.06:
                clicks.append(t)
        presses = None
        belts = [e['play'] for e in sounds if e['file'].endswith('servo-rise.wav') and e['pitch'] > 0.1]
        commands = sum(1 for e in sounds if e['file'].endswith('command.wav'))
        style['source'] = 'sounds (no input log)'
    style['clicks_per_second'] = round(len(clicks) / playtime, 2) if playtime else None
    if presses is not None:
        style['presses_per_second'] = round(len(presses) / playtime, 2) if playtime else None
    style['belt_placements'] = len(belts)
    style['belt_every'] = median([b - a for a, b in zip(belts, belts[1:]) if b - a < 120])
    style['commands'] = commands

    # Extraction timing: how long after a run's last point the player extracted, and how
    # long the talent screen took before the next run.
    stalls, screens = [], []
    last_point, talent_screen = None, None
    for e in marks:
        if e['type'] == 'point':
            last_point = e['play']
        elif e['type'] == 'prestige_sequence':
            if last_point is not None:
                stalls.append(e['play'] - last_point)
            last_point = None
        elif e['type'] == 'talent_screen':
            talent_screen = e['play']
        elif e['type'] == 'run_start' and talent_screen is not None:
            screens.append(e['play'] - talent_screen)
            talent_screen = None
    style['extract_after_last_point'] = median(stalls)
    style['talent_screen_seconds'] = median(screens)
    return {k: round(v, 2) if isinstance(v, float) else v for k, v in style.items()}


def write_profile(run: Path, name: str, events: list[dict], style: dict) -> None:
    """autoplay-profile.json: the purchase order of each run and the talents in the order
    learned, for autoplay's "profile" setting (Capture/Autoplay.cs)."""
    runs: list[list[str]] = [[]]
    talents: list[str] = []
    for e in events:
        if e['type'] == 'run_start':
            runs.append([])
        elif e['type'] in ('buy', 'system'):
            runs[-1].append(f'{e["id"]}:{e["level"]}')
        elif e['type'] == 'power_cell':
            runs[-1].append(f'power_cell:{e["count"]}')
        elif e['type'] == 'talent' and e['id'] not in talents:
            talents.append(e['id'])
    runs = [r for r in runs if r]
    profile = {'source': name, 'runs': runs, 'talents': talents, 'style': style}
    (run / 'autoplay-profile.json').write_text(json.dumps(profile, indent=1) + '\n')


def style_lines(style: dict) -> list[str]:
    def num(key, unit='', digits=1):
        value = style.get(key)
        return '—' if value is None else f'{value:.{digits}f}{unit}'
    lines = ['', '## How you played', '',
             f'From the {style["source"]}. autoplay-profile.json holds every run\'s purchase order and the talent '
             f'order for autoplay (scene `"autoplay": {{"profile": ...}}`).', '',
             f'- Shopping: a terminal visit every {num("shop_every", " s")} (median), '
             f'{num("purchases_per_visit", "", 0)} purchases a visit',
             f'- Clicks: {num("clicks_per_second", "/s", 2)} on the field, held repeats included'
             + (f' ({num("presses_per_second", "/s", 2)} presses)' if 'presses_per_second' in style else ''),
             f'- Tractor Belt: {style["belt_placements"]} intakes placed'
             + (f', one every {num("belt_every", " s", 0)} (median)' if style.get('belt_every') else ''),
             f'- Fleet commands: {style["commands"]}',
             f'- Extraction: {num("extract_after_last_point", " s", 0)} after the run\'s last point (median); '
             f'talent screen {num("talent_screen_seconds", " s", 0)}']
    return lines


def write_session_files(session: dict) -> None:
    stem, info = session['stem'], session['video']
    has_video = info.get('first_frame_us') is not None
    origin = session['start']['us']
    sounds = []
    for e in session['events']:
        if e['type'] != 'sound':
            continue
        t = e['video_t'] if has_video else (e['us'] - origin) / 1e6
        if t >= 0:
            sounds.append((t, e))
    fps = int(info.get('fps', 60))
    with stem.with_suffix('.sounds.csv').open('w', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['video_time' if has_video else 'session_time', 'timecode', 'playtime', 'file', 'volume',
                         'pitch', 'pan'])
        for t, e in sounds:
            writer.writerow([round(t, 4), timecode(t, fps), round(e['play'], 3), e['file'], e['volume'], e['pitch'],
                             e['pan']])

    duration = probe_duration(stem.with_suffix('.mov')) or probe_duration(stem.with_suffix('.mkv'))
    if duration is None:
        last = session['events'][-1]['us'] if session['events'] else origin
        duration = (last - (info.get('first_frame_us') or origin)) / 1e6 + 2
    if sounds and duration > 0:
        render_sfx(sounds, duration, stem.with_suffix('.sfx.wav'))

    if has_video:
        lines = [f'TITLE: {stem.name}', 'FCM: NON-DROP FRAME', '']
        markers = [e for e in session['events'] if e['type'] in MARKER_COLOURS and e['video_t'] is not None
                   and 0 <= e['video_t'] <= duration]
        for i, e in enumerate(markers, 1):
            tc_in, tc_out = timecode(e['video_t'], fps), timecode(e['video_t'] + 1 / fps, fps)
            lines += [f'{i:03}  001      V     C        {tc_in} {tc_out} {tc_in} {tc_out}  ',
                      f' |C:ResolveColor{MARKER_COLOURS[e["type"]]} |M:{describe(e)} |D:1', '']
        stem.with_suffix('.markers.edl').write_text('\n'.join(lines))


# ---------------------------------------------------------------- main

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('name', nargs='?', help='name of a new run (folder <root>/<date>_<name>)')
    parser.add_argument('--resume', type=Path, metavar='RUN_DIR', help='play another session of this run')
    parser.add_argument('--report', type=Path, metavar='RUN_DIR', help='only write the reports of this run')
    parser.add_argument('--root', type=Path, default=DEFAULT_ROOT, help=f'where new runs go (default {DEFAULT_ROOT})')
    parser.add_argument('--no-video', action='store_true', help='log timings and sounds without recording')
    parser.add_argument('--source', default='monitor',
                        help='what to record: "monitor" (default: the one the game opens on), a monitor name such as '
                             'DP-4, "window" (only the game window, via niri\'s dynamic cast target and the '
                             'screen-share dialog) or "portal" (whatever is picked in that dialog)')
    parser.add_argument('--fps', type=int, default=60)
    parser.add_argument('--codec', default='hevc', help='gpu-screen-recorder codec (default hevc)')
    parser.add_argument('--quality', default='ultra', help='medium, high, very_high or ultra (default ultra)')
    parser.add_argument('--keep-replay', action='store_true',
                        help="leave gsr-ui's instant replay running (by default it is paused while recording, since it "
                             "shares the GPU's video encoder)")
    parser.add_argument('--keep-mkv', action='store_true', help='keep the original recording next to the .mov')
    parser.add_argument('--no-build', action='store_true', help='use the current bin/Release build as it is')
    parser.add_argument('--stop-after', type=float, metavar='SECONDS', help='stop the game after this long (test runs)')
    args = parser.parse_args()

    if args.report:
        report(args.report.expanduser().resolve())
        return
    if args.resume:
        run = args.resume.expanduser().resolve()
        if not (run / 'run.json').exists():
            sys.exit(f'{run} is not a recorded run (no run.json).')
    elif not args.name:
        parser.error('give the new run a name, or --resume / --report a run folder')
    if not args.no_video and not shutil.which('gpu-screen-recorder'):
        sys.exit('gpu-screen-recorder is not installed (or use --no-video).')
    check_encoder(args)
    replay_paused = not args.no_video and not args.keep_replay and pause_replay()
    try:
        if not args.resume:
            run = new_run(args)
    except BaseException:
        if replay_paused:
            resume_replay()
        raise
    record_session(run, args, replay_paused)


def new_run(args) -> Path:
    run = args.root.expanduser() / f'{datetime.now():%Y-%m-%d_%H%M}_{args.name}'
    if run.exists():
        sys.exit(f'{run} exists already.')
    run.mkdir(parents=True)
    if args.no_build:
        copy_build(run)
    else:
        build_snapshot(run)
    (run / 'run.json').write_text(json.dumps({'name': args.name, 'created': datetime.now().isoformat(timespec='seconds'),
                                              'build': git_info()}, indent=2))
    return run

if __name__ == '__main__':
    main()
