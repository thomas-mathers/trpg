# Integration Test Conventions

xUnit + Testcontainers PostgreSQL. Read this before writing or reviewing any test file in this project.

## When to skip the database entirely
- Before writing an HTTP+Postgres test, check whether the logic under test is actually pure/in-memory (a generator method, a scheduling calculation, a pure in-memory registry, etc.) — if so, unit-test it directly instead: construct the real class with its real (non-LLM, non-DB) dependencies and assert on its return value, matching `WorldGeneratorEmploymentTests`/`WorldGeneratorHouseholdTests`/`WorldConnectionRegistryTests`. Reserve the full HTTP+Postgres harness for things that genuinely need it — persistence behavior, cross-service wiring, LLM-backed generation steps
- Never write a temporary/throwaway test just to verify something works and then delete it — if the check is worth writing, it's worth keeping as a permanent test

## Naming
- Test classes: `{Subject}Tests`
- Test methods: `Method_ExpectedResult_WhenCondition`

## Infrastructure
- `PostgreSqlFixture` is an assembly fixture: it starts one `postgres:17` container and migrates a template database once
- Database test classes use `IClassFixture<DatabaseFixture>`; each class clones its own database from the template and drops it on disposal. Up to eight classes run in parallel; tests within a class remain sequential.
- xUnit creates a new class instance per test — `IAsyncLifetime` handles per-test setup/teardown
- `InitializeAsync` creates a fresh `TrpgDbContext` and seeds shared state
- `DisposeAsync` disposes the context: `public async ValueTask DisposeAsync() => await _context.DisposeAsync();`

## Test class structure
```csharp
public sealed class FooServiceTests(DatabaseFixture db) : IAsyncLifetime, IClassFixture<DatabaseFixture>
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

## Constructing the handler under test
- Default to DI-resolving the handler(s) under test via `AddTrpgTestServices(_context)` (`TRPG.Tests/Helpers/TestServiceCollectionExtensions.cs`) rather than manually nesting `new Handler(new OtherHandler(...), ...)` — even when the handler only has one or two dependencies. The cost is near zero (a missing registration fails loudly at `GetRequiredService` time) and it means a handler gaining a new constructor dependency later never breaks this test's compilation
  ```csharp
  _serviceProvider = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
  _handler = _serviceProvider.GetRequiredService<FooCommandHandler>();
  ```
- `AddTrpgTestServices` caches the production `AddTrpgApplicationServices()` registration descriptors once, copies them into each test service collection, then adds the test's own already-constructed `TrpgDbContext` as a singleton **instance** (not a factory) — the container doesn't dispose an instance registered this way, so there's no double-dispose against the test's own `DisposeAsync`, and every resolved handler shares the exact same context/change-tracker the test seeded through
- It also registers two open-generic fallbacks so most tests need zero extra setup: `ILogger<T>` → `NullLogger<T>`, and `IOptionsSnapshot<T>` → `DefaultOptionsSnapshot<T>` (default-constructs `T`; both types live in `TestOptionsSnapshot.cs`). A test whose assertions depend on a *specific* non-default options value (e.g. forcing guaranteed hits via `CombatOptions`) chains its own `.AddSingleton<IOptionsSnapshot<T>>(new TestOptionsSnapshot<T>(...))` after `AddTrpgTestServices(...)` — later registrations win over the open-generic default
- Add a `ServiceProvider _serviceProvider` field and dispose it in `DisposeAsync`, alongside `_context`

## Seeding strategy
- Promote entities to class fields when every (or nearly every) test needs them
- A scalar seed value (a `Guid.NewGuid()` id shared across the class) is `private static readonly`, PascalCase, initialized inline — it isn't per-instance mutable state, so it doesn't get the `_camelCase` treatment
- An entity built via `Builders.MakeX(...)` that needs no DB access to construct is a `private readonly` instance field with an inline initializer, not `null!` assigned later in `InitializeAsync`
- `InitializeAsync` is reserved for genuinely async, DB-touching work only: constructing handlers that depend on `_context`, and persisting the already-constructed seed entities. If a field's value can be computed synchronously, it doesn't belong in `InitializeAsync`
- Add `private async Task<T> Seed*(...)` helper methods for entities needed by only a subset of tests, or for entities with test-class-specific shape (e.g. seeding a join-row with this class's own `WorldId`) — these stay local, they're not identical across test classes. Never add a `Seed*`-style wrapper for something only one call site needs — inline it there instead
- Seed helpers add to context, save, and return the entity
- Seed helpers return a single entity — never a tuple; use separate helpers if a test needs multiple seeded entities

## Persisting seeded entities
- `Builders` is the only place that builds entities; persistence is always a plain `context.Xs.Add(entity)` / `.AddRange(...)` followed by exactly one `await context.SaveChangesAsync(cancellationToken)` for that whole `InitializeAsync` method or that whole test's Arrange section — regardless of how many entity types are involved
- There is no shared "add-and-save" extension helper (a `TrpgDbContextExtensions` along those lines was tried and removed) — one auto-saving call per entity type means one Postgres round-trip per call, and mixing an auto-saving helper with plain `.Add()` calls for other types in the same setup step leaves some rows committed before others, which is exactly the partial-commit risk a single shared save avoids
- This applies uniformly whether the entities being added are all the same type or a mix (e.g. a `Country` + `State` + `City` seeded together for one test) — build every entity first with `Builders`, `.Add()`/`.AddRange()` each into its DbSet, then one `SaveChangesAsync`

## Builders
- Named `Make{Entity}`: `Builders.MakePerson()`, `Builders.MakeItem()`, `Builders.MakeSkill()`, `Builders.MakeQuest(giverId)`
- Fields with unique DB constraints use Guid suffix: `$"Item-{Guid.NewGuid():N}"`
- Optional parameters for FK overrides: `MakePerson(worldId: ...)`
- `Person.Name` has no unique constraint so a static string is fine
- If a test needs a builder-made entity with field values the builder doesn't expose yet, add an optional parameter to the builder (e.g. `MakeCreature(level: 7, baseAttributes: ...)`) rather than writing a local `MakeSeedX()` wrapper that constructs-then-mutates in the test file — the builder is the one place entity construction lives
- This applies to any builder-style factory method, not just `Builders` itself — a test file's own local `MakeX(...)` helper (e.g. a `MakeCombatant` in a single test class) follows the same rule: never call it and then mutate the result (`var c = MakeCombatant(...); c.CurrentHp = 1;`) — add the field as an optional parameter (`MakeCombatant(currentHp: 1)`) instead
- If a builder-style factory method is copy-pasted near-identically across multiple test files (a strong sign each file independently hit the "too many optional parameters" wall above), consolidate it into one shared fluent builder in `TRPG.Tests/Helpers` instead of leaving N slightly-diverged local copies — e.g. `CombatantBuilder` (`Builders.NewCombatant().WithName("Hero").AsPlayer().WithDexterity(20).WithAbilities(strike).Build()`) replaced four separate local `MakeCombatant(...)` helpers that had each grown their own parameter list for `CombatEngineTests`/`HitCalculatorTests`/`DamageCalculatorTests`/`PlayerCombatActionResolverTests`. A local one-line helper that just pre-seeds shared fields on the builder (e.g. `MakeCombatant(name) => Builders.NewCombatant().WithWorldId(_worldId).WithName(name)`) is fine — it isn't reconstructing the entity, just saving repetition of values every call site in that file needs anyway

## AAA sections
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

## Verifying deletes
- Open a second context to verify deletion — the original context change tracker still holds the entity

## Unique name collisions
- Tests within a class share their database with no rollback between tests; different classes have isolated databases
- Any entity with a unique name constraint must use a Guid-suffixed name in builders and seed helpers

## Hub tests
- `ChatHubTests` invokes SignalR hub methods through `HubConnection.StreamAsync<string>(...)`
- The two connection-lifecycle tests (`Connect_Succeeds_*`) are the exception — they assert on `HubConnectionState`/`StartAsync` directly against the raw `HubConnection`, since they're testing the connection itself, not a hub method call

## Endpoint tests
- The one deliberate exception to "no mocking": HTTP endpoint tests (`WorldEndpointsTests`, `GameSessionEndpointsTests`) mock the LLM client, because a real LLM provider is external, non-deterministic, and slow — unlike Postgres, it can't be spun up reliably via Testcontainers, and real narration text isn't what these tests are checking
- `EndpointTestFixture` wraps `WebApplicationFactory<Program>` in its own `[Collection("Endpoints")]` (its own database in the assembly container; endpoint classes remain sequential because they share the mutable fake and host configuration), and swaps in `FakeChatClient` (`TRPG.Tests.Helpers`) for both the `"WorldGeneration"` and `"Gameplay"` keyed `IChatClient` registrations — the same fake instance backs both roles, matching how production only differs in which concrete `IChatClient` (Ollama or Anthropic) gets constructed for each key
- `FakeChatClient` detects which of the world-generation schemas is being requested (factions/cities/geography-entity) from keywords in the combined message text, then constructs and serializes **the real production schema types** (`FactionListSchema`, `GeographyEntitySchema`, etc., widened from `file class` to `internal class` in their generator files specifically so tests can reference them) — never loosely-typed anonymous objects, so a breaking change to those schemas forces the test to be updated rather than silently drifting; anything that doesn't match a world-gen keyword falls back to a plain canned chat response
- Tests that exercise world generation (`CreateWorld_...`) pass a `CreateWorldRequest` with every count knob set to 1 (or 0 for optional entities) to keep the fake's canned responses trivial
- Corner-case tests (404s for an unknown session id, 400 for invalid `/wait` input) use the same fixture — those code paths never reach the LLM client, so no special handling is needed
- Seed data for session tests goes straight through a `TrpgDbContext` pulled from `fixture.CreateScope()`, the same way non-HTTP integration tests seed via `DatabaseFixture.CreateContext()`

## Running Tests

- Use `scripts/test.sh` for routine test runs instead of raw `dotnet test` — it wraps the full solution invocation and filters noisy per-test output down to failures and the summary, which matters because that output goes into an LLM's context. Pass extra args through as normal, e.g. `scripts/test.sh --filter-class "*SomeTests"`
- `scripts/test.sh` must run with its working directory inside `api/` for Microsoft.Testing.Platform mode to activate (it finds `api/global.json` there) — it already `cd`s there itself, so don't invoke `dotnet test api/TRPG.sln` from the repo root instead, that silently falls back to the legacy VSTest path
- The coverage command below intentionally bypasses `test.sh` — coverage collection needs the full test run, not a failures-only view

## Code Coverage

- Local only, not wired into CI. `coverlet.collector` is already a `TRPG.Tests` package reference; `reportgenerator` is pinned in `.config/dotnet-tools.json` alongside CSharpier
- Generate: `dotnet test api/TRPG.sln --collect:"XPlat Code Coverage" --settings api/coverlet.runsettings --results-directory ./TestResults`, then `dotnet reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:CoverageReport" "-reporttypes:Html"` and open `CoverageReport/index.html`
- `TestResults/` and `CoverageReport/` are gitignored — regenerate locally rather than committing either
- `api/coverlet.runsettings` excludes `TRPG.Data` and `TRPG.Domain` from collection entirely — entity models, EF schema configuration, and migrations have no branching logic worth chasing; their correctness is already proven by every other test failing if the schema/mappings were wrong. Always pass `--settings api/coverlet.runsettings`, or generated migration `Up`/`Down` noise drowns out real gaps
- `scripts/coverage-gaps.py` parses a cobertura report and prints uncovered methods ranked by uncovered-line count, with compiler-generated async state machines folded back onto their real method and known-noise buckets (Program.cs, `*ServiceCollectionExtensions`, record/compiler-generated members) filtered out by default — read the script's own header comment for usage and exact filtering rules before assuming what it excludes
