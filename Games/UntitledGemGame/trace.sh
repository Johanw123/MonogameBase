#!/usr/bin/env bash

# Safety checks
if ! command -v dotnet-trace &> /dev/null; then
    echo "Error: 'dotnet-trace' tool not found. Install it with: dotnet tool install --global dotnet-trace"
    exit 1
fi

GAME_NAME="${1:-$(basename "$PWD")}"
CONFIGURATION="Debug"
OUTPUT_DIR="$(pwd)/traces"
OUTPUT_FILE="${OUTPUT_DIR}/trace_$(date +%Y%m%d_%H%M%S)"

mkdir -p "$OUTPUT_DIR"

echo "=== Searching for running instance of '$GAME_NAME' in dotnet-trace ps... ==="

# Helper function to query dotnet-trace ps for our target process
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
        kill "$KITTY_PID" 2>/dev/null
    fi

    # Only kill the game if THIS script launched it
    if [ "$SPAWNED_BY_US" = true ] && [ -n "$GAME_PID" ]; then
        echo "=== Terminating game process ($GAME_PID) ==="
        kill "$GAME_PID" 2>/dev/null
        wait "$GAME_PID" 2>/dev/null
    else
        echo "=== Leaving original game process ($GAME_PID) running ==="
    fi

    # Open in Firefox via Speedscope local server
    SPEEDSCOPE_FILE="${OUTPUT_FILE}.speedscope.json"
    if [ -f "$SPEEDSCOPE_FILE" ]; then
        echo "=== Opening trace in Firefox via Speedscope ==="
        BROWSER="firefox --new-window" speedscope "$SPEEDSCOPE_FILE"
    else
        echo "Error: Output trace file ($SPEEDSCOPE_FILE) was not found."
    fi
    exit 0
}

trap cleanup SIGINT SIGTERM

echo "=== 3. Spawning dotnet-trace in Kitty ==="

kitty --title "dotnet-trace ($GAME_PID)" \
    dotnet-trace collect \
        --process-id "$GAME_PID" \
        --format "Speedscope" \
        --output "$OUTPUT_FILE" &

KITTY_PID=$!

# Wait for Kitty (the tracing session) to exit.
# When tracing an already running app, stopping tracing inside Kitty finish the script.
wait "$KITTY_PID" 2>/dev/null

cleanup
