#!/bin/bash
# build.sh — builds the solution and prints only warnings, errors, and the final summary.
#
# Usage: ./scripts/build.sh [dotnet build args...]
#   Any extra arguments are passed through to `dotnet build` (e.g. -c Release,
#   or a specific .csproj path to build just one project).
#
# Plain `dotnet build` output is mostly restore/copy/target noise that isn't
# worth paying to read after every edit. This filters the raw output down to
# the lines that actually matter, so a clean build prints almost nothing.
#
# MSB3026 ("Beginning retry...") is dropped separately: it's pure file-lock
# retry spam from a running instance holding a DLL open, not a code warning,
# and the final MSB3027/MSB3021 failure it leads to still prints.

set -uo pipefail

cd "$(dirname "$0")/.."

dotnet build api/TRPG.sln --nologo "$@" 2>&1 \
  | grep -E ": (error|warning) |Build succeeded|Build FAILED|Error\(s\)|Warning\(s\)" \
  | grep -v "warning MSB3026"

exit "${PIPESTATUS[0]}"
