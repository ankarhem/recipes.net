using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeFilteringFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Recipes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CookTime",
                table: "Recipes",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cuisine",
                table: "Recipes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PrepTime",
                table: "Recipes",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServingsCount",
                table: "Recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SuitableForDiets",
                table: "Recipes",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "TotalTime",
                table: "Recipes",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Category", table: "Recipes");
            migrationBuilder.DropColumn(name: "CookTime", table: "Recipes");
            migrationBuilder.DropColumn(name: "Cuisine", table: "Recipes");
            migrationBuilder.DropColumn(name: "PrepTime", table: "Recipes");
            migrationBuilder.DropColumn(name: "ServingsCount", table: "Recipes");
            migrationBuilder.DropColumn(name: "SuitableForDiets", table: "Recipes");
            migrationBuilder.DropColumn(name: "TotalTime", table: "Recipes");
        }
    }
}
