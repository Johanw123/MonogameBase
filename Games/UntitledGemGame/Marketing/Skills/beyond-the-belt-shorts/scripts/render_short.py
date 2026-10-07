#!/usr/bin/env python3
"""Render a short from an edit file (format: references/editing.md).

  render_short.py edit.json            # final MP4 + review sheet
  render_short.py edit.json --fast     # NVENC, for quick looks
  render_short.py edit.json --resolve  # also write a Resolve plan (resolve-game-video skill)

Shots are cut back to back from capture takes (punch-in, slow push, slow motion),
text is drawn in the chosen layout ("fullscreen": over the gameplay; "banners":
the branded top/bottom bands of Shorts 01-04), music and the takes' rebuilt game
audio are mixed and normalised to -14 LUFS, and the result is checked.
Existing outputs are never overwritten; bump the version in "output".
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

import cta
from common import FONT_BOLD, LOGO, contact_sheet, duration, game_number, load_jsonc, probe, word_number

ACCENT = (115, 232, 235)
BAND = (4, 15, 25)
WINDOW = (0, 380, 1080, 1014)  # banners: gameplay window (x, y, w, h) on the 1080x1920 canvas


def font(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_BOLD, size)


# ---------------------------------------------------------------- text

def resolve_tokens(text: str, sample: dict | None) -> str:
    if '{' not in text:
        return text
    if sample is None:
        raise ValueError(f'"{text}" uses numbers but its shot has no capture log')
    values = {'gems': game_number(sample['gems']), 'gems_words': word_number(sample['gems']),
              'rate': game_number(sample['gems_per_minute']), 'rate_words': word_number(sample['gems_per_minute']),
              'earned': game_number(sample['earned_this_run']), 'on_screen': f"{int(sample['active_gems']):,}",
              'upgrades': f"{int(sample.get('upgrades', 0)):,}"}
    return text.format(**values)


def runs(line: str) -> list[tuple[str, bool]]:
    """Split *accent* markup into (text, accented) runs."""
    parts = re.split(r'(\*[^*]+\*)', line)
    return [(p[1:-1], True) if p.startswith('*') else (p, False) for p in parts if p]


def draw_line(draw: ImageDraw.ImageDraw, x_center: float, y: float, line: str, size: int, stroke: int) -> None:
    f = font(size)
    width = sum(draw.textlength(text, font=f) for text, _ in runs(line))
    x = x_center - width / 2
    for text, accent in runs(line):
        draw.text((x, y), text, font=f, fill=ACCENT if accent else (255, 255, 255), stroke_width=stroke,
                  stroke_fill=(5, 10, 18))
        x += draw.textlength(text, font=f)


def fit(lines: list[str], size: int, max_width: int) -> int:
    probe_draw = ImageDraw.Draw(Image.new('RGBA', (8, 8)))
    while size > 24:
        widest = max(sum(probe_draw.textlength(t, font=font(size)) for t, _ in runs(l)) for l in lines)
        if widest <= max_width:
            return size
        size -= 4
    return size


def fullscreen_text(spec: dict, w: int, h: int, sample: dict | None) -> Image.Image:
    """Bold outlined text straight over the gameplay, inside the Shorts safe zone."""
    lines = [resolve_tokens(l, sample) for l in spec['text'].split('\n')]
    subs = [resolve_tokens(l, sample) for l in spec.get('sub', '').split('\n') if l]
    margin = int(w * 0.09)
    size = fit(lines, int(spec.get('size', 104) * w / 1080), w - 2 * margin)
    sub_size = fit(subs, int(size * 0.6), w - 2 * margin) if subs else 0
    line_h, sub_h = int(size * 1.12), int(sub_size * 1.25)
    block = line_h * len(lines) + (int(sub_size * 0.5) + sub_h * len(subs) if subs else 0)
    position = spec.get('pos', 'top')
    # A number places the block's top at that fraction of the height (e.g. 0.27 under the intro logo).
    top = int(h * position) if isinstance(position, (int, float)) else \
        {'top': int(h * 0.14), 'center': int(h * 0.42 - block / 2), 'bottom': int(h * 0.74 - block)}[position]
    text = Image.new('RGBA', (w, h))
    draw = ImageDraw.Draw(text)
    y = top
    for line in lines:
        draw_line(draw, w / 2, y, line, size, max(3, size // 11))
        y += line_h
    y += int(sub_size * 0.5)
    for line in subs:
        draw_line(draw, w / 2, y, line, sub_size, max(2, sub_size // 10))
        y += sub_h
    # Soft shadow so text reads over bright swarms without a box behind it.
    shadow = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    shadow.putalpha(text.getchannel('A').filter(ImageFilter.GaussianBlur(size // 6)).point(lambda a: int(a * 0.7)))
    out = Image.alpha_composite(Image.new('RGBA', (w, h)), shadow)
    return Image.alpha_composite(out, text)


def fullscreen_end(spec: dict, w: int, h: int) -> Image.Image:
    card = Image.new('RGBA', (w, h), (2, 6, 12, int(255 * spec.get('dim', 0.6))))
    logo = Image.open(LOGO).convert('RGBA')
    logo = logo.crop(logo.getbbox())
    logo.thumbnail((int(w * 0.78), int(h * 0.2)), Image.Resampling.LANCZOS)
    card.alpha_composite(logo, ((w - logo.width) // 2, int(h * 0.33)))
    text = fullscreen_text({'text': spec.get('title', 'WISHLIST NOW'), 'sub': spec.get('sub', ''), 'size': spec.get('size', 96),
                            'pos': 'top'}, w, h, None)
    shifted = Image.new('RGBA', (w, h))
    shifted.alpha_composite(text, (0, int(h * 0.33) + logo.height + int(h * 0.04) - int(h * 0.14)))
    return Image.alpha_composite(card, shifted)


def flash_end(spec: dict, w: int, h: int) -> tuple[Image.Image, Image.Image]:
    """Static layer (dim, logo, sub line) and the big centred title that blinks on the beat."""
    card = Image.new('RGBA', (w, h), (2, 6, 12, int(255 * spec.get('dim', 0.5))))
    logo = Image.open(LOGO).convert('RGBA')
    logo = logo.crop(logo.getbbox())
    logo.thumbnail((int(w * 0.6), int(h * 0.12)), Image.Resampling.LANCZOS)
    card.alpha_composite(logo, ((w - logo.width) // 2, int(h * 0.17)))
    if spec.get('sub'):
        card = Image.alpha_composite(card, fullscreen_text({'text': spec['sub'], 'size': spec.get('sub_size', 60),
                                                            'pos': 0.6}, w, h, None))
    title = fullscreen_text({'text': spec.get('title', 'WISHLIST\nNOW'), 'size': spec.get('size', 170), 'pos': 'center'},
                            w, h, None)
    return card, title


def banner_card(headline: str, caption: str, closing: bool, sequence: int, total: int) -> Image.Image:
    """The band design of Shorts 01-04 (TrailerAssets/Shorts/build_shorts.py)."""
    im = Image.new('RGBA', (1080, 1920))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 1080, WINDOW[1] - 1), fill=BAND + (255,))
    d.rectangle((0, WINDOW[1] + WINDOW[3], 1080, 1920), fill=BAND + (255,))
    logo = Image.open(LOGO).convert('RGBA')
    logo = logo.crop(logo.getbbox())
    logo.thumbnail((420, 134), Image.Resampling.LANCZOS)
    im.alpha_composite(logo, (84, 72))
    d.text((84, 203), headline, font=font(62), fill=(244, 251, 255), spacing=6)
    d.rectangle((84, 355, 248, 359), fill=ACCENT)
    d.text((84, 1440), 'BEYOND THE BELT', font=font(26), fill=ACCENT)
    if closing:
        d.text((84, 1496), 'Ready to go beyond?', font=font(43), fill='white')
        d.rounded_rectangle((84, 1580, 916, 1700), radius=18, fill=(13, 51, 63), outline=ACCENT, width=3)
        d.text((500, 1639), caption, font=font(43), fill='white', anchor='mm')
    else:
        words, lines = caption.split(), ['']
        for word in words:
            candidate = (lines[-1] + ' ' + word).strip()
            lines[-1:] = [candidate] if d.textlength(candidate, font=font(44)) <= 830 else [lines[-1], word]
        d.text((84, 1496), '\n'.join(lines), font=font(44), fill='white', spacing=12)
        d.text((84, 1660), 'BUILD YOUR FLEET. GO BEYOND.', font=font(27), fill=ACCENT)
    d.rectangle((84, 1770, 916, 1773), fill=(29, 52, 64))
    d.rectangle((84, 1770, 84 + round(832 * sequence / total), 1773), fill=ACCENT)
    return im


# ---------------------------------------------------------------- edit

class Edit:
    def __init__(self, path: Path):
        self.path = path
        self.root = path.parent
        self.spec = load_jsonc(path)
        self.w, self.h = self.spec.get('size', [1080, 1920])
        self.fps = self.spec.get('fps', 60)
        self.layout = self.spec.get('layout', 'fullscreen')
        if self.layout not in ('fullscreen', 'banners'):
            raise ValueError('layout must be fullscreen or banners')
        if self.layout == 'banners' and (self.w, self.h) != (1080, 1920):
            raise ValueError('the banners layout is designed for 1080x1920')
        self.output = self.file(self.spec['output'])
        self.shots = []
        at = 0.0
        for i, shot in enumerate(self.spec['shots']):
            speed = shot.get('speed', 1.0)
            span = shot['dur'] * speed
            parts = []
            # A "split" shot stacks two takes (top and bottom halves); otherwise the shot is one part.
            for part in shot.get('split', [shot]):
                take = self.file(part['take'])
                if not take.is_file():
                    raise FileNotFoundError(f'shot {i}: {take} missing (capture it first)')
                info = next(s for s in probe(take)['streams'] if s['codec_type'] == 'video')
                report_path = take.with_suffix('.capture.json')
                report = json.loads(report_path.read_text()) if report_path.is_file() else None
                if part['in'] + span > duration(take) + 1e-3:
                    raise ValueError(f'shot {i}: {take.name} is only {duration(take):.2f}s, needs {part["in"] + span:.2f}s')
                parts.append(dict(part, take=take, report=report, tw=int(info['width']), th=int(info['height']),
                                  dur=shot['dur'], speed=speed, span=span))
            self.shots.append(dict(shot, parts=parts, at=at, span=span, speed=speed, take=parts[0]['take'],
                                   report=parts[0]['report'], tw=parts[0]['tw'], th=parts[0]['th'],
                                   **{'in': parts[0]['in']}))
            at += shot['dur']
        self.total = at

    def file(self, name: str) -> Path:
        path = Path(name).expanduser()
        return path if path.is_absolute() else (self.root / path).resolve()

    def sample_at(self, t: float, source: int = 0) -> dict | None:
        shot = next((s for s in self.shots if s['at'] <= t < s['at'] + s['dur']), self.shots[-1])
        part = shot['parts'][min(source, len(shot['parts']) - 1)]
        if not part['report'] or not part['report']['samples']:
            return None
        source_t = part['in'] + (t - shot['at']) * shot['speed']
        samples = part['report']['samples']
        # Interpolate between the 0.25 s samples so live counters tick smoothly.
        after = next((i for i, s in enumerate(samples) if s['t'] >= source_t), len(samples) - 1)
        if after == 0 or samples[after]['t'] < source_t:
            return samples[after]
        a, b = samples[after - 1], samples[after]
        f = (source_t - a['t']) / max(1e-6, b['t'] - a['t'])
        # Only numbers interpolate; other fields (damage_by_source) come from the earlier sample.
        return {k: source_t if k == 't'
                else a[k] + (b[k] - a[k]) * f if isinstance(a[k], (int, float)) and isinstance(b.get(k), (int, float))
                else a[k] for k in a}


def frame_filter(shot: dict, w: int, h: int, fps: int) -> str:
    """Crop the take to the frame's aspect around the focus point, then zoom (optionally pushing in)."""
    tw, th = shot['tw'], shot['th']
    fx, fy = shot.get('focus', [0.5, 0.5])
    aspect = w / h
    cw, ch = (tw, int(tw / aspect)) if tw / th < aspect else (int(th * aspect), th)
    cw, ch = cw // 2 * 2, ch // 2 * 2
    cx = min(max(int(fx * tw - cw / 2), 0), tw - cw)
    cy = min(max(int(fy * th - ch / 2), 0), th - ch)
    z0, z1 = shot.get('zoom', 1.0), shot.get('zoom_end', shot.get('zoom', 1.0))
    frames = max(1, round(shot['dur'] * fps) - 1)
    # Focus inside the cropped area, so zooming moves toward the subject.
    px = (fx * tw - cx) / cw
    py = (fy * th - cy) / ch
    zoom = f"{z0}+({z1 - z0})*(on/{frames})*(on/{frames})*(3-2*on/{frames})" if z1 != z0 else str(z0)
    head = f"setpts=(PTS-STARTPTS)/{shot['speed']},fps={fps},"
    if 'focus_end' not in shot:
        return (f"{head}crop={cw}:{ch}:{cx}:{cy},"
                f"zoompan=z='{zoom}':x='max(0,min(iw-iw/zoom,{px}*iw-iw/zoom/2))':"
                f"y='max(0,min(ih-ih/zoom,{py}*ih-ih/zoom/2))':d=1:s={w}x{h}:fps={fps},setsar=1")
    # Pan: the frame-shaped window glides from focus to focus_end (eased), zooming around the moving point.
    gx, gy = shot['focus_end']
    def eased(n: str) -> str:
        x = f"min({n}/{frames},1)"
        return f"({x}*{x}*(3-2*{x}))"
    def point(n: str) -> tuple[str, str]:
        return f"({fx}+({gx - fx})*{eased(n)})", f"({fy}+({gy - fy})*{eased(n)})"
    def window(n: str) -> tuple[str, str]:
        x, y = point(n)
        return f"min(max({x}*{tw}-{cw / 2},0),{tw - cw})", f"min(max({y}*{th}-{ch / 2},0),{th - ch})"
    crop_x, crop_y = window('n')
    zx, zy = point('on')
    wx, wy = window('on')
    px_t = f"(({zx}*{tw}-({wx}))/{cw})"
    py_t = f"(({zy}*{th}-({wy}))/{ch})"
    return (f"{head}crop={cw}:{ch}:x='{crop_x}':y='{crop_y}',"
            f"zoompan=z='{zoom}':x='max(0,min(iw-iw/zoom,{px_t}*iw-iw/zoom/2))':"
            f"y='max(0,min(ih-ih/zoom,{py_t}*ih-ih/zoom/2))':d=1:s={w}x{h}:fps={fps},setsar=1")


def live_text(edit: Edit, text: dict, folder: Path) -> dict:
    """A counter that follows the capture log: one frame per step, encoded as an alpha clip."""
    folder.mkdir()
    step = text.get('step', 0.1)
    frames = max(1, round(text['dur'] / step))
    for k in range(frames):
        fullscreen_text(text, edit.w, edit.h, edit.sample_at(min(text['at'] + k * step, edit.total - 1e-3),
                                                             text.get('source', 0))).save(
            folder / f'{k:04d}.png')
    clip = folder.with_suffix('.mov')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-framerate', f'{1 / step:g}', '-i', str(folder / '%04d.png'),
                    '-c:v', 'png', '-pix_fmt', 'rgba', str(clip)], check=True)
    return dict(file=clip, at=text['at'], dur=text['dur'], fade=text.get('fade', 0.12), video=True)


def build_overlays(edit: Edit, tmp: Path) -> list[dict]:
    """PNG overlays: [{file, at, dur, fade}]."""
    overlays = []
    texts = edit.spec.get('texts', [])
    end = edit.spec.get('end_card')
    if edit.layout == 'fullscreen':
        for i, text in enumerate(texts):
            if text.get('live'):
                overlays.append(live_text(edit, text, tmp / f'live_{i:02d}'))
                continue
            png = tmp / f'text_{i:02d}.png'
            fullscreen_text(text, edit.w, edit.h, edit.sample_at(text['at'], text.get('source', 0))).save(png)
            overlays.append(dict(file=png, at=text['at'], dur=text['dur'], fade=text.get('fade', 0.12)))
        if end and end.get('style') in cta.STYLES:
            dur = end.get('dur', edit.total - end['at'])
            clip = cta.render(end, edit.w, edit.h, edit.fps, dur, tmp / 'cta')
            overlays.append(dict(file=clip, at=end['at'], dur=dur, fade=0, video=True))
        elif end and end.get('style') == 'flash':
            static, title = flash_end(end, edit.w, edit.h)
            dur = end.get('dur', edit.total - end['at'])
            static.save(tmp / 'end.png')
            title.save(tmp / 'end_title.png')
            overlays.append(dict(file=tmp / 'end.png', at=end['at'], dur=dur, fade=0.15))
            overlays.append(dict(file=tmp / 'end_title.png', at=end['at'], dur=dur, fade=0,
                                 blink=(end.get('period', 0.8333), end.get('duty', 0.65))))
        elif end:
            png = tmp / 'end.png'
            fullscreen_end(end, edit.w, edit.h).save(png)
            overlays.append(dict(file=png, at=end['at'], dur=end.get('dur', edit.total - end['at']), fade=0.2))
    else:
        cards = [(t['at'], t['dur'], resolve_tokens(t['text'], edit.sample_at(t['at'])),
                  resolve_tokens(t.get('sub', ''), edit.sample_at(t['at'])), False) for t in texts]
        if end:
            cards.append((end['at'], end.get('dur', edit.total - end['at']), end.get('title', 'HOW FAR\nWILL YOU GO?'),
                          end.get('sub', 'WISHLIST ON STEAM'), True))
        covered = sorted(cards)
        for i, (at, dur, headline, caption, closing) in enumerate(covered):
            png = tmp / f'band_{i:02d}.png'
            banner_card(headline, caption, closing, i + 1, len(covered)).save(png)
            overlays.append(dict(file=png, at=at, dur=dur, fade=0))
        gaps = [t for t in range(int(edit.total * 10)) if not any(a <= t / 10 < a + d for a, d, *_ in covered)]
        if gaps:
            raise ValueError(f'banners layout: texts must cover the whole edit (uncovered from {gaps[0] / 10:.1f}s)')
    return overlays


def render_audio(edit: Edit, tmp: Path) -> Path:
    """Music + game audio, normalised to about -14 LUFS with peaks under -1.5 dBFS."""
    inputs, graph, mix = [], [], []
    music = edit.spec.get('music')
    if music:
        inputs += ['-ss', str(music.get('in', 0)), '-t', f'{edit.total:.3f}', '-i', str(edit.file(music['file']))]
        fade_out = music.get('fade_out', 1.0)
        graph.append(f"[0:a]aresample=48000,aformat=channel_layouts=stereo,volume={music.get('gain_db', 0)}dB,"
                     f"afade=t=in:d={music.get('fade_in', 0) or 0.001},"
                     f"afade=t=out:st={edit.total - fade_out:.3f}:d={fade_out},apad=whole_dur={edit.total:.3f}[music]")
        mix.append('[music]')
    game = edit.spec.get('game_audio', {'gain_db': 6})
    if game:
        labels = []
        for k, shot in enumerate(edit.shots):
            tempo = f",atempo={shot['speed']}" if shot['speed'] != 1 else ''
            streams = []
            for j, part in enumerate(shot['parts']):
                wav = part['take'].with_suffix('.sfx.wav')
                if not wav.is_file():
                    raise FileNotFoundError(f'{wav} missing (capture.py writes it)')
                index = sum(1 for a in inputs if a == '-i')
                inputs += ['-ss', f"{part['in']:.3f}", '-t', f"{shot['span']:.3f}", '-i', str(wav)]
                graph.append(f"[{index}:a]aresample=48000,aformat=channel_layouts=stereo{tempo},"
                             f"apad=whole_dur={shot['dur']:.3f},atrim=0:{shot['dur']:.3f},asetpts=PTS-STARTPTS[g{k}_{j}]")
                streams.append(f'[g{k}_{j}]')
            if len(streams) == 1:
                graph.append(f"{streams[0]}anull[g{k}]")
            else:
                graph.append(f"{''.join(streams)}amix=inputs={len(streams)}:normalize=0[g{k}]")
            labels.append(f'[g{k}]')
        graph.append(f"{''.join(labels)}concat=n={len(labels)}:v=0:a=1,volume={game.get('gain_db', 6)}dB[game]")
        mix.append('[game]')
    if not mix:
        return None
    graph.append(f"{''.join(mix)}amix=inputs={len(mix)}:normalize=0:duration=longest,atrim=0:{edit.total:.3f}[mix]")
    raw = tmp / 'mix.wav'
    subprocess.run(['ffmpeg', '-v', 'error', '-y', *inputs, '-filter_complex', ';'.join(graph), '-map', '[mix]',
                    '-c:a', 'pcm_f32le', str(raw)], check=True)
    # Measured gain to -14 LUFS, then a true-peak limiter (loudnorm misbehaves on 20 s clips).
    gain = -14.0 - integrated_loudness(raw)
    final = tmp / 'audio.wav'
    for _ in range(3):  # the limiter can pull a peaky mix below target; correct and measure again
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(raw), '-af',
                        f'volume={gain:.2f}dB,alimiter=limit=0.84:level=false,aresample=48000', '-c:a', 'pcm_s16le',
                        str(final)], check=True)
        error = -14.0 - integrated_loudness(final)
        if abs(error) < 0.4:
            break
        gain += error
    return final


def integrated_loudness(path: Path) -> float:
    out = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', str(path), '-af', 'ebur128', '-f', 'null', '-'],
                         capture_output=True, text=True).stderr
    return float(re.findall(r'I:\s+(-?[\d.]+) LUFS', out)[-1])


def render(edit: Edit, fast: bool, tmp: Path) -> None:
    w, h = (WINDOW[2], WINDOW[3]) if edit.layout == 'banners' else (edit.w, edit.h)
    inputs, graph = [], []
    for i, shot in enumerate(edit.shots):
        pad = f",pad={edit.w}:{edit.h}:{WINDOW[0]}:{WINDOW[1]}:color=0x040f19" if edit.layout == 'banners' else ''
        tail = f"trim=duration={shot['dur']:.4f},setpts=PTS-STARTPTS"
        # Dips to black at the shot's edges (a title transition), in seconds of the timeline.
        if shot.get('fade_in'):
            tail += f",fade=t=in:st=0:d={shot['fade_in']}"
        if shot.get('fade_out'):
            tail += f",fade=t=out:st={shot['dur'] - shot['fade_out']:.4f}:d={shot['fade_out']}"
        tail += ",format=yuv420p"
        if len(shot['parts']) == 1:
            index = sum(1 for a in inputs if a == '-i')
            # One decoder thread per take: a long edit opens every 4K take at once, and the default
            # threads per decoder ran a 39-shot trailer out of memory.
            inputs += ['-threads', '1', '-ss', f"{shot['in']:.3f}", '-t', f"{shot['span']:.3f}", '-i', str(shot['take'])]
            graph.append(f"[{index}:v]{frame_filter(shot, w, h, edit.fps)}{pad},{tail}[v{i}]")
        else:
            halves = []
            for k, part in enumerate(shot['parts']):
                index = sum(1 for a in inputs if a == '-i')
                inputs += ['-threads', '1', '-ss', f"{part['in']:.3f}", '-t', f"{shot['span']:.3f}", '-i', str(part['take'])]
                graph.append(f"[{index}:v]{frame_filter(part, w, h // 2, edit.fps)},{tail}[h{i}_{k}]")
                halves.append(f'[h{i}_{k}]')
            graph.append(f"{''.join(halves)}vstack=inputs=2,drawbox=x=0:y={h // 2 - 3}:w={w}:h=6:"
                         f"color=0x73E8EB@0.9:t=fill,{tail}[v{i}]")
    graph.append(f"{''.join(f'[v{i}]' for i in range(len(edit.shots)))}concat=n={len(edit.shots)}:v=1:a=0[base0]")
    overlays = build_overlays(edit, tmp)
    last = 'base0'
    for k, o in enumerate(overlays):
        index = sum(1 for a in inputs if a == '-i')
        if o.get('video'):
            inputs += ['-i', str(o['file'])]
        else:
            inputs += ['-loop', '1', '-framerate', str(edit.fps), '-t', f"{o['dur']:.3f}", '-i', str(o['file'])]
        fades = (f",fade=t=in:st=0:d={o['fade']}:alpha=1,fade=t=out:st={o['dur'] - o['fade']:.3f}:d={o['fade']}:alpha=1"
                 if o['fade'] else '')
        graph.append(f"[{index}:v]format=rgba,fps={edit.fps}{fades},setpts=PTS-STARTPTS+{o['at']:.3f}/TB[o{k}]")
        blink = (f":enable='lt(mod(t-{o['at']:.3f},{o['blink'][0]}),{o['blink'][0] * o['blink'][1]:.4f})'"
                 if o.get('blink') else '')
        graph.append(f"[{last}][o{k}]overlay=0:0:eof_action=pass:format=auto{blink}[base{k + 1}]")
        last = f'base{k + 1}'
    audio = render_audio(edit, tmp)
    if audio:
        inputs += ['-i', str(audio)]
    codec = (['-c:v', 'h264_nvenc', '-preset', 'p6', '-tune', 'hq', '-rc', 'vbr', '-cq', '19', '-b:v', '0']
             if fast else ['-c:v', 'libx264', '-preset', 'slow', '-crf', '17'])
    command = ['ffmpeg', '-v', 'error', '-stats', '-y', *inputs, '-filter_complex', ';'.join(graph), '-map', f'[{last}]',
               *codec, '-pix_fmt', 'yuv420p', '-r', str(edit.fps), '-t', f'{edit.total:.3f}']
    if audio:
        audio_index = sum(1 for a in inputs if a == '-i') - 1
        command += ['-map', f'{audio_index}:a', '-c:a', 'aac', '-b:a', '192k', '-ar', '48000']
    command += ['-movflags', '+faststart', str(edit.output)]
    edit.output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(command, check=True)


def check(edit: Edit) -> list[str]:
    info = probe(edit.output)
    video = next(s for s in info['streams'] if s['codec_type'] == 'video')
    audio = [s for s in info['streams'] if s['codec_type'] == 'audio']
    problems = []
    if (int(video['width']), int(video['height'])) != (edit.w, edit.h):
        problems.append(f"size {video['width']}x{video['height']}")
    if abs(float(info['format']['duration']) - edit.total) > 0.1:
        problems.append(f"duration {float(info['format']['duration']):.2f}s, expected {edit.total:.2f}s")
    if edit.spec.get('music') or edit.spec.get('game_audio', True):
        if not audio:
            problems.append('no audio stream')
    return problems


def review_sheet(edit: Edit) -> Path:
    times, labels = [], []
    for i, shot in enumerate(edit.shots):
        times.append(shot['at'] + 0.2)
        labels.append(f"{shot['at'] + 0.2:.1f}s shot {i + 1}")
    for text in edit.spec.get('texts', []):
        times.append(text['at'] + min(0.5, text['dur'] / 2))
        labels.append(f"{times[-1]:.1f}s text")
    if edit.spec.get('end_card'):
        times.append(edit.total - 0.3)
        labels.append(f'{edit.total - 0.3:.1f}s end')
    order = sorted(range(len(times)), key=times.__getitem__)
    return contact_sheet(edit.output, edit.output.with_suffix('.sheet.jpg'), [times[i] for i in order],
                         [labels[i] for i in order], tile_height=640, columns=6)


def resolve_plan(edit: Edit, tmp: Path, audio: Path | None) -> Path:
    """Plan for resolve-game-video's resolve_script.py (fullscreen layout): takes on V1, text on V2."""
    folder = edit.output.with_suffix('.resolve')
    folder.mkdir(exist_ok=True)
    overlays = []
    for k, o in enumerate(build_overlays(edit, tmp)):
        mov = folder / f'overlay_{k:02d}.mov'
        source = ['-i', str(o['file'])] if o.get('video') else ['-loop', '1', '-framerate', str(edit.fps), '-i', str(o['file'])]
        subprocess.run(['ffmpeg', '-v', 'error', '-y', *source, '-r', str(edit.fps),
                        '-frames:v', str(round(o['dur'] * edit.fps)), '-c:v', 'prores_ks', '-profile:v', '4',
                        '-pix_fmt', 'yuva444p10le', str(mov)], check=True)
        overlays.append({'file': str(mov), 'in_s': 0, 'dur_s': o['dur'], 'at_s': o['at'], 'track': 2})
    shots = []
    for shot in edit.shots:
        if shot.get('focus', [0.5, 0.5]) != [0.5, 0.5] or shot.get('zoom_end') or shot['speed'] != 1:
            print(f"note: shot at {shot['at']:.1f}s uses focus/push-in/speed; set those by hand in Resolve", file=sys.stderr)
        shots.append({'file': str(shot['take']), 'in_s': shot['in'], 'out_s': shot['in'] + shot['span'], 'at_s': shot['at'],
                      'audio': False, 'track': 1, 'zoom': shot.get('zoom', 1.0)})
    plan = {'timeline': edit.output.stem, 'fps': edit.fps, 'width': edit.w, 'height': edit.h,
            'input_scaling': 'scaleToCrop', 'bin': edit.output.stem, 'shots': shots, 'overlays': overlays}
    if audio:
        mixed = folder / 'audio_mix.wav'
        shutil.copy(audio, mixed)
        plan['music'] = {'file': str(mixed), 'in_s': 0, 'out_s': edit.total, 'at_s': 0, 'track': 1}
    path = folder / 'plan.json'
    path.write_text(json.dumps(plan, indent=2))
    return path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('edit', type=Path)
    parser.add_argument('--fast', action='store_true', help='NVENC encode for quick reviews')
    parser.add_argument('--resolve', action='store_true', help='also write a Resolve plan (fullscreen layout)')
    args = parser.parse_args()
    edit = Edit(args.edit.resolve())
    if edit.output.exists():
        sys.exit(f'{edit.output} exists; bump the version in "output"')
    with tempfile.TemporaryDirectory(prefix='btb-render-') as tmp:
        render(edit, args.fast, Path(tmp))
        if args.resolve:
            audio = Path(tmp) / 'audio.wav'
            print(f'resolve plan: {resolve_plan(edit, Path(tmp), audio if audio.is_file() else None)}')
    problems = check(edit)
    sheet = review_sheet(edit)
    print(f'{edit.output}: {edit.total:.2f}s {edit.w}x{edit.h} {edit.fps} fps, {len(edit.shots)} shots, layout {edit.layout}')
    print(f'review sheet: {sheet}')
    if problems:
        sys.exit('PROBLEMS: ' + '; '.join(problems))


if __name__ == '__main__':
    main()
