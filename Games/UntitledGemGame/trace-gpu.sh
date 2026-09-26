#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

if [[ "${1:-}" == "--help" ]]; then
    cat <<'HELP'
Usage: ./trace-gpu.sh [--replay path/to/capture.trace]

Requires apitrace and a working OpenGL display/driver.
Uses EGL on both Wayland and X11 (forces SDL to use EGL on X11).
GPU_TRACE_API=gl explicitly selects X11/GLX instead.
With no arguments: builds Release, captures a new game session, then replays it
with GPU and CPU draw-call timing. Close the game normally to end capture.
Keep captures short: OpenGL traces can become large. The game uses normal saves.
--replay: profiles an existing capture without launching the game or rebuilding.

Output: traces/gpu_TIMESTAMP.trace and .profile.txt (timings are nanoseconds).
These are replay timings, not simultaneous timings of the live CPU trace.
The .trace can also be opened in qapitrace for interactive inspection.
Documentation: https://github.com/apitrace/apitrace/blob/master/docs/USAGE.markdown#profiling-a-trace
HELP
    exit 0
fi
if ! command -v apitrace >/dev/null 2>&1; then
    echo "apitrace is required; install your distribution's apitrace package." >&2
    exit 1
fi
if [[ $# == 2 && "$1" == "--replay" ]]; then
    capture="$2"
    [[ -s "$capture" ]] || { echo "Capture missing or empty: $capture" >&2; exit 1; }
elif [[ $# == 0 ]]; then
    # Session variables do not reliably identify SDL's GL context backend.
    # Match the backend to the tracer instead of guessing from the session.
    api="${GPU_TRACE_API:-egl}"
    case "$api" in
        egl) capture_environment=(SDL_VIDEO_X11_FORCE_EGL=1) ;;
        gl) capture_environment=(SDL_VIDEODRIVER=x11 SDL_VIDEO_X11_FORCE_EGL=0) ;;
        *) echo "GPU_TRACE_API must be gl or egl." >&2; exit 1 ;;
    esac
    dotnet build UntitledGemGame.csproj -c Release
    mkdir -p traces
    capture="$PWD/traces/gpu_$(date +%Y%m%d_%H%M%S).trace"
    capture_log="${capture%.trace}.capture.log"
    echo "Capturing Release using $api. Reproduce the slowdown, then close the game normally."
    printf 'Capture backend settings: %s\n' "${capture_environment[*]}"
    if ! env "${capture_environment[@]}" apitrace trace --api "$api" --output "$capture" dotnet "$PWD/bin/Release/net10.0/UntitledGemGame.dll" 2>&1 | tee "$capture_log"; then
        echo "Capture failed. See $capture_log" >&2
        exit 1
    fi
    if [[ ! -s "$capture" ]]; then
        echo "No OpenGL calls were captured ($api tracer). See $capture_log" >&2
        echo "Check the SDL video backend; use GPU_TRACE_API=egl for EGL or GPU_TRACE_API=gl for GLX." >&2
        exit 1
    fi
else
    echo "Usage: $0 [--replay path/to/capture.trace]" >&2
    exit 1
fi
profile="${capture%.trace}.profile.txt"
echo "Replaying capture to measure GPU execution..."
replay_log="${capture%.trace}.replay.log"
# Publish the report only after replay succeeds and produces timing records.
pending=$(mktemp "${profile}.XXXXXX")
trap 'rm -f -- "$pending"' EXIT
if ! apitrace replay --headless --pgpu --pcpu "$capture" > "$pending" 2> "$replay_log"; then
    cat "$replay_log" >&2
    echo "GPU replay failed. Capture preserved: $capture" >&2
    exit 1
fi
if ! grep -q '^call ' "$pending"; then
    cat "$replay_log" >&2
    echo "Replay produced no draw-call timing records. See $replay_log" >&2
    exit 1
fi
mv -- "$pending" "$profile"
echo "GPU/CPU timing profile: $profile"
echo "Open the capture in qapitrace: $capture"
