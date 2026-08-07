# TRPG Codebase Guide

## Project Overview

TRPG is a single-player text RPG where an LLM acts as game master — narrating scenes, roleplaying every NPC in conversation, and describing the consequences of the player's actions. The world is procedurally generated (countries, states, cities, districts, buildings, and NPCs with their own professions, schedules, relationships, and daily routines) and persists across sessions.

The LLM's role is deliberately narrow: it narrates and roleplays, but doesn't decide game outcomes. Combat, movement, and time all run through deterministic code (`CombatEngine`, the game clock, scheduling/generator classes); the LLM is only brought in afterward to describe what already happened. This keeps core mechanics reliable and repeatable regardless of which model is behind `IChatClient`.

---

## Project Structure

### Projects
- `TRPG` — ASP.NET Core minimal API host. Endpoints, SignalR hubs, DI wiring (`Program.cs`), background jobs (TickerQ)
- `TRPG.Application` — all business logic: command/query handlers, the combat engine, world/creature generators, LLM tool definitions. No web-framework references
- `TRPG.Contracts` — DTOs shared between the backend and its clients (requests/responses only, no logic)
- `TRPG.Data` — EF Core: `TrpgDbContext`, entity models, migrations
- `TRPG.Tests` — all backend tests (xUnit, Testcontainers-backed Postgres)

### Folder convention: feature-then-type
- Inside `TRPG`, `TRPG.Application`, and `TRPG.Contracts`, each top-level folder is a feature area (`Combat`, `Worlds`, `GameSessions`, `Inventory`, `Abilities`, `Creatures`, ...), not a type bucket
- Within a feature folder, subfolders group by type: `Commands/`, `Queries/`, `Tools/`, `Generators/` (Application); `Endpoints/`, `Hubs/`, `Jobs/` (host); `Requests/`, `Responses/` (Contracts)
- `TRPG.Data` is flat: `Models/` + `Migrations/` — entities aren't split by feature

### Key request flows
- **Plain HTTP**: `TRPG/<Feature>/Endpoints/<Feature>Endpoints.cs` → one or more `*QueryHandler`/`*CommandHandler` in `TRPG.Application` → `TrpgDbContext`
- **Player turn (chat/wait)**: `TRPG/GameSessions/Hubs/ChatHub.cs` (SignalR) → `GameTurnRunner` (`TRPG.Application/GameSessions/GameTurnRunner.cs`) → LLM (`IChatClient`) with tool-calling (`TRPG.Application/*/Tools/`) → narration streamed back token-by-token
- **Combat action (Attack/Defend/Item menu)**: client sends a typed `PlayerCombatAction` (`UseAbilityAction`/`UseItemAction`) over the same `SendCombatAction` hub method → `GameTurnRunner.StreamCombatActionResponse` → `PlayerCombatActionResolver` (validates the action, never throws — returns a `PlayerCombatActionResolverResult` with `Result`/`ErrorMessage`) → `CombatEngine.ProcessRound` (pure simulation over an already-validated action, also records any items consumed) → `ResolveCombatRoundCommand` persists the result and depletes any consumed inventory items → the LLM narrates the already-resolved outcome (tool-calling disabled for that one completion)

Keep this section in sync: when a change adds, removes, or moves a top-level project, a feature folder, or alters one of the flows above, update this section in the same commit. This section is structural only (project/folder map, request-flow shapes) — it should rarely need touching for ordinary feature work, which is exactly why it's worth keeping accurate.

---

## Language & Framework

- C# .NET 10, EF Core 10.0.9, Npgsql 10.0.2
- PostgreSQL via Testcontainers backs the integration tests, which are the default — pure/in-memory logic gets a direct unit test instead (see Integration Tests § When to skip the database entirely)
- xUnit throughout, no mocking except HTTP endpoint tests (see Integration Tests § Endpoint Tests), which mock the LLM client deliberately

---

## Formatting

- CSharpier formats the whole solution; version is pinned in `.config/dotnet-tools.json` (a local dotnet tool manifest) so CI and every machine use the exact same version — `dotnet tool restore` fetches it, then `dotnet csharpier format .` / `dotnet csharpier check .` run it
- CI runs `dotnet csharpier check .` and fails the build on any unformatted file
- One-time local setup: `git config core.hooksPath .githooks` — this points git at the checked-in `.githooks/pre-commit` hook, which runs `dotnet csharpier check .` before every commit and blocks it if anything is unformatted (run `dotnet csharpier format .` yourself and re-stage; the hook never auto-formats or rewrites files for you)

---

## Code Coverage

- Local only, not wired into CI. `coverlet.collector` is already a `TRPG.Tests` package reference; `reportgenerator` is pinned in `.config/dotnet-tools.json` alongside CSharpier
- Generate: `dotnet test TRPG.sln --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults`, then `dotnet reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:CoverageReport" "-reporttypes:Html"` and open `CoverageReport/index.html`
- `TestResults/` and `CoverageReport/` are gitignored — regenerate locally rather than committing either
- `coverlet.runsettings` (repo root) excludes `TRPG.Data` from collection entirely — entity models, EF schema config, and migrations, none of which have branching logic worth chasing; its correctness is already proven by every other test failing if the schema/mappings were wrong. Always pass `--settings coverlet.runsettings`, or `TRPG.Data` noise (mostly generated migration `Up`/`Down` bodies) drowns out real gaps
- `scripts/coverage-gaps.py` parses a cobertura report and prints uncovered methods ranked by uncovered-line count, with compiler-generated async state machines folded back onto their real method and known-noise buckets (Program.cs, `*ServiceCollectionExtensions`, record/compiler-generated members) filtered out by default — read the script's own header comment for usage and exact filtering rules before assuming what it excludes

---

## C# Style

### General
- File-scoped namespaces (`namespace TRPG.Models;`)
- Primary constructors everywhere (`public class ReputationService(TrpgDbContext context)`)
- Expression-bodied members for simple one-liners
- Place related types (classes, records, enums) in the same file as the class they primarily support — no standalone `Enums.cs` or similar
- Use named parameters when constructing records or objects with multiple positional arguments of the same or similar types (e.g. `new StatAffinities(Strength: 3, Defense: 2, ...)` not `new StatAffinities(3, 2, ...)`)
- No alignment padding — do not add extra spaces to align `=`, `:`, or other tokens across lines
- Investigate the root cause of a bug before patching around it — treat the underlying issue, not just the symptom
- Prefer affirmative conditionals over negated ones (`if (combatant.IsAlive)` not `if (!combatant.IsDead)`) — double negatives are harder to read at a glance

### Comments
- Explain *why*, never *how* — well-named identifiers make the what and how obvious
- One line, maximum. If the justification needs more than that, fix the code (better name, extracted helper) instead of writing a paragraph
- Only when truly necessary — a future reader must be left genuinely confused without it. Default to no comment; most code needs zero
- Justify the code locally and stay context-free: no references to other files, past decisions, tickets, memory docs, or session history. A comment tied to an external fact goes stale the moment that fact changes; one that only depends on the adjacent line(s) can't
- No XML doc comments
- Never comment out code — delete it; git history has it if it's needed again
- No closing-brace comments (`} // end if`) — if a block is long enough to seem to need one, extract a named helper instead

### Naming
- `_camelCase` for private fields
- `PascalCase` for everything public
- No abbreviations — write `minimum`, `maximum`, `quantity`, `defense`, `index`, not `min`, `max`, `qty`, `def`, `idx`
- No tuple return types or tuple parameters — use a named `record` instead; tuples as local variables inside method bodies are fine
- Functions with more than 5 parameters must capture those parameters in a class instead (constructors excluded — DI constructors may have as many parameters as needed)
- Test classes: `{Subject}Tests`
- Test methods: `Method_ExpectedResult_WhenCondition`

### Classes
- Each class has a single responsibility — if describing it requires "and", split it into two classes
- No hard line-count ceiling — length by itself isn't the problem, mixed responsibilities are. A class creeping past a few hundred lines of actual logic is a prompt to re-check whether it's still doing one thing, not an automatic violation (e.g. `PlayerActionResolver` was split out of `CombatEngine` because validating player input is a different responsibility than simulating a round, not because of line count)
- Classes that are mostly static literal data (e.g. `CreatureGenerator`'s name-pool arrays) can run long without needing a split — the length reflects data volume, not complexity

### Functions
- Each function does one thing — if you need "and" to describe what it does, split it
- Prefer pure functions (static methods that depend only on their parameters and return a value with no side effects) where possible
- Keep functions under 40 lines; if a function exceeds this, extract helpers
- Avoid flag parameters that switch behavior (e.g. `bool verbose`) on new code — prefer two separate, differently-named methods over one method with a branching bool

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

### Migrations
- `Microsoft.EntityFrameworkCore.Design` is referenced by `TRPG.Data`, not `TRPG` (the host) — `--startup-project` must point at `TRPG.Data`, not `TRPG`, or the CLI fails with "doesn't reference Microsoft.EntityFrameworkCore.Design"
- Two `DbContext`s exist (`TrpgDbContext`, `TrpgTickerQDbContext`) — always pass `--context TrpgDbContext` explicitly or the CLI fails with "More than one DbContext was found"
- Full command to add a migration: `dotnet ef migrations add <Name> --project TRPG.Data --startup-project TRPG.Data --context TrpgDbContext`
- To apply migrations against a running Postgres instance: `dotnet ef database update --project TRPG.Data --startup-project TRPG.Data --context TrpgDbContext` (tests never need this — `DatabaseFixture` calls `MigrateAsync` itself against its Testcontainers instance)

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

### Query tracking
- Public **query methods** (read-only, never call `SaveChangesAsync`) use `.AsNoTracking()`
- **Command methods** (mutate + call `SaveChangesAsync` in the same method) use the default tracked query instead — fetch the entity, mutate its properties in place, `SaveChangesAsync()` picks up the change automatically; no explicit `.Update()` call needed
- When a write only needs to set a field by id/filter and doesn't otherwise need the entity, prefer `ExecuteUpdateAsync` (or `ExecuteDeleteAsync`) over fetch-then-mutate — it never touches the change tracker, so it can't conflict with anything else tracked in the same `DbContext`
- A command method never returns the tracked entity it mutated to its caller — its effect is observed either by the caller re-querying, or by the command method returning a plain value describing the result
- Don't mix the two within one method, and don't let a second no-tracking-fetched copy of an already-tracked row get `.Update()`-ed — EF throws "another instance with the same key value is already being tracked"
- Watch for cross-service ordering/identity assumptions when two methods touch the same row through separate queries on the same `DbContext` — don't rely on the identity map coincidentally handing back the same in-memory object; make any mutating call run before a dependent read, and don't hold a pre-fetched entity/list across a call to another method that independently re-fetches-mutates-saves that same row
- When a row needs both a cheap read accessor and a targeted write, give the write its own `ExecuteUpdateAsync` query rather than making the read accessor tracked to accommodate a fetch-then-mutate write

### Command input shape
- A command class takes scalar ids/values, not a full domain entity — the one exception is a pure creation command (`Add*Command`) that only ever calls `.Add()`, since there's no pre-existing tracked row for a brand-new entity to conflict with
- Never accept a full entity as a command property and call `.Update()` on it — the entity's tracking state depends entirely on how the caller happened to fetch it, and a second tracked copy of the same row anywhere else in the same `DbContext` throws "another instance with the same key value is already being tracked"; use `ExecuteUpdateAsync` with named `SetProperty` calls instead
- For a command that may only partially update a row, use `Optional<T>` (`TRPG.Application/Common/Optional.cs`) for any field that's already nullable in the domain — a plain `T?` can't distinguish "leave this alone" from "set it to null". A field that's never legitimately null (e.g. an enum status) can stay a plain `T?` on the command
- Build the `ExecuteUpdateAsync` call as a block-bodied lambda (EF Core 10+) and only call `s.SetProperty(...)` for fields the command actually set — `if (command.State != null) { s.SetProperty(c => c.State, command.State.Value); }`, `if (command.CityId.IsSet) { s.SetProperty(c => c.CityId, command.CityId.Value); }` — rather than the old pattern of unconditionally chaining every `SetProperty` and null-coalescing against the row's own current value (`c => command.State ?? c.State`); the old pattern always wrote every column, whether the caller set that field or not. `ExecuteUpdateAsync` throws if the lambda ends up calling `SetProperty` zero times, so guard the call (or return early) when every optional field on the command is unset
- A command that can act on more than one row takes a pluralized `IReadOnlyCollection<Guid> XIds` and applies the same field values to all of them in one `ExecuteUpdateAsync` call — matches the `GetXsByIdsQuery` batch-read naming convention rather than looping a singular command

### World scoping
- Every new entity/table must have a `WorldId` column with `HasIndex(x => x.WorldId)` — no FK constraint needed (matches other loose Guid references like `Job.PersonId`), just the indexed column
- This keeps `DropWorldCommandHandler` a flat `Where(x => x.WorldId == worldId).ExecuteDeleteAsync(...)` per table — never make it derive world membership by chasing a parent entity's FK chain again

---

## Services

### Structure
- Primary constructor injection: `public class ReputationService(TrpgDbContext context)`
- No interfaces — direct concrete classes
- Throw `InvalidOperationException` for business rule violations
- Exception: a pure resolver/validator whose caller needs to turn failure into user-facing output without exception-driven control flow (e.g. a SignalR-streamed response) can return a small Result-style object instead of throwing — see `PlayerCombatActionResolver`'s `PlayerCombatActionResolverResult` (`Result`/`ErrorMessage`/`IsError`). Reserve this for that specific shape of caller, not general command/query handlers
- No pre-checks for uniqueness — rely on DB constraints

### Patterns
- `FindAsync([id], cancellationToken)` for PK lookups
- `ExecuteDeleteAsync` for hard deletes
- `FirstOrDefaultAsync` + null check for lookups with side effects

---

## Ollama Model Benchmarking

- `scripts/benchmark-model.sh` and `scripts/benchmark-models.sh` measure move-command reliability and latency for a given Ollama model + thinking-mode combination, run against the server's real HTTP endpoints (`POST /sessions?worldId={id}` then `POST /admin/sessions/{id}/chat`) — built to replace manual edit-source-and-rebuild A/B testing
- Full usage, argument syntax, and design rationale (why the server restarts per trial, why there's an untimed warmup, why there's no capability auto-detection) live in each script's own header comment — read that first, don't rely on this note for exact syntax
- Some models crash the server if asked to think when they don't support it (e.g. `mistral-nemo:12b`) — there's no auto-detection, the caller has to know which models can take `@true`/`@both`
- Results accumulate across every invocation in `scripts/benchmark-results.csv` (gitignored — local experiment output, not committed), grouped by a `tag` column
- Readiness polling uses `curl` against a real endpoint, not `Get-NetTCPConnection`/`Get-Process` — repeated `powershell.exe` spawns alongside a backgrounded process have been observed to stall badly in some shells

---

## Frontend Tests

- Use Vitest, React Testing Library, `user-event`, MSW, and the generated Hey API MSW handlers for frontend tests
- Render components through `TRPG.Web/src/test/test-utils.tsx`; it provides a fresh `QueryClient`, the shared providers, and an isolated `userEvent` instance per test
- Configure test query clients with retries disabled so failed queries and mutations fail promptly instead of waiting through exponential backoff
- Prefer accessible queries (`getByRole`, `getByLabelText`, and accessible names) over DOM order, CSS selectors, or implementation details
- Add an `aria-label` or other accessible name to a control when that makes it meaningfully testable and improves the component's accessibility; testability is a valid reason to add one
- When multiple controls have the same visible label, give the intended control a distinct accessible name; do not disambiguate with `getAll()[0]` or DOM order
- Prefer real components backed by generated MSW handlers over mocking child components or hooks just to avoid configuring their API requests; reserve mocks for true external boundaries or dependencies outside the behavior being tested
- Assert user-visible behavior and network contracts at the MSW boundary, not internal component state

## Integration Tests

### When to skip the database entirely
- Before writing an HTTP+Postgres test, check whether the logic under test is actually pure/in-memory (a generator method, a scheduling calculation, a pure in-memory registry, etc.) — if so, unit-test it directly instead: construct the real class with its real (non-LLM, non-DB) dependencies and assert on its return value, matching `WorldGeneratorEmploymentTests`/`WorldGeneratorHouseholdTests`/`WorldConnectionRegistryTests`. Reserve the full HTTP+Postgres harness for things that genuinely need it — persistence behavior, cross-service wiring, LLM-backed generation steps
- Never write a temporary/throwaway test just to verify something works and then delete it — if the check is worth writing, it's worth keeping as a permanent test

### Infrastructure
- `DatabaseFixture` spins up `postgres:17` via Testcontainers, runs `MigrateAsync` once
- All test classes share the container via `[Collection("Database")]`
- xUnit creates a new class instance per test — `IAsyncLifetime` handles per-test setup/teardown
- `InitializeAsync` creates a fresh `TrpgDbContext` and seeds shared state
- `DisposeAsync` disposes the context: `public async ValueTask DisposeAsync() => await _context.DisposeAsync();`

### Test class structure
```csharp
[Collection("Database")]
public sealed class FooServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private FooService _service = null!;
    private readonly SomeEntity _entity = Builders.MakeSomeEntity(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new FooService(_context);

        _context.SomeEntities.Add(_entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();
}
```

### Constructing the handler under test
- Default to DI-resolving the handler(s) under test via `AddTrpgTestServices(_context)` (`TRPG.Tests/Helpers/TestServiceCollectionExtensions.cs`) rather than manually nesting `new Handler(new OtherHandler(...), ...)` — even when the handler only has one or two dependencies. The cost is near zero (a missing registration fails loudly at `GetRequiredService` time) and it means a handler gaining a new constructor dependency later never breaks this test's compilation
  ```csharp
  _serviceProvider = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
  _handler = _serviceProvider.GetRequiredService<FooCommandHandler>();
  ```
- `AddTrpgTestServices` wraps the production `AddTrpgApplicationServices()` registration, then adds the test's own already-constructed `TrpgDbContext` as a singleton **instance** (not a factory) — the container doesn't dispose an instance registered this way, so there's no double-dispose against the test's own `DisposeAsync`, and every resolved handler shares the exact same context/change-tracker the test seeded through
- It also registers two open-generic fallbacks so most tests need zero extra setup: `ILogger<T>` → `NullLogger<T>`, and `IOptionsSnapshot<T>` → `DefaultOptionsSnapshot<T>` (default-constructs `T`; both types live in `TestOptionsSnapshot.cs`). A test whose assertions depend on a *specific* non-default options value (e.g. forcing guaranteed hits via `CombatOptions`) chains its own `.AddSingleton<IOptionsSnapshot<T>>(new TestOptionsSnapshot<T>(...))` after `AddTrpgTestServices(...)` — later registrations win over the open-generic default
- Add a `ServiceProvider _serviceProvider` field and dispose it in `DisposeAsync`, alongside `_context`

### Seeding strategy
- Promote entities to class fields when every (or nearly every) test needs them
- A scalar seed value (a `Guid.NewGuid()` id shared across the class) is `private static readonly`, PascalCase, initialized inline — it isn't per-instance mutable state, so it doesn't get the `_camelCase` treatment
- An entity built via `Builders.MakeX(...)` that needs no DB access to construct is a `private readonly` instance field with an inline initializer, not `null!` assigned later in `InitializeAsync`
- `InitializeAsync` is reserved for genuinely async, DB-touching work only: constructing handlers that depend on `_context`, and persisting the already-constructed seed entities. If a field's value can be computed synchronously, it doesn't belong in `InitializeAsync`
- Add `private async Task<T> Seed*(...)` helper methods for entities needed by only a subset of tests, or for entities with test-class-specific shape (e.g. seeding a join-row with this class's own `WorldId`) — these stay local, they're not identical across test classes. Never add a `Seed*`-style wrapper for something only one call site needs — inline it there instead
- Seed helpers add to context, save, and return the entity
- Seed helpers return a single entity — never a tuple; use separate helpers if a test needs multiple seeded entities

### Persisting seeded entities
- `Builders` is the only place that builds entities; persistence is always a plain `context.Xs.Add(entity)` / `.AddRange(...)` followed by exactly one `await context.SaveChangesAsync(cancellationToken)` for that whole `InitializeAsync` method or that whole test's Arrange section — regardless of how many entity types are involved
- There is no shared "add-and-save" extension helper (a `TrpgDbContextExtensions` along those lines was tried and removed) — one auto-saving call per entity type means one Postgres round-trip per call, and mixing an auto-saving helper with plain `.Add()` calls for other types in the same setup step leaves some rows committed before others, which is exactly the partial-commit risk a single shared save avoids
- This applies uniformly whether the entities being added are all the same type or a mix (e.g. a `Country` + `State` + `City` seeded together for one test) — build every entity first with `Builders`, `.Add()`/`.AddRange()` each into its DbSet, then one `SaveChangesAsync`

### Builders
- Named `Make{Entity}`: `Builders.MakePerson()`, `Builders.MakeItem()`, `Builders.MakeSkill()`, `Builders.MakeQuest(giverId)`
- Fields with unique DB constraints use Guid suffix: `$"Item-{Guid.NewGuid():N}"`
- Optional parameters for FK overrides: `MakePerson(worldId: ...)`
- `Person.Name` has no unique constraint so a static string is fine
- If a test needs a builder-made entity with field values the builder doesn't expose yet, add an optional parameter to the builder (e.g. `MakeCreature(level: 7, baseAttributes: ...)`) rather than writing a local `MakeSeedX()` wrapper that constructs-then-mutates in the test file — the builder is the one place entity construction lives
- This applies to any builder-style factory method, not just `Builders` itself — a test file's own local `MakeX(...)` helper (e.g. a `MakeCombatant` in a single test class) follows the same rule: never call it and then mutate the result (`var c = MakeCombatant(...); c.CurrentHp = 1;`) — add the field as an optional parameter (`MakeCombatant(currentHp: 1)`) instead
- If a builder-style factory method is copy-pasted near-identically across multiple test files (a strong sign each file independently hit the "too many optional parameters" wall above), consolidate it into one shared fluent builder in `TRPG.Tests/Helpers` instead of leaving N slightly-diverged local copies — e.g. `CombatantBuilder` (`Builders.NewCombatant().WithName("Hero").AsPlayer().WithDexterity(20).WithAbilities(strike).Build()`) replaced four separate local `MakeCombatant(...)` helpers that had each grown their own parameter list for `CombatEngineTests`/`HitCalculatorTests`/`DamageCalculatorTests`/`PlayerCombatActionResolverTests`. A local one-line helper that just pre-seeds shared fields on the builder (e.g. `MakeCombatant(name) => Builders.NewCombatant().WithWorldId(_worldId).WithName(name)`) is fine — it isn't reconstructing the entity, just saving repetition of values every call site in that file needs anyway

### AAA sections
- Every test has `// Arrange`, `// Act`, `// Assert` comments
- Exception-throwing tests use `// Act & Assert`
- The Act section is exactly one statement — the single call under test. If getting there needs more than one line (e.g. awaiting the call, or a multi-line argument list), that's still one statement; never sequence two separate calls in Act, and never wrap it in a helper method either, even a same-shaped one repeated byte-for-byte across every `[Fact]` in the file — the call under test stays inline and visible, full stop
- Arrange and Act are as small as possible — a reader should see at a glance what's being exercised without wading through setup. Push anything not essential to that one test into `Builders`, shared fields, or `InitializeAsync`
- When Arrange has genuine ceremony repeated across multiple tests — a command-handler dispatch wrapped around a builder call (locking a door, adding a job, giving an item, always-empty fields on a larger record) — extract a `Seed*`/verb-named private helper for it, same as the `Seed*` convention below. But when the repeated-looking code is actually each test's essential, varying setup (which stats, which room, which ability combination, which conditions), leave it inline — extracting it hides the point of the test rather than clarifying it. The test is "is this the same mechanical ceremony every time" vs. "is this what makes each test different"
- If a test seems to need two calls in Act, figure out which shape it actually is before fixing it:
  - If the first call is only there to establish pre-existing state (e.g. casting a buff so a second cast can be checked for stacking vs. refreshing), move that first call into Arrange and leave the second as the sole Act statement
  - If the two calls are independent, comparable scenarios bundled into one test (e.g. generating monsters for two different dungeon themes, or checking entry with two different valid keys), split into two separate `[Fact]`s instead — each gets its own one-statement Act and its own name
  - Exception: a test whose entire point is verifying behavior across a multi-step process (buff decay over several combat rounds, a full creation-through-combat lifecycle) legitimately needs multiple actions with interleaved assertions — don't force these into a single Act statement, they're testing a sequence by design
- A duplicate multi-assertion block (2+ `Assert` calls, byte-for-byte identical) repeated across different `[Fact]`s in the same file is a signal to consider collapsing them into a `[Theory]` — but a single-field assert repeated across Facts is fine (each Fact is proving a different code path reaches the same success shape, not duplicating logic)
- Omit empty sections rather than writing a comment with nothing under it

### Verifying deletes
- Open a second context to verify deletion — the original context change tracker still holds the entity

### Unique name collisions
- Tests share one Postgres container with no rollback between tests
- Any entity with a unique name constraint must use a Guid-suffixed name in builders and seed helpers

### Hub tests
- `ChatHubTests` invokes SignalR hub methods through `HubConnection.StreamAsync<string>(...)`
- The two connection-lifecycle tests (`Connect_Succeeds_*`) are the exception — they assert on `HubConnectionState`/`StartAsync` directly against the raw `HubConnection`, since they're testing the connection itself, not a hub method call

### Endpoint tests
- The one deliberate exception to "no mocking": HTTP endpoint tests (`WorldEndpointsTests`, `GameSessionEndpointsTests`) mock the LLM client, because a real LLM provider is external, non-deterministic, and slow — unlike Postgres, it can't be spun up reliably via Testcontainers, and real narration text isn't what these tests are checking
- `EndpointTestFixture` wraps `WebApplicationFactory<Program>` in its own `[Collection("Endpoints")]` (a separate Postgres container from the `"Database"` collection), and swaps in `FakeChatClient` (`TRPG.Tests.Helpers`) for both the `"WorldGeneration"` and `"Gameplay"` keyed `IChatClient` registrations — the same fake instance backs both roles, matching how production only differs in which concrete `IChatClient` (Ollama or Anthropic) gets constructed for each key
- `FakeChatClient` detects which of the world-generation schemas is being requested (factions/cities/geography-entity) from keywords in the combined message text, then constructs and serializes **the real production schema types** (`FactionListSchema`, `GeographyEntitySchema`, etc., widened from `file class` to `internal class` in their generator files specifically so tests can reference them) — never loosely-typed anonymous objects, so a breaking change to those schemas forces the test to be updated rather than silently drifting; anything that doesn't match a world-gen keyword falls back to a plain canned chat response
- Tests that exercise world generation (`CreateWorld_...`) pass a `CreateWorldRequest` with every count knob set to 1 (or 0 for optional entities) to keep the fake's canned responses trivial
- Corner-case tests (404s for an unknown session id, 400 for invalid `/wait` input) use the same fixture — those code paths never reach the LLM client, so no special handling is needed
- Seed data for session tests goes straight through a `TrpgDbContext` pulled from `fixture.CreateScope()`, the same way non-HTTP integration tests seed via `DatabaseFixture.CreateContext()`
