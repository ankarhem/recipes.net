set dotenv-load

web_project := "src/Web/Web.csproj"
infra_project := "src/Infrastructure/Infrastructure.csproj"

# List all commands
default:
    @just --list

# Run the web app in development mode
dev:
    dotnet run --project {{web_project}}

# Run all tests
test:
    dotnet test

# Run only unit tests
test-unit:
    dotnet test tests/Web.UnitTests/Web.UnitTests.csproj

# Create a new EF Core migration (pass name as argument)
migrate name:
    dotnet ef migrations add {{name}} --project {{infra_project}} --startup-project {{infra_project}} --output-dir Migrations

# Build the entire solution
build:
    dotnet build

# Watch the web app with hot reload
watch:
    dotnet watch --project {{web_project}}

# Start Temporal dev server
temporal:
    temporal server start-dev
