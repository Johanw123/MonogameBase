#!/usr/bin/env python3
"""Compares two benchmark.sh runs scene by scene (Markdown to stdout).
Usage: compare.py BEFORE_DIR AFTER_DIR
Only compare runs from the same machine and scenes; mean and p99 move by a few percent
between identical runs."""
import glob
import json
import os
import sys


def load(run):
    stats = {}
    for path in glob.glob(os.path.join(run, "*.capture.json")):
        name = os.path.basename(path)[:-len(".capture.json")]
        if name.endswith((".trace", ".allocs")):
            continue
        bench = json.load(open(path)).get("benchmark")
        if bench:
            stats[name] = bench
    return stats


def change(before, after):
    if not before:
        return ""
    return f"{100 * (after - before) / before:+.0f}%"


def main():
    before, after = load(sys.argv[1]), load(sys.argv[2])
    print(f"Before: `{sys.argv[1]}`, after: `{sys.argv[2]}`\n")
    print("| Scene | Mean ms | | p99 ms | | Max ms | Frames >16.7 ms | Alloc KB/frame | GC pause ms |")
    print("|---|---:|---:|---:|---:|---:|---:|---:|---:|")
    for name in sorted(set(before) & set(after)):
        b, a = before[name], after[name]
        print(f"| {name} | {b['frame']['mean']:.2f} → {a['frame']['mean']:.2f} | {change(b['frame']['mean'], a['frame']['mean'])} "
              f"| {b['frame']['p99']:.1f} → {a['frame']['p99']:.1f} | {change(b['frame']['p99'], a['frame']['p99'])} "
              f"| {b['frame']['max']:.0f} → {a['frame']['max']:.0f} "
              f"| {b['frames_over16']} → {a['frames_over16']} "
              f"| {b['allocated_kb_per_frame']:.0f} → {a['allocated_kb_per_frame']:.0f} "
              f"| {b['gc_pause_ms']:.0f} → {a['gc_pause_ms']:.0f} |")
    missing = sorted(set(before) ^ set(after))
    if missing:
        print(f"\nOnly in one run: {', '.join(missing)}")


if __name__ == "__main__":
    main()
