#!/bin/bash
# migrate.sh — applies pending EF Core migrations to a running Postgres instance.
#
# Usage: ./scripts/migrate.sh [target-migration-name]
#   With no argument, applies every pending migration. With a migration name,
#   updates the database to that migration (forward or backward), same as
#   `dotnet ef database update <Name>`.
#
# Wraps the full command from AGENTS.md's Migrations section so nobody has to
# retype --project/--startup-project/--context by hand. Tests never need
# this — DatabaseFixture calls MigrateAsync itself against its own
# Testcontainers instance.

set -uo pipefail

cd "$(dirname "$0")/.."

dotnet ef database update "$@" --project api/TRPG.Data --startup-project api/TRPG.Data --context TrpgDbContext
