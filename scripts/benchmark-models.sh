#!/bin/bash
# benchmark-models.sh — run benchmark-model.sh across a list of models, with each
# model specifying its own thinking mode(s) to test, under one shared tag.
#
# Usage: ./benchmark-models.sh [--tag "label"] [--world-id <uuid>] <trials> <model-spec1> [model-spec2] ...
#   --tag        optional label written to every row's tag column instead of an
#                auto-generated one, e.g. --tag "temperature 0.5". Can appear
#                anywhere in the arguments. Defaults to an auto-generated
#                "tag-<timestamp>" if omitted.
#   --world-id   world UUID whose player to test against. Can appear anywhere in the
#                arguments. Omit entirely to use the first world with a player (see
#                benchmark-model.sh).
#   trials       trials per script (hard/easy) — applied to every model/think combination
#   model-spec   MODEL[@true|@false|@both][@temperature], e.g.:
#                  qwen3.5:9b@both              - test both thinking modes, default temperature
#                  gemma4:latest@true           - only thinking mode, default temperature
#                  mistral-nemo:12b@false@0.2   - only non-thinking mode, temperature=0.2
#                  mistral-nemo:12b             - no @suffix defaults to @both, default temperature
#                Temperature applies to every think variant tested for that model-spec —
#                if you want different temperatures per think setting, list the model twice
#                with two separate specs instead of using @both.
#
# Think is the inner loop for any model tested @both, so both variants of that
# model run back-to-back while it's already warm in Ollama's memory, before
# moving on and evicting it for the next model.
#
# All runs in this invocation share a single tag (custom label or
# auto-generated), written to benchmark-results.csv alongside every trial, so
# this comparison's rows can be grouped and filtered apart from any other
# benchmark session in the same file.
#
# This is intentionally a thin loop over benchmark-model.sh rather than teaching
# that script to juggle multiple models/think settings itself — keeps the
# single-model script simple for quick one-off checks, and keeps the "sweep"
# concern in one place.
#
# No capability auto-detection: some models (e.g. mistral-nemo:12b) crash the
# agent server if asked to think when they don't support it. It's on the
# caller to only request @true/@both for models known to support thinking. If
# a combination does fail, that one is logged and skipped — the rest of the
# list still runs.

set -euo pipefail

TAG=""
WORLD_ID=""
ARGS=()
while [ "$#" -gt 0 ]; do
    case "$1" in
        --tag)
            TAG="$2"
            shift 2
            ;;
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

if [ "$#" -lt 2 ]; then
    echo "Usage: benchmark-models.sh [--tag \"label\"] [--world-id <uuid>] <trials> <model-spec1> [model-spec2] ..." >&2
    echo "  model-spec: MODEL[@true|@false|@both][@temperature] (default @both, no temperature override)" >&2
    exit 1
fi

TRIALS="$1"
shift

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TAG="${TAG:-tag-$(date +%s)}"

echo "=== Benchmarking $# model spec(s) under tag=$TAG ==="

for spec in "$@"; do
    IFS='@' read -ra parts <<< "$spec"
    model="${parts[0]}"
    think_spec="${parts[1]:-both}"
    temperature="${parts[2]:-}"

    case "$think_spec" in
        true) think_list=("true") ;;
        false) think_list=("false") ;;
        both) think_list=("true" "false") ;;
        *)
            echo "ERROR: invalid think spec '@$think_spec' for model '$model' (expected @true, @false, or @both)" >&2
            exit 1
            ;;
    esac

    for think in "${think_list[@]}"; do
        echo ""
        echo ">>> $model think=$think${temperature:+ temperature=$temperature}"
        if [ -n "$WORLD_ID" ]; then
            success=0
            "$SCRIPT_DIR/benchmark-model.sh" --world-id "$WORLD_ID" "$model" "$think" "$TRIALS" "$TAG" "$temperature" || success=$?
        else
            success=0
            "$SCRIPT_DIR/benchmark-model.sh" "$model" "$think" "$TRIALS" "$TAG" "$temperature" || success=$?
        fi
        if [ "$success" -ne 0 ]; then
            echo "!!! FAILED: $model think=$think temperature=${temperature:-default} — continuing with remaining combinations" >&2
        fi
    done
done

echo ""
echo "=== All model specs done. tag=$TAG ==="
