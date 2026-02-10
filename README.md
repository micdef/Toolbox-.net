# Toolbox

A .NET 10 library providing base services, telemetry, and dependency injection extensions.

## Features

- **Cryptography Services**: AES, RSA, and Base64 encoding/decoding
- **File Transfer Services**: FTP, FTPS, and SFTP with batch operations
- **Mailing Services**: SMTP with TLS/SSL, OAuth2, and attachment support
- **API Services**: HTTP client with multiple authentication modes and retry policies
- **LDAP Services**: Active Directory, Azure AD, OpenLDAP, and Apple Directory integration
- **Disposable Services**: Thread-safe base classes for synchronous and asynchronous disposal
- **OpenTelemetry Integration**: Built-in distributed tracing and metrics collection
- **Dependency Injection**: Fluent extensions for easy service registration
- **Comprehensive Documentation**: Full Doxygen documentation (HTML, LaTeX, XML, DocBook, RTF, Man)

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

- [Usage Guide](USAGE.md) - Detailed usage instructions and examples
- [API Documentation (HTML)](docs/html/index.html) - Web-based API reference
- [API Documentation (PDF)](docs/latex/refman.pdf) - PDF reference manual (requires LaTeX compilation)

### Available Documentation Formats

| Format | Location | Description |
|--------|----------|-------------|
| HTML | `docs/html/` | Web-based documentation |
| LaTeX | `docs/latex/` | PDF generation source |
| XML | `docs/xml/` | Structured XML for tooling |
| DocBook | `docs/docbook/` | Technical documentation format |
| RTF | `docs/rtf/` | Rich Text Format |
| Man | `docs/man/` | Unix manual pages |

### Generating Documentation

```bash
doxygen Doxyfile
```

## Project Structure

```
Toolbox/
├── src/
│   └── Toolbox.Core/           # Core library
│       ├── Abstractions/       # Service interfaces
│       │   └── Services/       # ICryptographyService, IFileTransferService, etc.
│       ├── Base/               # Base classes for services
│       ├── Extensions/         # DI extension methods
│       ├── Options/            # Configuration options and DTOs
│       ├── Services/           # Service implementations
│       │   ├── Api/            # HTTP API service
│       │   ├── Cryptography/   # AES, RSA, Base64 services
│       │   ├── FileTransfer/   # FTP, SFTP services
│       │   ├── Ldap/           # Active Directory, Azure AD, OpenLDAP, Apple Directory
│       │   └── Mailing/        # SMTP service
│       └── Telemetry/          # OpenTelemetry infrastructure
├── tests/
│   └── Toolbox.Tests/          # Unit and integration tests
├── samples/
│   └── Toolbox.Sample/         # Sample application
└── docs/                       # Generated Doxygen documentation
    ├── html/                   # Web documentation
    ├── latex/                  # PDF source
    ├── xml/                    # Structured XML
    ├── docbook/                # DocBook format
    ├── rtf/                    # Rich Text Format
    └── man/                    # Unix manual pages
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
