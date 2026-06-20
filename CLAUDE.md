# TRPG Codebase Conventions

## Language & Framework

- C# .NET 9, EF Core 9.0.4, Npgsql 9.0.4
- PostgreSQL via Testcontainers in tests
- xUnit for integration tests — no unit tests, no mocking
- No ASP.NET Core — local single-player game

---

## C# Style

### General
- File-scoped namespaces (`namespace TRPG.Models;`)
- Primary constructors everywhere (`public class PersonService(TrpgDbContext context)`)
- Expression-bodied members for simple one-liners
- No comments except where non-obvious; no XML doc comments

### Naming
- `_camelCase` for private fields
- `PascalCase` for everything public
- Test classes: `{Subject}Tests`
- Test methods: `Method_ExpectedResult_WhenCondition`

### Null handling
- `null!` for fields initialized in `InitializeAsync` (not inline)
- Nullable reference types enabled; use `?` where genuinely optional

### Collections
- `List<T>` (not `IList`, `ICollection`, `IEnumerable`) for EF array columns and return types
- Collection expressions `[]` for empty lists, `[x, y]` for inline initialization

### Async
- No `Async` suffix on service methods
- `CancellationToken cancellationToken = default` on every public service method
- `await using` for disposable contexts

---

## Models

### Immutability
- `init` on fields that should not change after construction: `Id`, `Name`, `RaceId`, `BirthCityId`, etc.
- `set` on mutable fields: `Gold`, `Biography`, `Location`, `Progression`, `Attributes`
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
- Composite index `(WorldId, CityId, BuildingId)` on any flattened `Location`

### Enum storage
- All enums stored as `string` via `HaveConversion<string>()` in `ConfigureConventions`

### MaxAsync on nullable
- Use `MaxAsync(x => (int?)x.Field) ?? -1` — `DefaultIfEmpty(-1).MaxAsync()` cannot be translated by EF Core

### Unique constraints
- Let EF Core throw `DbUpdateException` on violations — no pre-check queries

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
