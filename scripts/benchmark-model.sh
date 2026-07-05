#!/bin/bash
# benchmark-model.sh — controlled reliability/latency benchmark for an Ollama model + think setting.
#
# Usage: ./benchmark-model.sh [--world-id <uuid>] <model> <true|false> [trials-per-script] [tag] [temperature]
#   --world-id         the world's UUID (from the worlds table) whose player to test against.
#                       Can appear anywhere in the arguments. Omit entirely to use the first
#                       world with a player, ordered by name — same default as the game's own
#                       world picker.
#   model              Ollama model tag, e.g. qwen3.5:9b, gemma4:latest, mistral-nemo:12b
#   true|false         whether to enable the model's thinking mode
#   trials-per-script  number of trials to run for each of the two test scripts (default 4)
#   tag                arbitrary label written to every row's tag column, so several
#                       invocations (e.g. one per model in a comparison) can be grouped
#                       together in the CSV later. Defaults to an auto-generated
#                       "tag-<timestamp>" if omitted — every invocation always gets some
#                       tag, even standalone ones.
#   temperature        optional sampling temperature override (e.g. 0.2). Omit or pass ""
#                       to leave the model's own default temperature untouched.
#
# Runs two fixed move commands N times each, each trial against a freshly
# started server with a freshly created session:
#   - "hard" script: an indirect district reference ("lets go to the city center")
#   - "easy" script: a direct reference using the target district's actual name
# Both target that world's CityCenter-type district from its Residential-type
# district, resolved by district_type (not hardcoded IDs/names) so the script
# works against any world, not just one specific save. Success is verified via
# a direct DB query after each trial, not by trusting the model's narration —
# models can describe arrival without ever having called the move tool. The
# server restarts per trial (rather than being reused across all of them)
# because the opening prompt is baked into a session's chat history once at
# creation — reusing a session across a DB reset would leave stale location
# context in the conversation.
#
# Requires: the TRPG solution already built (dotnet build), the trpg-postgres-1
# and trpg-ollama-1 docker containers running, and the given world having a
# player character in a city with both a Residential and a CityCenter district.
#
# Runs one untimed, uncounted warmup request before any real trial, so a cold
# model load (VRAM contention from whatever was last loaded in Ollama) doesn't
# skew the first counted trial's latency.
#
# Every trial's result is appended as one row to scripts/benchmark-results.csv
# (created with a header on first run). The file is shared across invocations,
# so running this once per model in a list builds up one combined CSV you can
# open in a spreadsheet to compare everything together.
#
# The server is launched with --LogDirectory pointing at a run-specific temp directory
# (not the game's normal logs/ folder), so this run's structured [perf]/think
# logging is isolated from ordinary gameplay logs and from other benchmark
# runs — no need to correlate by timestamp across one shared multi-day file.

set -euo pipefail

USAGE="Usage: benchmark-model.sh [--world-id <uuid>] <model> <true|false> [trials] [tag] [temperature]"

WORLD_ID=""
ARGS=()
while [ "$#" -gt 0 ]; do
    case "$1" in
        --world-id)
            WORLD_ID="$2"
            shift 2
            ;;
        *)
            ARGS+=("$1")
            shift
            ;;
    esac
done
set -- "${ARGS[@]}"

MODEL="${1:?$USAGE}"
THINK="${2:?$USAGE}"
TRIALS="${3:-4}"
TAG="${4:-tag-$(date +%s)}"
TEMPERATURE="${5:-}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BINDIR="$REPO_ROOT/TRPG/bin/Debug/net10.0"
RUN_TIMESTAMP="$(date +%s)"
LOG_FILE="/tmp/benchmark-$RUN_TIMESTAMP.log"
APP_LOG_DIR="/tmp/benchmark-$RUN_TIMESTAMP-applogs"
CSV_FILE="$REPO_ROOT/scripts/benchmark-results.csv"

psql_exec() {
    docker exec trpg-postgres-1 psql -U postgres -d trpg -t -A -c "$1"
}

if [ -z "$WORLD_ID" ]; then
    WORLD_ID=$(psql_exec "SELECT id FROM worlds WHERE player_id IS NOT NULL ORDER BY name LIMIT 1;")
    if [ -z "$WORLD_ID" ]; then
        echo "ERROR: no worlds with a player found" >&2
        exit 1
    fi
    echo "No world-id given, using: $WORLD_ID"
fi

PLAYER_ID=$(psql_exec "SELECT player_id FROM worlds WHERE id = '$WORLD_ID';")
if [ -z "$PLAYER_ID" ]; then
    echo "ERROR: no player found for world $WORLD_ID" >&2
    exit 1
fi

CITY_ID=$(psql_exec "SELECT city_id FROM persons WHERE id = '$PLAYER_ID';")
START_DISTRICT_ID=$(psql_exec "SELECT id FROM districts WHERE city_id = '$CITY_ID' AND district_type = 'Residential' LIMIT 1;")
TARGET_DISTRICT_ID=$(psql_exec "SELECT id FROM districts WHERE city_id = '$CITY_ID' AND district_type = 'CityCenter' LIMIT 1;")
TARGET_DISTRICT_NAME=$(psql_exec "SELECT name FROM districts WHERE id = '$TARGET_DISTRICT_ID';")

if [ -z "$START_DISTRICT_ID" ] || [ -z "$TARGET_DISTRICT_ID" ]; then
    echo "ERROR: world $WORLD_ID's city is missing a Residential or CityCenter district" >&2
    exit 1
fi

if [ ! -f "$CSV_FILE" ]; then
    echo "timestamp,tag,world_id,model,think,temperature,script,trial,success,elapsed_ms,first_token_ms,token_count,tokens_per_second" > "$CSV_FILE"
fi

reset_player() {
    psql_exec "UPDATE persons SET district_id = '$START_DISTRICT_ID', room_id = NULL WHERE id = '$PLAYER_ID';" >/dev/null
}

current_district_id() {
    psql_exec "SELECT district_id FROM persons WHERE id = '$PLAYER_ID';"
}

start_server() {
    powershell -Command "Get-Process -Name TRPG -ErrorAction SilentlyContinue | Stop-Process -Force" >/dev/null 2>&1 || true
    # 2s, not 1s: Windows can take a moment to fully release the port 5000
    # binding after the old process is killed. Too short a wait risks the new
    # process crashing with "Failed to listen... conflicts with an existing
    # registration" while curl still sees a (stale) Listening state and the
    # trial silently records a bogus near-instant failure instead of a real one.
    sleep 2
    local original_dir="$PWD"
    cd "$BINDIR"
    if [ -n "$TEMPERATURE" ]; then
        ./TRPG.exe --OllamaModel "$MODEL" --OllamaThink "$THINK" --OllamaTemperature "$TEMPERATURE" \
            --LogDirectory "$APP_LOG_DIR" >> "$LOG_FILE" 2>&1 &
    else
        ./TRPG.exe --OllamaModel "$MODEL" --OllamaThink "$THINK" --LogDirectory "$APP_LOG_DIR" >> "$LOG_FILE" 2>&1 &
    fi
    disown
    cd "$original_dir"

    # A curl round-trip against a real endpoint confirms the app is actually
    # accepting and answering HTTP requests, not just that a socket is bound —
    # a stronger check than Get-NetTCPConnection, and it avoids spawning
    # powershell.exe on every poll iteration (spawning it repeatedly alongside
    # a backgrounded process has been observed to stall badly in some shells).
    for _ in $(seq 1 60); do
        local code
        code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/worlds 2>/dev/null)
        if [ "$code" = "200" ]; then
            return 0
        fi
        sleep 1
    done

    echo "ERROR: server did not start within 60s" >&2
    return 1
}

# The server has no more --agent mode — every run against it is just a normal
# session. Each trial gets its own freshly created session right after the
# server starts, so its opening-turn chat history never leaks between trials.
start_session() {
    local response session_id
    response=$(curl -s -X POST "http://localhost:5000/worlds/$WORLD_ID/sessions")
    session_id=$(echo "$response" | grep -oE '"sessionId":"[^"]+"' | grep -oE '[0-9a-fA-F-]{36}')
    if [ -z "$session_id" ]; then
        echo "ERROR: failed to start session for world $WORLD_ID: $response" >&2
        return 1
    fi
    echo "$session_id"
}

stop_server() {
    powershell -Command "Get-Process -Name TRPG -ErrorAction SilentlyContinue | Stop-Process -Force" >/dev/null 2>&1 || true
}

# Untimed, uncounted request that forces the model into VRAM before any real
# trial runs. Ollama's own keep_alive (effectively infinite on this setup)
# keeps it resident across every subsequent server restart in this run, so one
# warmup at the very start covers both the hard and easy sections below.
warmup() {
    echo "Warming up $MODEL (untimed)..."
    reset_player
    start_server
    local session_id
    session_id=$(start_session) || { stop_server; return 1; }
    curl -s -X POST "http://localhost:5000/sessions/$session_id/chat" -H "Content-Type: application/json" \
        -d "{\"message\":\"lets go to $TARGET_DISTRICT_NAME\"}" >> "$LOG_FILE" 2>&1 || true
    stop_server
}

run_trial() {
    local script_name="$1" message="$2" trial_number="$3"

    # Reset BEFORE starting the server: the opening prompt is built once at server
    # startup and baked into the chat's message history, so a stale district there
    # would confuse later turns even if the DB itself is reset afterward. Each
    # trial gets a fully fresh server + fresh history, not a reused long-lived one.
    reset_player
    start_server

    local session_id
    session_id=$(start_session) || { stop_server; echo "FAIL 0"; return; }

    local start_ns end_ns elapsed_ms response district success
    start_ns=$(date +%s%N)
    response=$(curl -s -X POST "http://localhost:5000/sessions/$session_id/chat?includeMetrics=true" \
        -H "Content-Type: application/json" -d "{\"message\":\"$message\"}" 2>&1) || response="CURL_FAILED"
    end_ns=$(date +%s%N)
    elapsed_ms=$(( (end_ns - start_ns) / 1000000 ))

    stop_server

    {
        echo "--- $message ---"
        echo "$response"
    } >> "$LOG_FILE"

    district=$(current_district_id)
    if [ "$district" = "$TARGET_DISTRICT_ID" ]; then
        success="true"
    else
        success="false"
    fi

    local first_token_ms token_count tokens_per_second
    first_token_ms=$(echo "$response" | grep -oE '"firstTokenMs":[0-9]+' | grep -oE '[0-9]+')
    token_count=$(echo "$response" | grep -oE '"tokenCount":[0-9]+' | grep -oE '[0-9]+')
    tokens_per_second=$(echo "$response" | grep -oE '"tokensPerSecond":[0-9.]+' | grep -oE '[0-9.]+$')

    echo "$(date -u +%Y-%m-%dT%H:%M:%SZ),\"$TAG\",$WORLD_ID,\"$MODEL\",$THINK,$TEMPERATURE,\"$script_name\",$trial_number,$success,$elapsed_ms,$first_token_ms,$token_count,$tokens_per_second" >> "$CSV_FILE"

    if [ "$success" = "true" ]; then
        echo "SUCCESS $elapsed_ms"
    else
        echo "FAIL $elapsed_ms"
    fi
}

run_script() {
    local script_name="$1" message="$2"
    local -a results=()

    echo "--- $script_name ---"
    for i in $(seq 1 "$TRIALS"); do
        local result
        result=$(run_trial "$script_name" "$message" "$i")
        echo "  trial $i: $result"
        results+=("$result")
    done

    local success_count=0 total_ms=0
    for r in "${results[@]}"; do
        local status="${r%% *}" ms="${r##* }"
        if [ "$status" = "SUCCESS" ]; then
            success_count=$((success_count + 1))
        fi
        total_ms=$((total_ms + ms))
    done
    local avg_ms=$((total_ms / TRIALS))
    echo "$script_name: $success_count/$TRIALS succeeded, avg ${avg_ms}ms"
}

TEMPERATURE_LABEL="${TEMPERATURE:-default}"
echo "=== Benchmarking model=$MODEL think=$THINK temperature=$TEMPERATURE_LABEL trials=$TRIALS tag=$TAG (target: $TARGET_DISTRICT_NAME) ==="
echo "Full responses logged to: $LOG_FILE"
echo "App structured logs (per-request [perf]/think detail) in: $APP_LOG_DIR"
echo "Results appended to: $CSV_FILE"

warmup

run_script "hard" "lets go to the city center"
run_script "easy" "lets go to $TARGET_DISTRICT_NAME"

echo "=== Done ==="
