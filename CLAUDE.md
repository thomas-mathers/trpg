# TRPG Codebase Conventions

## Language & Framework

- C# .NET 9, EF Core 9.0.4, Npgsql 9.0.4
- PostgreSQL via Testcontainers in tests
- xUnit for integration tests — no unit tests, no mocking, except HTTP endpoint tests (see Integration Tests § Endpoint Tests) which mock the Ollama client deliberately
- `TRPG` is an ASP.NET Core minimal API host; `TRPG.Client` is a separate thin console client talking to it over HTTP; `TRPG.Contracts` holds the shared request/response DTOs both reference

---

## C# Style

### General
- File-scoped namespaces (`namespace TRPG.Models;`)
- Primary constructors everywhere (`public class PersonService(TrpgDbContext context)`)
- Expression-bodied members for simple one-liners
- Place related types (classes, records, enums) in the same file as the class they primarily support — no standalone `Enums.cs` or similar
- Use named parameters when constructing records or objects with multiple positional arguments of the same or similar types (e.g. `new StatAffinities(Strength: 3, Defense: 2, ...)` not `new StatAffinities(3, 2, ...)`)
- No alignment padding — do not add extra spaces to align `=`, `:`, or other tokens across lines

### Comments
- Explain *why*, never *how* — well-named identifiers make the what and how obvious
- Use a comment only when a future reader would be genuinely confused without it; if removing it wouldn't confuse anyone, don't write it
- No XML doc comments

### Naming
- `_camelCase` for private fields
- `PascalCase` for everything public
- No abbreviations — write `minimum`, `maximum`, `quantity`, `defense`, `index`, not `min`, `max`, `qty`, `def`, `idx`
- No tuple return types or tuple parameters — use a named `record` instead; tuples as local variables inside method bodies are fine
- Functions with more than 3 parameters must capture those parameters in a class instead (constructors excluded — DI constructors may have as many parameters as needed)
- Test classes: `{Subject}Tests`
- Test methods: `Method_ExpectedResult_WhenCondition`

### Classes
- Each class has a single responsibility — if describing it requires "and", split it into two classes

### Functions
- Each function does one thing — if you need "and" to describe what it does, split it
- Prefer pure functions (static methods that depend only on their parameters and return a value with no side effects) where possible
- Keep functions under 40 lines; if a function exceeds this, extract helpers

### Null handling
- `null!` for fields initialized in `InitializeAsync` (not inline)
- Nullable reference types enabled; use `?` where genuinely optional

### Collections
- `List<T>` **only** for PostgreSQL array columns (Npgsql requires it — `Collection<T>` throws at runtime)
- `Collection<T>` for any other mutable public/internal collection property
- Public/internal method return types and parameters: use `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, or `IReadOnlyDictionary<K,V>` — never expose concrete collection types in signatures
- Private method signatures and local fields may use concrete types (`List<T>`, `Dictionary<K,V>`) for performance
- Collection expressions `[]` for empty collections, `[x, y]` for inline initialization
- When returning `IReadOnlyCollection<T>` or `IReadOnlyList<T>` from a method, use `.ToArray()` — never `.ToList().AsReadOnly()` or `.AsReadOnly()`

### Async
- No `Async` suffix on service methods
- `CancellationToken cancellationToken = default` on every public service method
- `await using` for disposable contexts
- Never chain member access directly on an awaited expression: `(await Foo()).Bar` — always assign to a variable first

---

## Models

### Immutability
- Prefer `init` by default — only use `set` when a property genuinely needs to change after construction
- `set` is justified for runtime game state: `Gold`, `Biography`, `Location`, `Progression`, `Attributes`, `DurabilityCurrent`
- Standard Id pattern: `public Guid Id { get; init; } = Guid.NewGuid();`

### Value objects
- `record` for purely scalar value objects: `Meter(int Current, int Maximum)`, `Point(int X, int Y)`, `Rectangle`
- `class` for value objects that contain other owned types: `Location`, `Circle`, `Progression`, `Attributes`
  (EF Core cannot bind owned entity types to positional record constructor parameters)

### Strings
- No max-length constraints — PostgreSQL `text` is used throughout

---

## EF Core / Data

### Naming
- `UseSnakeCaseNamingConvention()` applied globally — all tables and columns are snake_case
- DbSet accessor: expression body using `Set<T>()`, not auto-property

### Owned entities
- `ToJson()` for owned types not queried/indexed (e.g. `Attributes`, `Progression`, `WorldEvent.Region`)
- No `ToJson()` for owned types that need indexed columns (e.g. `Person.Location`) — these flatten to regular columns
- Composite index `(RegionId, BuildingId)` on any flattened `Location`

### Enum storage
- All enums stored as `string` via `HaveConversion<string>()` in `ConfigureConventions`

### MaxAsync on nullable
- Use `MaxAsync(x => (int?)x.Field) ?? -1` — `DefaultIfEmpty(-1).MaxAsync()` cannot be translated by EF Core

### Unique constraints
- Let EF Core throw `DbUpdateException` on violations — no pre-check queries

### World scoping
- Every new entity/table must have a `WorldId` column with `HasIndex(x => x.WorldId)` — no FK constraint needed (matches other loose Guid references like `Job.PersonId`), just the indexed column
- This keeps `DropWorldCommandHandler` a flat `Where(x => x.WorldId == worldId).ExecuteDeleteAsync(...)` per table — never make it derive world membership by chasing a parent entity's FK chain again

---

## Services

### Structure
- Primary constructor injection: `public class InventoryService(TrpgDbContext context)`
- No interfaces — direct concrete classes
- Throw `InvalidOperationException` for business rule violations
- No pre-checks for uniqueness — rely on DB constraints

### Patterns
- `FindAsync([id], cancellationToken)` for PK lookups
- `ExecuteDeleteAsync` for hard deletes
- `FirstOrDefaultAsync` + null check for lookups with side effects

---

## Rider MCP Tools

### Known issues
- `mcp__rider__rename_refactoring` **does not work for C# symbols** — always returns "Couldn't find symbol 'X' in file 'Y'" regardless of path format or symbol name. Known JetBrains bug: RIDER-136391. Use the `Edit` tool for targeted renames instead.
- `mcp__rider__get_symbol_info` always returns `{"documentation":""}` for all positions — symbol info is unavailable via MCP.
- `mcp__rider__search_symbol` returns file-level positions (line 1, col 1) rather than actual symbol positions — behaves like a text search, not semantic search.

### Path format
- `projectPath`: use forward slashes, point to the solution root — e.g. `C:/Users/mathe/RiderProjects/TRPG`
- `pathInProject`: use forward slashes, relative to solution root — e.g. `TRPG/Models/City.cs`

### Working tools
- `mcp__rider__build_solution` — triggers a full incremental build; confirms project compiles
- `mcp__rider__get_file_problems` — runs Rider code analysis on a file; useful to verify edits are error-free
- `mcp__rider__list_directory_tree` — filesystem tree exploration
- `mcp__rider__get_solution_projects` — lists projects in the solution
- `mcp__rider__open_file_in_editor` — opens a file in Rider (occasionally times out; retry once if so)
- `mcp__rider__execute_terminal_command` — runs shell commands in Rider's terminal (requires "Brave Mode" enabled in MCP settings to skip confirmation prompts; otherwise times out)

---

## Ollama Model Benchmarking

- `scripts/benchmark-model.sh` and `scripts/benchmark-models.sh` measure move-command reliability and latency for a given Ollama model + thinking-mode combination, run against the server's real HTTP endpoints (`POST /worlds/{id}/sessions` then `POST /sessions/{id}/chat`) — built to replace manual edit-source-and-rebuild A/B testing
- Full usage, argument syntax, and design rationale (why the server restarts per trial, why there's an untimed warmup, why there's no capability auto-detection) live in each script's own header comment — read that first, don't rely on this note for exact syntax
- Some models crash the server if asked to think when they don't support it (e.g. `mistral-nemo:12b`) — there's no auto-detection, the caller has to know which models can take `@true`/`@both`
- Results accumulate across every invocation in `scripts/benchmark-results.csv` (gitignored — local experiment output, not committed), grouped by a `tag` column
- Readiness polling uses `curl` against a real endpoint, not `Get-NetTCPConnection`/`Get-Process` — repeated `powershell.exe` spawns alongside a backgrounded process have been observed to stall badly in some shells

---

## Integration Tests

### Infrastructure
- `DatabaseFixture` spins up `postgres:17` via Testcontainers, runs `MigrateAsync` once
- All test classes share the container via `[Collection("Database")]`
- xUnit creates a new class instance per test — `IAsyncLifetime` handles per-test setup/teardown
- `InitializeAsync` creates a fresh `TrpgDbContext` and seeds shared state
- `DisposeAsync` disposes the context: `public async Task DisposeAsync() => await _context.DisposeAsync();`

### Test class structure
```csharp
[Collection("Database")]
public class FooServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private FooService _service = null!;
    private SomeEntity _entity = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new FooService(_context);
        _entity = ...;
        _context.Foos.Add(_entity);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();
}
```

### Seeding strategy
- Promote entities to class fields in `InitializeAsync` when every (or nearly every) test needs them
- Add `private async Task<T> Seed*(...)` helper methods for entities needed by only a subset of tests
- Seed helpers add to context, save, and return the entity
- Seed helpers return a single entity — never a tuple; use separate helpers if a test needs multiple seeded entities
- `Builders` static class (`TRPG.Tests.Helpers`) constructs valid model objects

### Builders
- Named `Make{Entity}`: `Builders.MakePerson()`, `Builders.MakeItem()`, `Builders.MakeSkill()`, `Builders.MakeQuest(giverId)`
- Fields with unique DB constraints use Guid suffix: `$"Item-{Guid.NewGuid():N}"`
- Optional parameters for FK overrides: `MakePerson(worldId: ...)`
- `Person.Name` has no unique constraint so a static string is fine

### AAA sections
- Every test has `// Arrange`, `// Act`, `// Assert` comments
- Exception-throwing tests use `// Act & Assert`
- Omit empty sections rather than writing a comment with nothing under it

### Verifying deletes
- Open a second context to verify deletion — the original context change tracker still holds the entity

### Unique name collisions
- Tests share one Postgres container with no rollback between tests
- Any entity with a unique name constraint must use a Guid-suffixed name in builders and seed helpers

### Endpoint tests
- The one deliberate exception to "no mocking": HTTP endpoint tests (`WorldEndpointsTests`, `SessionEndpointsTests`) mock the Ollama client, because Ollama is an external, non-deterministic, slow LLM service — unlike Postgres, it can't be spun up reliably via Testcontainers, and real narration text isn't what these tests are checking
- `EndpointTestFixture` wraps `WebApplicationFactory<Program>` in its own `[Collection("Endpoints")]` (a separate Postgres container from the `"Database"` collection), and swaps in `FakeOllamaApiClient` (`TRPG.Tests.Helpers`) for `IOllamaApiClient`
- `FakeOllamaApiClient.GenerateAsync` detects which of the 5 world-generation schemas is being requested (races/factions/cities/roads/geography-entity) from keywords in the prompt text, then constructs and serializes **the real production schema types** (`RaceListSchema`, `FactionListSchema`, etc., widened from `file class` to `internal class` in their generator files specifically so tests can reference them) — never loosely-typed anonymous objects, so a breaking change to those schemas forces the test to be updated rather than silently drifting
- Tests that exercise world generation (`CreateWorld_...`) pass a `CreateWorldRequest` with every count knob set to 1 (or 0 for optional entities) to keep the fake's canned responses trivial
- Corner-case tests (404s for an unknown session id, 400 for invalid `/wait` input) use the same fixture — those code paths never reach Ollama, so no special handling is needed
- Seed data for session tests goes straight through a `TrpgDbContext` pulled from `fixture.CreateScope()`, the same way non-HTTP integration tests seed via `DatabaseFixture.CreateContext()`
