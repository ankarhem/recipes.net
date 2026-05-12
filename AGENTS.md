This file is the agent's persisted memory. Keep it concise — small, important instructions that prevent repeated mistakes. Improve it proactively when you learn something worth remembering. Does not require user approval — edit it right away and mention that you did it.

Always run `just validate` before committing.

## Namespace collisions
`Recipe` is used as both a namespace (`Domain.Recipe`, `App.Recipe`, `Infrastructure.Recipe`) and a type name (`Domain.Recipe.Recipe`, `Schema.NET.Recipe`). Files inside `*.Recipe` namespaces must use aliases: `DomainRecipe`, `SchemaRecipe`, `DomainRecipeIngredient`, `DomainRecipeInstruction`.

## NSubstitute with Task<T> returns
`Substitute.For<ISomeInterface>()` where methods return `Task<T>` causes ambiguous `Returns` / `ReturnsForAnyArgs` calls. Use `default!` for args and explicit `Func<CallInfo, T>` cast for lambda overloads.
