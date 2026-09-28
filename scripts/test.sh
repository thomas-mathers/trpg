#!/bin/bash
# test.sh — runs the test suite and prints only failures and the final summary.
#
# Usage: ./scripts/test.sh [dotnet test args...]
#   Any extra arguments are passed through to `dotnet test` (e.g.
#   --filter-class "*SomeTests" to run one class, or --filter-namespace
#   "TRPG.Tests.Application.Combat" to run one feature's tests).
#
# The test runner is Microsoft.Testing.Platform, opted in via
# api/global.json, not VSTest — `--logger` doesn't apply here, and MTP mode
# only activates when the working directory is inside api/ (where
# global.json is found), so this cds there rather than passing api/TRPG.sln
# from the repo root.
#
# There's no built-in "quiet on success" mode: `--output` only offers
# Detailed/Normal, and Normal still unconditionally prints a per-assembly
# "passed (Xs)" banner even when every test passed, plus `-v q` doesn't
# suppress compiler warnings during the test build the way it does for
# build.sh. This filters both of those out, along with the startup banner —
# everything a failure prints (name, message, stack trace, captured output)
# and the final Passed!/Failed! summary always survive the filter.

set -uo pipefail

cd "$(dirname "$0")/../api"

dotnet test TRPG.sln -v q --output Normal --no-ansi "$@" 2>&1 \
  | grep -vE "^Running tests from|: warning |\) passed \("

exit "${PIPESTATUS[0]}"
