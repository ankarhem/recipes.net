This file is the agent's persisted memory. Keep it concise — small, important instructions that prevent repeated mistakes. Improve it proactively when you learn something worth remembering. Does not require user approval — edit it right away and mention that you did it.

Always run `just validate` before committing.

## Namespace collisions
`Recipe` is used as both a namespace (`Domain.Recipe`, `App.Recipe`, `Infrastructure.Recipe`) and a type name (`Domain.Recipe.Recipe`, `Schema.NET.Recipe`). Files inside `*.Recipe` namespaces must use aliases: `DomainRecipe`, `SchemaRecipe`, `DomainRecipeIngredient`, `DomainRecipeInstruction`.

## Controller → Service → Repository
Controllers depend on App-layer services (`IRecipeService`), never repositories. Services live in App and delegate to `IRecipeRepository`. Repository interfaces return domain types (`DomainRecipe?`), not DTOs or entity types.

## Entity design for child collections
Queryable data (ingredients, instructions) gets its own table with FK. Pure value objects that are always fetched together (image URLs) stay as jsonb on the parent entity. Never deserialize Schema.NET JSON-LD on reads — denormalize at save time into proper columns/tables.

## NSubstitute with Task<T> returns
`Substitute.For<ISomeInterface>()` where methods return `Task<T>` causes ambiguous `Returns` / `ReturnsForAnyArgs` calls. Use `default!` for args and explicit `Func<CallInfo, T>` cast for lambda overloads.

## Temporal workflow testing
For workflow tests that include `Workflow.DelayAsync`, keep workflow code untouched and use `WorkflowEnvironment.StartTimeSkippingAsync(...)` in tests to avoid real-time waiting.
