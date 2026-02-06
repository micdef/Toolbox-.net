# Toolbox Usage Guide

This guide provides detailed instructions for using the Toolbox library.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Cryptography Services](#cryptography-services)
   - [Base64 Encoding](#base64-encoding)
   - [AES Encryption](#aes-encryption)
   - [RSA Encryption](#rsa-encryption)
3. [File Transfer Services](#file-transfer-services)
   - [FTP/FTPS](#ftpftps)
   - [SFTP](#sftp)
4. [Mailing Services](#mailing-services)
5. [Creating Custom Services](#creating-custom-services)
6. [OpenTelemetry Integration](#opentelemetry-integration)
7. [Configuration Options](#configuration-options)
8. [Best Practices](#best-practices)

## Getting Started

### Basic Setup

Add Toolbox to your application:

```csharp
using Toolbox.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Register Toolbox core services
builder.Services.AddToolboxCore();

var app = builder.Build();
```

### Configuration via appsettings.json

```json
{
  "Toolbox": {
    "EnableDetailedTelemetry": false,
    "ServicePrefix": "MyApp",
    "AsyncDisposalTimeout": "00:00:30"
  },
  "Toolbox:Telemetry": {
    "EnableTracing": true,
    "EnableMetrics": true,
    "EnableConsoleExport": false,
    "ServiceName": "MyApplication",
    "ServiceVersion": "1.0.0"
  }
}
```

```csharp
builder.Services.AddToolboxCore(builder.Configuration);
builder.Services.AddToolboxOpenTelemetry(builder.Configuration);
```

---

## Cryptography Services

### Base64 Encoding

Base64 encoding/decoding service for text obfuscation (not encryption).

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Standard Base64
services.AddBase64Cryptography();

// URL-safe Base64
services.AddBase64Cryptography(Base64EncodingTable.UrlSafe);

// With full options
services.AddBase64Cryptography(options =>
{
    options.EncodingTable = Base64EncodingTable.UrlSafe;
    options.IncludePadding = false;
});
```

#### Usage

```csharp
public class MyService
{
    private readonly ICryptographyService _crypto;

    public MyService(ICryptographyService crypto)
    {
        _crypto = crypto;
    }

    public async Task ProcessAsync()
    {
        // Encode
        var encoded = _crypto.Encrypt("Hello, World!");
        // Result: "SGVsbG8sIFdvcmxkIQ=="

        // Decode
        var decoded = _crypto.Decrypt(encoded);
        // Result: "Hello, World!"

        // Async versions
        var encodedAsync = await _crypto.EncryptAsync("Hello");
        var decodedAsync = await _crypto.DecryptAsync(encodedAsync);
    }
}
```

---

### AES Encryption

Secure symmetric encryption using AES (CBC mode with PKCS7 padding).

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Generate new key and IV
var (key, iv) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);

// Register with generated key
services.AddAesCryptography(key, iv);

// Or with key size enum
services.AddAesCryptography(AesKeySize.Aes256);
```

#### Usage

```csharp
public class SecureService
{
    private readonly ICryptographyService _crypto;

    public SecureService(ICryptographyService crypto)
    {
        _crypto = crypto;
    }

    public async Task<string> EncryptDataAsync(string sensitiveData)
    {
        // Encrypt (returns Base64-encoded ciphertext)
        var encrypted = await _crypto.EncryptAsync(sensitiveData);

        // Decrypt
        var decrypted = await _crypto.DecryptAsync(encrypted);

        return encrypted;
    }
}
```

#### Key Generation

```csharp
// Generate AES-128 key
var (key128, iv128) = AesCryptographyService.GenerateKey(AesKeySize.Aes128);

// Generate AES-192 key
var (key192, iv192) = AesCryptographyService.GenerateKey(AesKeySize.Aes192);

// Generate AES-256 key (recommended)
var (key256, iv256) = AesCryptographyService.GenerateKey(AesKeySize.Aes256);
```

---

### RSA Encryption

Asymmetric encryption using RSA with various key sizes and padding modes.

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Generate key pair
var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);

// Register with full key pair (encrypt + decrypt)
services.AddRsaCryptography(
    keyPair.PublicKey!,
    RsaPaddingMode.OaepSha256,
    keyPair.PrivateKey);

// Register with public key only (encrypt only)
services.AddRsaCryptography(
    keyPair.PublicKey!,
    RsaPaddingMode.OaepSha256);

// Register with X.509 certificate
var cert = new X509Certificate2("certificate.pfx", "password");
services.AddRsaCryptography(cert, RsaPaddingMode.OaepSha256);
```

#### Usage

```csharp
public class AsymmetricService
{
    private readonly ICryptographyService _crypto;

    public AsymmetricService(ICryptographyService crypto)
    {
        _crypto = crypto;
    }

    public async Task<(string encrypted, string decrypted)> ProcessAsync(string data)
    {
        // Encrypt with public key
        var encrypted = await _crypto.EncryptAsync(data);

        // Decrypt with private key (requires private key)
        var decrypted = await _crypto.DecryptAsync(encrypted);

        return (encrypted, decrypted);
    }
}
```

#### Key Generation

```csharp
// Generate key pair (bytes)
var keyPair = RsaCryptographyService.GenerateKeyPair(RsaKeySize.Rsa2048);
byte[] publicKey = keyPair.PublicKey!;
byte[] privateKey = keyPair.PrivateKey!;

// Generate self-signed certificate
var certKeyPair = RsaCryptographyService.GenerateCertificate(
    RsaKeySize.Rsa4096,
    "CN=MyApplication",
    TimeSpan.FromDays(365));
X509Certificate2 certificate = certKeyPair.Certificate!;

// Public key only (for sharing)
var publicOnly = RsaCryptographyService.GenerateKeyPair(
    RsaKeySize.Rsa2048,
    includePrivateKey: false);
```

#### Padding Modes

| Mode | Description |
|------|-------------|
| `Pkcs1` | PKCS#1 v1.5 padding (legacy) |
| `OaepSha1` | OAEP with SHA-1 |
| `OaepSha256` | OAEP with SHA-256 (recommended) |
| `OaepSha384` | OAEP with SHA-384 |
| `OaepSha512` | OAEP with SHA-512 |

---

## File Transfer Services

### FTP/FTPS

File transfer service for FTP and FTPS (FTP over TLS) protocols.

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Basic FTP
services.AddFtpFileTransfer(options =>
{
    options.Host = "ftp.example.com";
    options.Port = 21;
    options.Username = "user";
    options.Password = "password";
    options.Protocol = FileTransferProtocol.Ftp;
});

// Secure FTPS
services.AddFtpFileTransfer(options =>
{
    options.Host = "ftp.example.com";
    options.Port = 21;
    options.Username = "user";
    options.Password = "password";
    options.Protocol = FileTransferProtocol.Ftps;
});

// From configuration
services.AddFtpFileTransfer(configuration.GetSection("Ftp"));
```

#### Usage

```csharp
public class FileService
{
    private readonly IFileTransferService _ftp;

    public FileService(IFileTransferService ftp)
    {
        _ftp = ftp;
    }

    public async Task TransferFilesAsync()
    {
        // Upload single file
        await _ftp.UploadOneAsync(
            localPath: @"C:\local\file.txt",
            remotePath: "/remote/file.txt",
            overwrite: true);

        // Download single file
        await _ftp.DownloadOneAsync(
            remotePath: "/remote/file.txt",
            localPath: @"C:\local\downloaded.txt");

        // Batch upload
        var uploads = new[]
        {
            (@"C:\local\file1.txt", "/remote/file1.txt"),
            (@"C:\local\file2.txt", "/remote/file2.txt")
        };
        int successCount = await _ftp.UploadBatchAsync(uploads);

        // Batch download
        var downloads = new[]
        {
            ("/remote/file1.txt", @"C:\local\file1.txt"),
            ("/remote/file2.txt", @"C:\local\file2.txt")
        };
        int downloaded = await _ftp.DownloadBatchAsync(downloads);
    }
}
```

---

### SFTP

Secure file transfer over SSH (SFTP protocol).

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Password authentication
services.AddSftpFileTransfer(options =>
{
    options.Host = "sftp.example.com";
    options.Port = 22;
    options.Username = "user";
    options.Password = "password";
});

// Private key authentication
services.AddSftpFileTransfer(options =>
{
    options.Host = "sftp.example.com";
    options.Port = 22;
    options.Username = "user";
    options.PrivateKeyPath = @"C:\keys\id_rsa";
    options.PrivateKeyPassphrase = "passphrase"; // Optional
});

// Private key from string
services.AddSftpFileTransfer(options =>
{
    options.Host = "sftp.example.com";
    options.Username = "user";
    options.PrivateKeyContent = "-----BEGIN RSA PRIVATE KEY-----\n...";
});
```

#### Usage

```csharp
public class SecureFileService
{
    private readonly IFileTransferService _sftp;

    public SecureFileService(IFileTransferService sftp)
    {
        _sftp = sftp;
    }

    public async Task TransferAsync(CancellationToken ct)
    {
        // Upload with cancellation support
        await _sftp.UploadOneAsync(
            @"C:\data\report.pdf",
            "/uploads/report.pdf",
            overwrite: true,
            ct);

        // Download
        await _sftp.DownloadOneAsync(
            "/downloads/data.csv",
            @"C:\data\data.csv",
            cancellationToken: ct);
    }
}
```

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Host` | string | - | Server hostname |
| `Port` | int | 21/22 | Server port |
| `Username` | string | - | Login username |
| `Password` | string? | null | Password (if using password auth) |
| `PrivateKeyPath` | string? | null | Path to private key file |
| `PrivateKeyContent` | string? | null | Private key as string |
| `PrivateKeyPassphrase` | string? | null | Private key passphrase |
| `Protocol` | FileTransferProtocol | Ftp | Ftp, Ftps, or Sftp |
| `ConnectionTimeout` | TimeSpan | 30s | Connection timeout |
| `OperationTimeout` | TimeSpan | 5min | Operation timeout |
| `BufferSize` | int | 32KB | Transfer buffer size |
| `AutoCreateDirectory` | bool | true | Auto-create remote directories |

---

## Mailing Services

SMTP email sending service with TLS/SSL, OAuth2, and attachment support.

### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Generic SMTP
services.AddSmtpMailing(options =>
{
    options.Host = "smtp.example.com";
    options.Port = 587;
    options.SecurityMode = SmtpSecurityMode.StartTls;
    options.Username = "user@example.com";
    options.Password = "password";
    options.DefaultFrom = new EmailAddress("noreply@example.com", "My App");
});

// Gmail shortcut
services.AddGmailMailing(
    email: "myapp@gmail.com",
    appPassword: "xxxx-xxxx-xxxx-xxxx");

// Outlook/Office 365 shortcut
services.AddOutlookMailing(
    email: "myapp@outlook.com",
    password: "password");

// Anonymous relay (internal servers)
services.AddSmtpMailing(options =>
{
    options.Host = "mail.internal.local";
    options.Port = 25;
    options.SecurityMode = SmtpSecurityMode.None;
});

// From configuration
services.AddSmtpMailing(configuration.GetSection("Mailing"));
```

### Usage

```csharp
public class NotificationService
{
    private readonly IMailingService _mailing;

    public NotificationService(IMailingService mailing)
    {
        _mailing = mailing;
    }

    public async Task SendWelcomeEmailAsync(string userEmail, string userName)
    {
        var message = new EmailMessage
        {
            From = new EmailAddress("noreply@example.com", "My Application"),
            Subject = "Welcome to Our Service!",
            Body = $"<h1>Welcome, {userName}!</h1><p>Thank you for joining us.</p>",
            IsBodyHtml = true,
            To = { new EmailAddress(userEmail, userName) }
        };

        await _mailing.SendMailAsync(message);
    }

    public async Task SendReportAsync(string[] recipients, byte[] pdfReport)
    {
        var message = new EmailMessage
        {
            From = new EmailAddress("reports@example.com", "Report System"),
            Subject = "Monthly Report",
            Body = "Please find the monthly report attached.",
            IsBodyHtml = false,
            Attachments =
            {
                EmailAttachment.FromBytes(pdfReport, "report.pdf", "application/pdf")
            }
        };

        // Multiple recipients
        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        await _mailing.SendMailAsync(message);
    }

    public async Task SendBccEmailAsync(string[] bccRecipients)
    {
        // Email with BCC only (no visible recipients)
        var message = new EmailMessage
        {
            From = new EmailAddress("newsletter@example.com"),
            Subject = "Newsletter",
            Body = "<h1>Monthly Newsletter</h1>",
            IsBodyHtml = true
        };

        foreach (var bcc in bccRecipients)
        {
            message.Bcc.Add(bcc);
        }

        await _mailing.SendMailAsync(message);
    }
}
```

### Email with Inline Images

```csharp
var logoBytes = File.ReadAllBytes("logo.png");

var message = new EmailMessage
{
    From = new EmailAddress("noreply@example.com"),
    Subject = "Email with Logo",
    Body = @"
        <html>
        <body>
            <img src='cid:company-logo' alt='Logo' />
            <h1>Welcome!</h1>
        </body>
        </html>",
    IsBodyHtml = true,
    To = { "recipient@example.com" },
    Attachments =
    {
        EmailAttachment.CreateInline(logoBytes, "logo.png", "company-logo")
    }
};

await _mailing.SendMailAsync(message);
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Host` | string | "localhost" | SMTP server hostname |
| `Port` | int | 25 | SMTP port (25, 587, 465) |
| `SecurityMode` | SmtpSecurityMode | Auto | Security mode |
| `Username` | string? | null | SMTP username |
| `Password` | string? | null | SMTP password |
| `OAuth2AccessToken` | string? | null | OAuth2 token (Gmail, O365) |
| `ConnectionTimeout` | TimeSpan | 30s | Connection timeout |
| `OperationTimeout` | TimeSpan | 2min | Send timeout |
| `ValidateCertificate` | bool | true | Validate SSL certificate |
| `DefaultFrom` | EmailAddress? | null | Default sender address |
| `DefaultReplyTo` | EmailAddress? | null | Default reply-to address |

### Security Modes

| Mode | Port | Description |
|------|------|-------------|
| `Auto` | - | Automatic detection |
| `None` | 25 | No encryption (internal only) |
| `StartTls` | 587 | Upgrade to TLS (recommended) |
| `StartTlsWhenAvailable` | 587 | TLS if available |
| `SslOnConnect` | 465 | Implicit SSL/TLS |

---

## Creating Custom Services

### Synchronous Disposal

For services with synchronous cleanup:

```csharp
using Toolbox.Core.Base;

public class MyService : BaseDisposableService
{
    private readonly SomeResource _resource;

    public MyService(ILogger<MyService> logger)
        : base("MyService", logger)
    {
        _resource = new SomeResource();
    }

    public void DoWork()
    {
        ThrowIfDisposed();

        using var activity = StartActivity();
        var sw = Stopwatch.StartNew();

        _resource.Process();

        RecordOperation("DoWork", sw.ElapsedMilliseconds);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resource?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### Asynchronous Disposal

For services with async resources:

```csharp
using Toolbox.Core.Base;

public class MyAsyncService : BaseAsyncDisposableService
{
    private readonly Stream _stream;

    public MyAsyncService(ILogger<MyAsyncService> logger)
        : base("MyAsyncService", logger)
    {
        _stream = new FileStream("data.bin", FileMode.OpenOrCreate);
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        using var activity = StartActivity();
        var sw = Stopwatch.StartNew();

        await _stream.WriteAsync(data, ct);

        RecordOperation("ProcessAsync", sw.ElapsedMilliseconds);
    }

    protected override async ValueTask DisposeAsyncCore(CancellationToken ct)
    {
        await _stream.DisposeAsync();
    }
}
```

---

## OpenTelemetry Integration

### Basic Tracing and Metrics

```csharp
builder.Services.AddToolboxOpenTelemetry(options =>
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.ServiceName = "MyApplication";
    options.ServiceVersion = "1.0.0";
});
```

### OTLP Export

```csharp
builder.Services.AddToolboxOpenTelemetry(options =>
{
    options.OtlpEndpoint = "http://localhost:4317";
});
```

### Custom Instrumentation

```csharp
using Toolbox.Core.Telemetry;

// Start a custom activity
using var activity = ToolboxActivitySource.StartActivity("CustomOperation");
activity?.SetTag("custom.tag", "value");

// Record metrics
ToolboxMeter.RecordOperation("MyService", "CustomOp", elapsedMs);
```

### Available Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `toolbox.operations.count` | Counter | Total operations |
| `toolbox.operations.duration` | Histogram | Operation duration (ms) |
| `toolbox.crypto.encrypt.count` | Counter | Encryption operations |
| `toolbox.crypto.decrypt.count` | Counter | Decryption operations |
| `toolbox.crypto.data.size` | Histogram | Data size (bytes) |
| `toolbox.filetransfer.upload.count` | Counter | File uploads |
| `toolbox.filetransfer.download.count` | Counter | File downloads |
| `toolbox.filetransfer.size` | Histogram | File size (bytes) |

---

## Configuration Options

### ToolboxOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableDetailedTelemetry` | bool | false | Enable detailed telemetry |
| `ServicePrefix` | string? | null | Prefix for service names |
| `AsyncDisposalTimeout` | TimeSpan | 30s | Timeout for async disposal |

### ToolboxTelemetryOptions

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableTracing` | bool | true | Enable distributed tracing |
| `EnableMetrics` | bool | true | Enable metrics collection |
| `EnableConsoleExport` | bool | false | Export to console |
| `OtlpEndpoint` | string? | null | OTLP collector endpoint |
| `ServiceName` | string | "Toolbox" | Service name for telemetry |
| `ServiceVersion` | string | "1.0.0" | Service version |

---

## Best Practices

### 1. Always Check Disposal State

```csharp
public void DoWork()
{
    ThrowIfDisposed();  // Call this first
    // ... rest of method
}
```

### 2. Use Activity Scopes for Tracing

```csharp
public async Task DoWorkAsync()
{
    using var activity = StartActivity();
    // Activity automatically ends when scope exits
}
```

### 3. Record Metrics for Performance

```csharp
var sw = Stopwatch.StartNew();
// ... operation
RecordOperation("OperationName", sw.ElapsedMilliseconds);
```

### 4. Handle Cancellation in Async Operations

```csharp
public async Task ProcessAsync(CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    await _service.DoWorkAsync(ct);
}
```

### 5. Use Appropriate Service Lifetimes

```csharp
// Singleton for stateless services
services.AddSingleton<ICryptographyService, AesCryptographyService>();

// Scoped for per-request services
services.AddScoped<IFileTransferService, SftpFileTransferService>();

// Transient for lightweight services
services.AddTransient<IMailingService, SmtpMailingService>();
```

### 6. Secure Credential Management

```csharp
// Use configuration/secrets, not hardcoded values
services.AddSmtpMailing(configuration.GetSection("Mailing"));

// Or use Azure Key Vault, AWS Secrets Manager, etc.
var password = await secretsClient.GetSecretAsync("smtp-password");
```
