# Local Setup

## Git Hooks

1. Run `git config core.hooksPath .githooks`

## MCP Servers

### roslyn-codelens (C#)

1. `dotnet tool install -g roslyncodelens.mcp`

### typescript-mcp (TypeScript)

1. Install a Go toolchain
2. `go install github.com/paulvanbrenk/typescript-mcp/cmd/typescript-mcp@latest`
3. Add `%USERPROFILE%\go\bin` to `PATH`
4. `npm install -g @typescript/native-preview`

Note: there's an unrelated npm package also named `typescript-mcp` — don't `npm install -g typescript-mcp` by mistake.

### postgres-mcp (database inspection, query analysis, index suggestions)

1. `docker pull crystaldba/postgres-mcp`
2. Requires the local `trpg-postgres-1` container (`docker-compose.yml`) to be running.

Runs read-only (`--access-mode=restricted`) against `postgresql://postgres:postgres@host.docker.internal:5432/trpg`. Index-suggestion tools (`analyze_workload_indexes`, `analyze_query_indexes`, `get_top_queries`) need the `pg_stat_statements` and `hypopg` extensions, which aren't set up yet — that's a follow-up (needs a custom Postgres image for `hypopg`, plus `shared_preload_libraries = pg_stat_statements` and a container restart). Until then, `execute_sql`, `explain_query`, `list_schemas`/`list_objects`, and `analyze_db_health` all work.
