This file is the agent's persisted memory. Keep it concise — small, important instructions that prevent repeated mistakes. Improve it proactively when you learn something worth remembering. Does not require user approval — edit it right away and mention that you did it.

Always run `just validate` before committing.

The app is not deployed and is under active developement.

## Controller → Service → Repository
Controllers depend on App-layer services (`IRecipeService`), never repositories. Services live in App and delegate to `IRecipeRepository`. Repository interfaces return domain types (`Recipe?`), not DTOs or entity types.

## Bounded contexts
`Identity` (User aggregate + UserSession aggregate + tokens), `Recipes` (Recipe aggregate + RecipeFavorite + embeddings), `Crawler`, `Embedding`. Each context spans Domain / App / Infrastructure layers with matching namespaces. Cross-context dependencies go through interfaces, not direct type references.

## Aggregate roots own behavior
Behavior (validation, state transitions, invariants) lives on the aggregate, not on the service. Repositories load and save aggregates only; they do not expose child-table CRUD. App-layer services hash inputs, load the aggregate, call its behavior, and `SaveChanges`. Optimistic concurrency conflicts surface as `ConcurrencyConflictException` and translate to `AuthResult.Invalid*` variants.

## Entity design for child collections
Queryable data (ingredients, instructions) gets its own table with FK. Pure value objects that are always fetched together (image URLs) stay as jsonb on the parent entity. Never deserialize Schema.NET JSON-LD on reads — denormalize at save time into proper columns/tables.

## NSubstitute with Task<T> returns
`Substitute.For<ISomeInterface>()` where methods return `Task<T>` causes ambiguous `Returns` / `ReturnsForAnyArgs` calls. Use `default!` for args and explicit `Func<CallInfo, T>` cast for lambda overloads.

## Temporal workflow testing
For workflow tests that include `Workflow.DelayAsync`, keep workflow code untouched and use `WorkflowEnvironment.StartTimeSkippingAsync(...)` in tests to avoid real-time waiting.
