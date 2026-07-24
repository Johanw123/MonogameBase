#!/usr/bin/env bash

# Resolve user's home directory if script is executed via sudo
REAL_USER="${SUDO_USER:-$USER}"
REAL_HOME=$(eval echo "~$REAL_USER")

# Check for dotnet-trace in PATH or user's .dotnet/tools directory
if command -v dotnet-trace &> /dev/null; then
    DOTNET_TRACE_BIN=$(command -v dotnet-trace)
elif [ -f "${REAL_HOME}/.dotnet/tools/dotnet-trace" ]; then
    DOTNET_TRACE_BIN="${REAL_HOME}/.dotnet/tools/dotnet-trace"
else
    echo "Error: 'dotnet-trace' tool not found in PATH or ${REAL_HOME}/.dotnet/tools."
    echo "Install it with: dotnet tool install --global dotnet-trace"
    exit 1
fi

GAME_NAME="${1:-$(basename "$PWD")}"
CONFIGURATION="Debug"
OUTPUT_DIR="$(pwd)/traces"
OUTPUT_FILE="${OUTPUT_DIR}/trace_$(date +%Y%m%d_%H%M%S).nettrace"

mkdir -p "$OUTPUT_DIR"

echo "=== Searching for running instance of '$GAME_NAME' in dotnet-trace ps... ==="

get_game_pid() {
    dotnet-trace ps | grep -i "$GAME_NAME" | head -n 1 | awk '{print $1}'
}

GAME_PID=$(get_game_pid)
SPAWNED_BY_US=false

if [ -n "$GAME_PID" ]; then
    echo "----------------------------------------------------"
    echo "FOUND ALREADY RUNNING INSTANCE! Game PID: $GAME_PID"
    echo "----------------------------------------------------"
else
    echo "=== 1. No running instance found. Starting via 'dotnet run' ==="
    dotnet run -c "$CONFIGURATION" &
    RUN_PID=$!
    SPAWNED_BY_US=true

    echo "=== 2. Waiting for '$GAME_NAME' to show up in 'dotnet-trace ps'... ==="
    MAX_ATTEMPTS=60
    ATTEMPT=0

    while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
        GAME_PID=$(get_game_pid)
        if [ -n "$GAME_PID" ]; then
            break
        fi
        sleep 1.0
        ((ATTEMPT++))
    done

    if [ -z "$GAME_PID" ]; then
        echo "Error: Timed out waiting for process '$GAME_NAME' in dotnet-trace ps."
        kill "$RUN_PID" 2>/dev/null
        exit 1
    fi

    echo "----------------------------------------------------"
    echo "SUCCESS! Launched and found Game PID: $GAME_PID"
    echo "----------------------------------------------------"
fi

# Cleanup function to handle exit or Ctrl+C
cleanup() {
    echo ""
    echo "=== Cleaning up trace session... ==="
    
    # Kill Kitty terminal if it's still open
    if [ -n "$KITTY_PID" ]; then
        sudo kill "$KITTY_PID" 2>/dev/null
    fi

    # Terminate game process if launched by this script
    if [ "$SPAWNED_BY_US" = true ] && [ -n "$GAME_PID" ]; then
        echo "=== Terminating game process ($GAME_PID) ==="
        kill "$GAME_PID" 2>/dev/null
        wait "$GAME_PID" 2>/dev/null
    else
        echo "=== Leaving original game process ($GAME_PID) running ==="
    fi

    # Fix ownership of root-created trace files back to current user
    if [ -d "$OUTPUT_DIR" ]; then
        sudo chown -R "$REAL_USER:$REAL_USER" "$OUTPUT_DIR" 2>/dev/null
    fi

    if [ -f "$OUTPUT_FILE" ]; then
        echo "=== Converting .nettrace to Chromium format... ==="
        "$DOTNET_TRACE_BIN" convert "$OUTPUT_FILE" --format Chromium
        
        CHROMIUM_TRACE_FILE="${OUTPUT_FILE%.nettrace}.chromium.json"
        
        if [ -f "$CHROMIUM_TRACE_FILE" ]; then
            echo "----------------------------------------------------"
            echo "Trace converted: $CHROMIUM_TRACE_FILE"
            echo "=== Opening Perfetto / Chromium Visualizer ==="
            echo "----------------------------------------------------"
            
            # Open Chromium directly to Perfetto UI (modern trace viewer)
            chromium --new-window "https://ui.perfetto.dev/" &
        else
            echo "Error: Trace conversion to Chromium format failed."
        fi
    else
        echo "Error: Output trace file was not generated."
    fi
    exit 0
}

trap cleanup SIGINT SIGTERM

echo "=== 3. Spawning dotnet-trace collect-linux in Kitty ==="

kitty --title "dotnet-trace ($GAME_PID)" \
    sudo "$DOTNET_TRACE_BIN" collect-linux \
        --process-id "$GAME_PID" \
        --output "$OUTPUT_FILE" &

KITTY_PID=$!

# Wait for the tracing session window in Kitty to finish
wait "$KITTY_PID" 2>/dev/null

cleanup
