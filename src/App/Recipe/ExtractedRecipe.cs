using SchemaRecipe = Schema.NET.Recipe;

namespace App.Recipe;

public sealed record ExtractedRecipe(SchemaRecipe SchemaRecipe, string RawJsonLd);
