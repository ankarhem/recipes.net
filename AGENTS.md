This file is the agent's persisted memory. Keep it concise — small, important instructions that prevent repeated mistakes. Improve it proactively when you learn something worth remembering. Does not require user approval — edit it right away and mention that you did it.

Always run `just validate` before committing.

The app is not deployed and is under active developement.

## Aggregate roots own behavior
Behavior (validation, state transitions, invariants) lives on the aggregate, not on the service. Repositories load and save aggregates only; they do not expose child-table CRUD. App-layer services hash inputs, load the aggregate, call its behavior, and `SaveChanges`. Optimistic concurrency conflicts surface as `ConcurrencyConflictException`.

## EF Core persistence: domain types directly
Domain aggregates are persisted directly via EF Core — no separate infrastructure entity types. Each domain type has an `IEntityTypeConfiguration<T>` in `Infrastructure/*/Configurations/`. Value objects use `HasConversion()`, child collections use `OwnsMany` with `PropertyAccessMode.Field` on backing fields. Infrastructure-only columns (source Url, raw JsonLd, timestamps on Recipe) are shadow properties. `RecipesDbContext.OnModelCreating` is a single `ApplyConfigurationsFromAssembly()` call.

## Entity design for child collections
Queryable data (ingredients, instructions) gets its own table with FK. Pure value objects that are always fetched together (image URLs) stay as jsonb on the parent entity. Never deserialize Schema.NET JSON-LD on reads — denormalize at save time into proper columns/tables.

## Temporal workflow testing
For workflow tests that include `Workflow.DelayAsync`, keep workflow code untouched and use `WorkflowEnvironment.StartTimeSkippingAsync(...)` in tests to avoid real-time waiting.
