# List available recipes
default:
    @just --list

# Restore local dotnet tools and NuGet packages
restore:
    dotnet tool restore
    dotnet restore DotFuzz.slnx

# Format all C# sources with CSharpier
format:
    dotnet csharpier format .

# Verify formatting without changing files (CI-friendly)
format-check:
    dotnet csharpier check .

# Build the full solution
build:
    dotnet build DotFuzz.slnx

# Run the test suite
test:
    dotnet test tests/DotFuzz.Tests/DotFuzz.Tests.csproj

# Run benchmarks (pass a filter, e.g. `just bench *Ratio*`)
bench filter='*':
    dotnet run --project benchmarks/DotFuzz.Benchmarks -c Release -- --filter "{{filter}}"

# Remove build artifacts
clean:
    dotnet clean DotFuzz.slnx
