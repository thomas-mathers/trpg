#!/bin/bash
# add-migration.sh — creates a new EF Core migration from current model changes.
#
# Usage: ./scripts/add-migration.sh <MigrationName>
#   Same as `dotnet ef migrations add <MigrationName>`, scaffolds a new
#   migration file under api/TRPG.Data based on model changes since the last
#   migration. Does not touch the database — use scripts/migrate.sh to apply it.
#
# Wraps the full command from AGENTS.md's Migrations section so nobody has to
# retype --project/--startup-project/--context by hand.

set -uo pipefail

if [ $# -eq 0 ]; then
    echo "Usage: $0 <MigrationName>" >&2
    exit 1
fi

cd "$(dirname "$0")/.."

dotnet ef migrations add "$1" --project api/TRPG.Data --startup-project api/TRPG.Data --context TrpgDbContext
