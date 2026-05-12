using SchemaRecipe = Schema.NET.Recipe;

namespace App.Crawler;

public sealed record ExtractedRecipe(SchemaRecipe SchemaRecipe, string RawJsonLd);
