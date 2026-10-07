#!/usr/bin/env python3
"""Writes a Markdown report for a benchmark.sh run directory (to stdout).

For every scene: frame-time statistics, GC figures and the load (gems, ships) from
<scene>.capture.json. With traces (<scene>.trace.speedscope.json, benchmark.sh --trace):
main-thread time per phase and the hottest methods, plus hotspots across all scenes.

Speedscope exports from dotnet-trace mark every sample with a pseudo leaf frame:
CPU_TIME (running on a CPU) or UNMANAGED_CODE_TIME (in native code, including waits on
the GPU driver). Time is attributed to the deepest real method. Traced runs are slower
than timed ones, so phase times use the traced run's own mean frame time.
Usage: report.py RUN_DIR [TOP]"""
import glob
import json
import os
import sys
from collections import defaultdict

PSEUDO = {"CPU_TIME", "UNMANAGED_CODE_TIME"}
CAPTURE = "UntitledGemGame.Capture."
GAME_MODULES = ("[UntitledGemGame]", "[JapeFramework]")
PHASES = [
    ("Frame", "Microsoft.Xna.Framework.Game.Tick("),
    ("Update", "UntitledGemGame.GameMain.Update("),
    ("· game screen", "UntitledGemGameGameScreen.Update("),
    ("· · weapons", "UntitledGemGameGameScreen.UpdateWeapons("),
    ("· gems (UpdateSystem2)", "UpdateSystem2.Update("),
    ("· fleet (HarvesterCollectionSystem)", "HarvesterCollectionSystem.Update("),
    ("· menus (RenderGuiSystem)", "RenderGuiSystem.Update("),
    ("Draw world", "UntitledGemGameGameScreen.Draw("),
    ("· gems", "GemRenderBatch.Draw("),
    ("· ships (RenderSystem)", "RenderSystem.Draw("),
    ("· weapons and effects", "UntitledGemGameGameScreen.DrawWeapons("),
    ("Draw HUD", "GameMain.DrawHudLayer("),
    ("Draw menus (RenderGuiSystem)", "RenderGuiSystem.Draw("),
    ("Bloom", "Bloom"),
    ("Present (swap)", "PlatformPresent("),
    ("GC suspension", "PollGC"),
]


def short(name):
    module, _, method = name.rpartition("!")
    method = method.split("(", 1)[0]
    parts = method.split(".")
    label = ".".join(parts[-2:]) if len(parts) >= 2 else method
    return f"{label}  [{module}]" if module else label


def walk(profile):
    stack, last = [], profile["startValue"]
    for ev in profile["events"]:
        at = ev["at"]
        if stack and at > last:
            yield stack, at - last
        last = at
        if ev["type"] == "O":
            stack.append(ev["frame"])
        else:
            for i in range(len(stack) - 1, -1, -1):
                if stack[i] == ev["frame"]:
                    del stack[i:]
                    break


def analyse(path):
    """Main-thread phase shares and method times, as shares of the frame loop."""
    data = json.load(open(path))
    frames = [f["name"] for f in data["shared"]["frames"]]
    tick = {i for i, n in enumerate(frames) if PHASES[0][1] in n}
    profiles = [p for p in data["profiles"] if p["type"] == "evented"]
    main = max(profiles, key=lambda p: sum(1 for ev in p["events"] if ev["frame"] in tick))
    phase = defaultdict(lambda: [0.0, 0.0])
    self_t = defaultdict(lambda: [0.0, 0.0])
    incl_t = defaultdict(float)
    loop = capture = 0.0
    for stack, d in walk(main):
        names = [frames[i] for i in stack]
        state = names[-1] if names and names[-1] in PSEUDO else None
        real = [n for n in names if n not in PSEUDO]
        if not real or not any(PHASES[0][1] in n for n in real):
            continue  # outside the frame loop (startup, shutdown)
        slot = 0 if state == "CPU_TIME" else 1
        loop += d
        if any(CAPTURE in n for n in real):
            capture += d
            continue  # benchmark bookkeeping and stills, not the game
        for label, needle in PHASES:
            if any(needle in n for n in real):
                phase[label][slot] += d
        self_t[short(real[-1])][slot] += d
        for n in {short(x) for x in real}:
            incl_t[n] += d
    return {"loop": loop, "capture": capture, "phase": phase, "self": self_t, "incl": incl_t}


def fmt(v, digits=2):
    return f"{v:.{digits}f}"


def main():
    run = sys.argv[1]
    top = int(sys.argv[2]) if len(sys.argv) > 2 else 12
    scenes = sorted(p for p in glob.glob(os.path.join(run, "*.json"))
                    if not p.endswith((".capture.json", ".trace.json", ".speedscope.json")))
    out = [f"# Benchmark: {os.path.basename(os.path.normpath(run))}", ""]
    env = os.path.join(run, "environment.txt")
    if os.path.exists(env):
        out += ["```", open(env).read().strip(), "```", ""]
    out += ["CPU frame times of the Release build in capture mode (headless, unthrottled, fixed 60 Hz",
            "simulation step, world rendered at 3840x2160 with the HUD). Frame = one update + draw +",
            "present. Smoothness budget: 16.7 ms per frame for 60 fps.", ""]

    rows, traced = [], {}
    for scene in scenes:
        name = os.path.basename(scene)[:-5]
        first = open(scene).readline().strip()
        note = first[2:].strip() if first.startswith("//") else ""
        report_path = os.path.join(run, f"{name}.capture.json")
        if not os.path.exists(report_path):
            rows.append((name, note, None))
            continue
        bench = json.load(open(report_path)).get("benchmark")
        rows.append((name, note, bench))
        trace = os.path.join(run, f"{name}.trace.speedscope.json")
        trace_report = os.path.join(run, f"{name}.trace.capture.json")
        if os.path.exists(trace) and os.path.exists(trace_report):
            traced[name] = (analyse(trace), json.load(open(trace_report))["benchmark"])

    out += ["## Summary", "",
            "| Scene | Mean ms | p50 | p95 | p99 | Max | >16.7 ms | >33 ms | Update | Draw | Alloc KB/frame | GC pause ms (gen2) | Draw calls | Gems | Ships |",
            "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for name, note, b in rows:
        if b is None:
            out.append(f"| {name} | failed | | | | | | | | | | | | | |")
            continue
        f = b["frame"]
        over = f"{100 * b['frames_over16'] / max(1, b['frames']):.0f}%"
        over33 = f"{100 * b['frames_over33'] / max(1, b['frames']):.1f}%"
        out.append(f"| {name} | {fmt(f['mean'])} | {fmt(f['p50'])} | {fmt(f['p95'])} | {fmt(f['p99'])} | {fmt(f['max'], 1)} "
                   f"| {over} | {over33} | {fmt(b['update']['mean'])} | {fmt(b['draw']['mean'])} | {b['allocated_kb_per_frame']:.0f} "
                   f"| {b['gc_pause_ms']:.0f} ({b['gen2_collections']}) | {b.get('draw_calls', 0):.0f} "
                   f"| {b['active_gems']} | {b['flying_ships']} |")
    out += ["", "Scenes:", ""] + [f"- **{name}**: {note}" for name, note, _ in rows] + [""]

    # Spikes: frames slower than the scene's own p95 and than 60 fps, split by whether a
    # garbage collection ran during them.
    spikes = [(name, b) for name, _, b in rows if b and b.get("frame_ms")]
    if spikes:
        out += ["## Spikes", "",
                "Frames slower than 16.7 ms, and whether a garbage collection ran during them. "
                "Median of frames with a GC against the rest shows what a collection costs.", "",
                "| Scene | Frames >16.7 ms | ...with a GC | Median GC frame | Median other frame | Worst 5 frames (G = GC) |",
                "|---|---:|---:|---:|---:|---|"]
        for name, b in spikes:
            frames, gc = b["frame_ms"], set(b.get("gc_frames") or [])
            slow = [i for i, ms in enumerate(frames) if ms > 1000 / 60]
            gc_ms = sorted(frames[i] for i in gc) or [0]
            other = sorted(ms for i, ms in enumerate(frames) if i not in gc) or [0]
            worst = sorted(range(len(frames)), key=lambda i: -frames[i])[:5]
            worst_text = ", ".join(f"{frames[i]:.0f}{' G' if i in gc else ''}" for i in worst)
            out.append(f"| {name} | {len(slow)} | {sum(1 for i in slow if i in gc)} | {gc_ms[len(gc_ms) // 2]:.1f} "
                       f"| {other[len(other) // 2]:.1f} | {worst_text} |")
        out += [""]

    if traced:
        out += ["## Hotspots across all traced scenes", "",
                "Self time per method, averaged over the traced scenes as a share of each scene's frame time "
                "(capture bookkeeping excluded).", "",
                "| Share | Of which CPU | Method |", "|---:|---:|---|"]
        overall = defaultdict(lambda: [0.0, 0.0])
        for name, (a, _) in traced.items():
            for m, (c, n) in a["self"].items():
                overall[m][0] += c / a["loop"] / len(traced)
                overall[m][1] += n / a["loop"] / len(traced)
        for m, (c, n) in sorted(overall.items(), key=lambda kv: -sum(kv[1]))[:25]:
            out.append(f"| {100 * (c + n):.1f}% | {100 * c:.1f}% | `{m}` |")
        out += [""]

        out += ["## Per scene", ""]
        for name, note, b in rows:
            if name not in traced:
                continue
            a, tb = traced[name]
            per = a["loop"] / tb["frame"]["mean"] if tb["frame"]["mean"] else 1
            out += [f"### {name}", "", note, "",
                    f"Traced run: {tb['frame']['mean']:.2f} ms mean frame (untraced {b['frame']['mean']:.2f} ms). "
                    f"Benchmark bookkeeping: {a['capture'] / per:.2f} ms/frame (excluded below).", "",
                    "| Phase | ms/frame | CPU | Native/waiting |", "|---|---:|---:|---:|"]
            for label, _ in PHASES:
                c, n = a["phase"][label]
                if c + n > 0:
                    out.append(f"| {label} | {(c + n) / per:.2f} | {c / per:.2f} | {n / per:.2f} |")
            out += ["", f"Top {top} by self time:", "", "| ms/frame | Method |", "|---:|---|"]
            for m, (c, n) in sorted(a["self"].items(), key=lambda kv: -sum(kv[1]))[:top]:
                out.append(f"| {(c + n) / per:.2f} | `{m}` |")
            out += ["", f"Top {top} game methods by inclusive time:", "", "| ms/frame | Method |", "|---:|---|"]
            shown = 0
            for m, t in sorted(a["incl"].items(), key=lambda kv: -kv[1]):
                if not m.endswith(GAME_MODULES) or m.startswith(("Program", "BaseGame.", "GameMain.Draw", "GameMain.Update")):
                    continue
                out.append(f"| {t / per:.2f} | `{m}` |")
                shown += 1
                if shown >= top:
                    break
            out += [""]
    print("\n".join(out))


if __name__ == "__main__":
    main()
