#!/usr/bin/env bash
# Benchmarks the game's CPU frame time over the scenes in tools/benchmark/scenes:
# progression stages, gem and fleet loads, menus and big events. Each scene runs the
# Release build through capture mode with "benchmark": true, so no video is recorded;
# the game runs unthrottled and <scene>.capture.json gets frame timings and GC figures.
#
#   ./benchmark.sh                      time every scene
#   ./benchmark.sh --trace              also profile each scene with dotnet-trace
#   ./benchmark.sh --allocs             also record what allocates and every GC (tools/benchmark/allocs)
#   ./benchmark.sh --only 03-late,20-menu-upgrades
#   ./benchmark.sh --out benchmarks/before-gems
#
# Results go to benchmarks/<timestamp>/ (or --out): the scenes, their reports and logs,
# speedscope traces with --trace (open with `speedscope file`), <scene>.allocs.txt with
# --allocs, and report.md (tools/benchmark/report.py). Traced runs are separate from the
# timed ones, since tracing slows the game down. Compare runs on the same machine, build
# and scenes.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

trace=false
allocs=false
only=""
out="benchmarks/$(date +%Y%m%d_%H%M%S)"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --trace) trace=true ;;
        --only) only="$2"; shift ;;
        --out) out="$2"; shift ;;
        --allocs) allocs=true ;;
        -h|--help) sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown argument: $1 (see --help)" >&2; exit 1 ;;
    esac
    shift
done

dotnet_trace="$(command -v dotnet-trace || echo "$HOME/.dotnet/tools/dotnet-trace")"
if { $trace || $allocs; } && [[ ! -x "$dotnet_trace" ]]; then
    echo "dotnet-trace not found; install it with: dotnet tool install --global dotnet-trace" >&2
    exit 1
fi

echo "=== Building Release ==="
if ! build_log=$(dotnet build UntitledGemGame.csproj -c Release 2>&1); then
    echo "$build_log" | grep -E "error" >&2
    exit 1
fi
game="bin/Release/net10.0/UntitledGemGame.dll"
mkdir -p "$out"
out="$(cd "$out" && pwd)"

{
    echo "date: $(date -Iseconds)"
    echo "commit: $(git rev-parse --short HEAD 2>/dev/null)$(git diff --quiet 2>/dev/null || echo ' (uncommitted changes)')"
    echo "cpu: $(grep -m1 'model name' /proc/cpuinfo | cut -d: -f2 | xargs) ($(nproc) threads)"
    echo "gpu: $(lspci 2>/dev/null | grep -iE 'vga|3d' | head -n1 | cut -d: -f3 | xargs)"
    echo "dotnet: $(dotnet --version)"
} > "$out/environment.txt"

# Runs one scene; retries the occasional startup abort (ImGui display size).
run_scene() {
    local scene="$1" log="$2" attempt
    for attempt in 1 2 3; do
        if dotnet "$game" --capture "$scene" --overwrite > "$log" 2>&1; then
            return 0
        fi
        find /tmp -maxdepth 1 -name 'btb-capture-*' -type d -empty -delete 2>/dev/null || true
        echo "    attempt $attempt failed (see $log)"
    done
    return 1
}

# Runs one scene with dotnet-trace attached from its first recorded frame to its exit.
# Extra arguments go to dotnet-trace (allocation runs pick the GC events).
trace_scene() {
    local scene="$1" log="$2" nettrace="$3" pid
    shift 3
    dotnet "$game" --capture "$scene" --overwrite > "$log" 2>&1 &
    pid=$!
    until grep -q "CAPTURE RECORDING" "$log" 2>/dev/null; do
        if ! kill -0 "$pid" 2>/dev/null; then
            wait "$pid" || true
            return 1
        fi
        sleep 0.2
    done
    "$dotnet_trace" collect --process-id "$pid" --output "$nettrace" "$@" > "${nettrace%.nettrace}.dotnet-trace.log" 2>&1 || true
    wait "$pid"
}

failed=()
for source in tools/benchmark/scenes/*.json; do
    name="$(basename "$source" .json)"
    if [[ -n "$only" && ",$only," != *",$name,"* ]]; then continue; fi
    echo "=== $name ==="
    cp "$source" "$out/$name.json"
    if run_scene "$out/$name.json" "$out/$name.log"; then
        grep -h "BENCHMARK:" "$out/$name.log" | sed 's/^/    /'
    else
        failed+=("$name")
        continue
    fi
    if $trace; then
        sed "s/\"output\": *\"$name.mp4\"/\"output\": \"$name.trace.mp4\"/" "$source" > "$out/$name.trace.json"
        attempt=1
        until trace_scene "$out/$name.trace.json" "$out/$name.trace.log" "$out/$name.nettrace"; do
            find /tmp -maxdepth 1 -name 'btb-capture-*' -type d -empty -delete 2>/dev/null || true
            if (( ++attempt > 3 )); then failed+=("$name (trace)"); break; fi
        done
        if [[ -s "$out/$name.nettrace" ]]; then
            # convert names its output after the input, without the extension.
            "$dotnet_trace" convert "$out/$name.nettrace" --format Speedscope -o "$out/$name" > /dev/null
            mv "$out/$name.speedscope.json" "$out/$name.trace.speedscope.json"
            rm -f "$out/$name.nettrace"
            echo "    traced: $name.trace.speedscope.json"
        fi
    fi
    if $allocs; then
        sed "s/\"output\": *\"$name.mp4\"/\"output\": \"$name.allocs.mp4\"/" "$source" > "$out/$name.allocs.json"
        attempt=1
        # GC keyword at verbose level: allocation samples with stacks, collections and pauses.
        until trace_scene "$out/$name.allocs.json" "$out/$name.allocs.log" "$out/$name.allocs.nettrace" \
            --providers "Microsoft-Windows-DotNETRuntime:0x1:5"; do
            find /tmp -maxdepth 1 -name 'btb-capture-*' -type d -empty -delete 2>/dev/null || true
            if (( ++attempt > 3 )); then failed+=("$name (allocs)"); break; fi
        done
        if [[ -s "$out/$name.allocs.nettrace" ]]; then
            if dotnet run --project tools/benchmark/allocs -c Release -- "$out/$name.allocs.nettrace" > "$out/$name.allocs.txt" 2>&1; then
                rm -f "$out/$name.allocs.nettrace" "$out/$name.allocs.nettrace.etlx"
                echo "    allocations: $name.allocs.txt ($(head -n1 "$out/$name.allocs.txt" | cut -d: -f2 | xargs))"
            else
                echo "    allocation analysis failed, kept $name.allocs.nettrace (see $name.allocs.txt)"
            fi
        fi
    fi
done

python3 tools/benchmark/report.py "$out" > "$out/report.md"
echo "=== Report: $out/report.md ==="
if (( ${#failed[@]} > 0 )); then
    echo "Failed scenes: ${failed[*]}" >&2
    exit 1
fi
