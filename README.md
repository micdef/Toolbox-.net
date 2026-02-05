# Toolbox

A .NET 10 library providing base services, telemetry, and dependency injection extensions.

## Features

- **Disposable Services**: Thread-safe base classes for synchronous and asynchronous disposal
- **OpenTelemetry Integration**: Built-in distributed tracing and metrics collection
- **Dependency Injection**: Fluent extensions for easy service registration
- **Comprehensive Documentation**: Full Doxygen documentation support

## Requirements

- .NET 10.0 or later
- C# 14.0

## Installation

Add the project reference to your solution:

```xml
<ProjectReference Include="..\src\Toolbox.Core\Toolbox.Core.csproj" />
```

## Quick Start

```csharp
using Toolbox.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Add Toolbox core services
builder.Services.AddToolboxCore(options =>
{
    options.EnableDetailedTelemetry = true;
});

// Add OpenTelemetry integration
builder.Services.AddToolboxOpenTelemetry(options =>
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.EnableConsoleExport = true;
});

var app = builder.Build();
app.Run();
```

## Documentation

- [Usage Guide](USAGE.md) - Detailed usage instructions
- [API Documentation](docs/html/index.html) - Generated Doxygen documentation

### Generating Documentation

```bash
doxygen Doxyfile
```

## Project Structure

```
Toolbox/
├── src/
│   └── Toolbox.Core/           # Core library
│       ├── Abstractions/       # Interfaces
│       ├── Base/               # Base classes
│       ├── Extensions/         # DI extensions
│       ├── Options/            # Configuration options
│       └── Telemetry/          # OpenTelemetry infrastructure
├── tests/
│   └── Toolbox.Tests/          # Unit and integration tests
├── samples/
│   └── Toolbox.Sample/         # Sample application
└── docs/                       # Generated documentation
```

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```

## License

This project is licensed under the MIT License.
