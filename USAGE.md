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
5. [API Services](#api-services)
6. [LDAP Services](#ldap-services)
   - [Active Directory](#active-directory)
   - [Azure AD / Entra ID](#azure-ad--entra-id)
   - [OpenLDAP](#openldap)
   - [Apple Directory](#apple-directory)
   - [Advanced Authentication](#advanced-authentication)
7. [SSO Services](#sso-services)
   - [Session Management](#session-management)
   - [Credential Storage](#credential-storage)
   - [Automatic Token Refresh](#automatic-token-refresh)
8. [Creating Custom Services](#creating-custom-services)
9. [OpenTelemetry Integration](#opentelemetry-integration)
10. [Configuration Options](#configuration-options)
11. [Best Practices](#best-practices)

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

## API Services

HTTP API client service with multiple authentication modes and automatic retry.

### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Anonymous API
services.AddHttpApiAnonymous("https://api.example.com");

// Bearer token authentication
services.AddHttpApiWithBearerToken(
    "https://api.example.com",
    "your-bearer-token");

// Basic authentication
services.AddHttpApiWithBasicAuth(
    "https://api.example.com",
    "username",
    "password");

// API key authentication (header)
services.AddHttpApiWithApiKey(
    "https://api.example.com",
    "your-api-key",
    "X-API-Key",
    ApiKeyLocation.Header);

// API key authentication (query string)
services.AddHttpApiWithApiKey(
    "https://api.example.com",
    "your-api-key",
    "api_key",
    ApiKeyLocation.QueryString);

// Client certificate authentication
services.AddHttpApiWithCertificate(
    "https://api.example.com",
    new X509Certificate2("client.pfx", "password"));

// OAuth2 client credentials
services.AddHttpApiWithOAuth2(
    "https://api.example.com",
    "https://auth.example.com/oauth/token",
    "client-id",
    "client-secret",
    "read write");

// Full configuration
services.AddHttpApi(options =>
{
    options.BaseUrl = "https://api.example.com";
    options.AuthenticationMode = ApiAuthenticationMode.BearerToken;
    options.BearerToken = "your-token";
    options.Timeout = TimeSpan.FromSeconds(30);
    options.MaxRetries = 3;
    options.UseExponentialBackoff = true;
});

// From configuration
services.AddHttpApi(configuration.GetSection("Api"));
```

### Usage

```csharp
public class DataService
{
    private readonly IApiService _api;

    public DataService(IApiService api)
    {
        _api = api;
    }

    public async Task<User?> GetUserAsync(int id)
    {
        // Simple GET request
        var request = ApiRequest.Get($"/users/{id}");
        var response = await _api.SendAsync(request);

        response.EnsureSuccess();
        return response.Deserialize<User>();
    }

    public async Task<User?> CreateUserAsync(User user)
    {
        // POST with JSON body
        var request = ApiRequest.Post("/users", user);
        return await _api.SendAsync<User>(request);
    }

    public async Task UpdateUserAsync(int id, User user)
    {
        // PUT with JSON body
        var request = ApiRequest.Put($"/users/{id}", user);
        await _api.SendAsync(request);
    }

    public async Task PatchUserAsync(int id, object partialData)
    {
        // PATCH with JSON body
        var request = ApiRequest.Patch($"/users/{id}", partialData);
        await _api.SendAsync(request);
    }

    public async Task DeleteUserAsync(int id)
    {
        // DELETE request
        var request = ApiRequest.Delete($"/users/{id}");
        await _api.SendAsync(request);
    }
}
```

### Advanced Usage

```csharp
public class AdvancedApiService
{
    private readonly IApiService _api;

    public AdvancedApiService(IApiService api)
    {
        _api = api;
    }

    public async Task<SearchResult> SearchAsync(string query, int page)
    {
        // Request with query parameters and headers
        var request = ApiRequest.Get("/search")
            .WithQuery("q", query)
            .WithQuery("page", page.ToString())
            .WithHeader("X-Request-ID", Guid.NewGuid().ToString())
            .WithTimeout(TimeSpan.FromSeconds(60));

        var response = await _api.SendAsync(request);

        if (response.IsSuccess)
        {
            return response.Deserialize<SearchResult>()!;
        }

        if (response.IsClientError)
        {
            throw new InvalidOperationException($"Client error: {response.ReasonPhrase}");
        }

        throw new HttpRequestException($"Server error: {response.StatusCode}");
    }

    public async Task UploadFileAsync(byte[] fileData, string fileName)
    {
        // Binary content
        var request = ApiRequest.Post("/upload")
            .SetBinaryContent(fileData, "application/octet-stream")
            .WithHeader("X-File-Name", fileName);

        await _api.SendAsync(request);
    }

    public async Task SubmitFormAsync(Dictionary<string, string> formData)
    {
        // Form URL-encoded content
        var request = ApiRequest.Post("/form")
            .SetFormContent(formData);

        await _api.SendAsync(request);
    }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BaseUrl` | string? | null | Base URL for all requests |
| `AuthenticationMode` | ApiAuthenticationMode | Anonymous | Authentication method |
| `Timeout` | TimeSpan | 30s | Request timeout |
| `MaxRetries` | int | 3 | Maximum retry attempts |
| `RetryDelay` | TimeSpan | 1s | Delay between retries |
| `UseExponentialBackoff` | bool | true | Use exponential backoff |
| `ValidateCertificate` | bool | true | Validate SSL certificates |
| `FollowRedirects` | bool | true | Follow HTTP redirects |
| `MaxRedirects` | int | 10 | Maximum redirects to follow |
| `UserAgent` | string | "Toolbox..." | Default User-Agent header |

### Authentication Modes

| Mode | Description |
|------|-------------|
| `Anonymous` | No authentication |
| `BearerToken` | Bearer token in Authorization header |
| `Basic` | Basic authentication (username:password) |
| `ApiKey` | API key in header or query string |
| `Certificate` | Client certificate authentication |
| `OAuth2ClientCredentials` | OAuth2 client credentials flow |

---

## LDAP Services

Directory services for querying users, groups, and computers from various LDAP providers.

### Active Directory

Windows Active Directory service using LDAP protocol.

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Using current Windows credentials
services.AddActiveDirectory(options =>
{
    options.Domain = "corp.example.com";
    options.UseCurrentCredentials = true;
    options.UseSsl = true;
});

// Using explicit credentials
services.AddActiveDirectory(options =>
{
    options.Domain = "corp.example.com";
    options.Server = "dc01.corp.example.com";
    options.Port = 636;
    options.UseSsl = true;
    options.Username = "CORP\\serviceaccount";
    options.Password = "password";
});

// From configuration
services.AddActiveDirectory(configuration.GetSection("Toolbox:Ldap:ActiveDirectory"));
```

#### Usage

```csharp
public class UserService
{
    private readonly ILdapService _ldap;

    public UserService(ILdapService ldap)
    {
        _ldap = ldap;
    }

    public async Task<LdapUser?> FindUserAsync(string username)
    {
        var user = await _ldap.GetUserByUsernameAsync(username);

        if (user != null)
        {
            Console.WriteLine($"Found: {user.DisplayName} ({user.Email})");
            Console.WriteLine($"Department: {user.Department}");
        }

        return user;
    }

    public async Task<bool> ValidateUserAsync(string username, string password)
    {
        return await _ldap.ValidateCredentialsAsync(username, password);
    }

    public async Task<PagedResult<LdapUser>> SearchUsersAsync(string department)
    {
        var criteria = LdapSearchCriteria.Create()
            .WithDepartment(department)
            .EnabledOnly();

        return await _ldap.SearchUsersAsync(criteria, page: 1, pageSize: 25);
    }
}
```

#### Group and Computer Operations

```csharp
// Get group by name
var group = await _ldap.GetGroupByNameAsync("Developers");

// Get group members with pagination
var members = await _ldap.GetGroupMembersAsync("CN=Developers,OU=Groups,DC=example,DC=com", page: 1, pageSize: 50);

// Get computer by name
var computer = await _ldap.GetComputerByNameAsync("SERVER01");

// Search servers
var criteria = LdapComputerSearchCriteria.Create()
    .WithOperatingSystem("Windows Server*")
    .EnabledOnly();
var servers = await _ldap.SearchComputersAsync(criteria, page: 1, pageSize: 25);
```

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Domain` | string | - | Fully qualified domain name |
| `Server` | string? | null | Domain controller (auto-discovers if null) |
| `Port` | int | 389 | LDAP port (636 for SSL) |
| `BaseDn` | string? | null | Base DN for searches |
| `Username` | string? | null | Bind username |
| `Password` | string? | null | Bind password |
| `UseSsl` | bool | false | Use SSL/TLS (LDAPS) |
| `UseCurrentCredentials` | bool | false | Use Windows integrated auth |
| `ValidateCertificate` | bool | true | Validate SSL certificate |
| `ConnectionTimeout` | TimeSpan | 30s | Connection timeout |
| `OperationTimeout` | TimeSpan | 60s | Operation timeout |

---

### Azure AD / Entra ID

Azure Active Directory service using Microsoft Graph API.

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Client secret authentication
services.AddAzureAd(options =>
{
    options.TenantId = "your-tenant-id";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.AuthenticationMode = AzureAdAuthMode.ClientSecret;
});

// Managed identity (Azure-hosted apps)
services.AddAzureAdWithManagedIdentity(
    tenantId: "your-tenant-id",
    clientId: "your-client-id");

// Certificate authentication
services.AddAzureAd(options =>
{
    options.TenantId = "your-tenant-id";
    options.ClientId = "your-client-id";
    options.CertificatePath = "/path/to/cert.pfx";
    options.CertificatePassword = "password";
    options.AuthenticationMode = AzureAdAuthMode.Certificate;
});

// From configuration
services.AddAzureAd(configuration.GetSection("Toolbox:Ldap:AzureAd"));
```

#### Usage

```csharp
public class AzureUserService
{
    private readonly ILdapService _azureAd;

    public AzureUserService(ILdapService azureAd)
    {
        _azureAd = azureAd;
    }

    public async Task<LdapUser?> GetUserAsync(string email)
    {
        return await _azureAd.GetUserByEmailAsync(email);
    }

    public async Task<IEnumerable<LdapUser>> SearchByDepartmentAsync(string department)
    {
        // Azure AD uses OData filter syntax
        return await _azureAd.SearchUsersAsync($"department eq '{department}'", maxResults: 50);
    }

    public async Task<IEnumerable<LdapGroup>> GetSecurityGroupsAsync()
    {
        return await _azureAd.SearchGroupsAsync("securityEnabled eq true", maxResults: 100);
    }
}
```

#### Important Notes

- Azure AD does not support `ValidateCredentials()` - use Azure AD authentication flows instead
- Search filters use OData syntax, not LDAP filter syntax
- Computer queries return Azure AD joined devices

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `TenantId` | string | - | Azure AD tenant ID or domain |
| `ClientId` | string | - | Application (client) ID |
| `ClientSecret` | string? | null | Client secret |
| `AuthenticationMode` | AzureAdAuthMode | ClientSecret | Authentication method |
| `UseManagedIdentity` | bool | false | Use Azure Managed Identity |
| `CertificatePath` | string? | null | Path to certificate file |
| `CertificateThumbprint` | string? | null | Certificate thumbprint |
| `GraphApiBaseUrl` | string | v1.0 endpoint | Microsoft Graph API URL |

---

### OpenLDAP

OpenLDAP or compatible Linux directory service.

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Basic configuration
services.AddOpenLdap(options =>
{
    options.Host = "ldap.example.com";
    options.Port = 389;
    options.BaseDn = "dc=example,dc=com";
    options.BindDn = "cn=admin,dc=example,dc=com";
    options.BindPassword = "secret";
    options.SecurityMode = LdapSecurityMode.StartTls;
});

// FreeIPA configuration
services.AddOpenLdap(options =>
{
    options.Host = "ipa.example.com";
    options.Port = 636;
    options.BaseDn = "dc=example,dc=com";
    options.BindDn = "uid=admin,cn=users,cn=accounts,dc=example,dc=com";
    options.BindPassword = "secret";
    options.SecurityMode = LdapSecurityMode.Ssl;
    options.UserObjectClass = "inetOrgPerson";
    options.GroupObjectClass = "groupOfNames";
});

// From configuration
services.AddOpenLdap(configuration.GetSection("Toolbox:Ldap:OpenLdap"));
```

#### Usage

```csharp
public class LinuxUserService
{
    private readonly ILdapService _ldap;

    public LinuxUserService(ILdapService ldap)
    {
        _ldap = ldap;
    }

    public async Task<LdapUser?> GetUserAsync(string uid)
    {
        return await _ldap.GetUserByUsernameAsync(uid);
    }

    public async Task<bool> AuthenticateAsync(string uid, string password)
    {
        return await _ldap.ValidateCredentialsAsync(uid, password);
    }
}
```

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Host` | string | - | LDAP server hostname |
| `Port` | int | 389 | LDAP port |
| `BaseDn` | string | - | Base DN for searches |
| `BindDn` | string? | null | Bind DN for authentication |
| `BindPassword` | string? | null | Bind password |
| `SecurityMode` | LdapSecurityMode | None | Security mode (None, Ssl, StartTls) |
| `UserObjectClass` | string | inetOrgPerson | User object class |
| `GroupObjectClass` | string | groupOfNames | Group object class |
| `UsernameAttribute` | string | uid | Username attribute |

---

### Apple Directory

Apple Open Directory service for macOS environments.

#### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

services.AddAppleDirectory(options =>
{
    options.Host = "od.example.com";
    options.Port = 389;
    options.BaseDn = "dc=example,dc=com";
    options.BindDn = "uid=admin,cn=users,dc=example,dc=com";
    options.BindPassword = "secret";
    options.UseSsl = true;
});

// From configuration
services.AddAppleDirectory(configuration.GetSection("Toolbox:Ldap:AppleDirectory"));
```

#### Usage

```csharp
public class MacUserService
{
    private readonly ILdapService _ldap;

    public MacUserService(ILdapService ldap)
    {
        _ldap = ldap;
    }

    public async Task<LdapUser?> GetMacUserAsync(string uid)
    {
        return await _ldap.GetUserByUsernameAsync(uid);
    }

    public async Task<IEnumerable<string>> GetUserGroupsAsync(string uid)
    {
        return await _ldap.GetUserGroupsAsync(uid);
    }
}
```

#### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Host` | string | - | Directory server hostname |
| `Port` | int | 389 | LDAP port |
| `BaseDn` | string | - | Base DN |
| `BindDn` | string? | null | Bind DN |
| `BindPassword` | string? | null | Bind password |
| `UseSsl` | bool | false | Use SSL |
| `UserObjectClass` | string | apple-user | Apple user object class |
| `GroupObjectClass` | string | apple-group | Apple group object class |
| `UniqueIdAttribute` | string | apple-generateduid | Unique ID attribute |

---

### Advanced Authentication

All LDAP services support advanced authentication methods beyond simple username/password authentication.

#### Authentication Modes

| Mode | AD | Azure AD | OpenLDAP | Apple | Description |
|------|:--:|:--------:|:--------:|:-----:|-------------|
| `Simple` | ✓ | ✓* | ✓ | ✓ | DN + Password |
| `Anonymous` | ✓ | ✗ | ✓ | ✓ | No credentials |
| `Kerberos` | ✓ | ✗ | ✓** | ✗ | GSSAPI/SPNEGO |
| `Ntlm` | ✓ | ✗ | ✗ | ✗ | NTLM legacy |
| `Negotiate` | ✓ | ✗ | ✗ | ✗ | Auto Kerberos/NTLM |
| `IntegratedWindows` | ✓ | ✗ | ✗ | ✗ | Current Windows context |
| `Certificate` | ✓ | ✓ | ✓** | ✓** | X.509 client certificate |
| `SaslPlain` | ✗ | ✗ | ✓ | ✓ | SASL PLAIN |
| `SaslExternal` | ✗ | ✗ | ✓** | ✓** | SASL EXTERNAL (certificate) |
| `SaslGssapi` | ✗ | ✗ | ✓** | ✗ | SASL GSSAPI (Kerberos) |

\* Azure AD Simple maps to ROPC (Resource Owner Password Credentials) OAuth2 flow
\*\* Limited support - may return failure with guidance

#### Usage with Options

```csharp
using Toolbox.Core.Options;

// Authenticate with options
var authOptions = new LdapAuthenticationOptions
{
    Mode = LdapAuthenticationMode.Simple,
    Username = "john.doe",
    Password = "password",
    IncludeGroups = true,
    IncludeClaims = true,
    Timeout = TimeSpan.FromSeconds(30)
};

var result = await ldapService.AuthenticateAsync(authOptions);

if (result.IsAuthenticated)
{
    Console.WriteLine($"Authenticated: {result.Username}");
    Console.WriteLine($"DN: {result.UserDistinguishedName}");
    Console.WriteLine($"Groups: {string.Join(", ", result.Groups ?? [])}");
}
else
{
    Console.WriteLine($"Failed: {result.ErrorMessage} ({result.ErrorCode})");
}
```

#### Kerberos Authentication (Active Directory)

```csharp
// Using current Windows ticket (SSO)
var result = await adService.AuthenticateWithKerberosAsync();

// Using explicit credentials
var authOptions = new LdapAuthenticationOptions
{
    Mode = LdapAuthenticationMode.Kerberos,
    Username = "john.doe@CORP.EXAMPLE.COM",
    Password = "password",
    Domain = "CORP"
};
var result = await adService.AuthenticateAsync(authOptions);
```

#### Integrated Windows Authentication

```csharp
// Uses the Windows identity of the current process
var authOptions = new LdapAuthenticationOptions
{
    Mode = LdapAuthenticationMode.IntegratedWindows
};
var result = await adService.AuthenticateAsync(authOptions);
```

#### Certificate Authentication

```csharp
using System.Security.Cryptography.X509Certificates;

// Using certificate object
var cert = new X509Certificate2("client.pfx", "password");
var result = await ldapService.AuthenticateWithCertificateAsync(cert);

// Using options with certificate path
var authOptions = new LdapAuthenticationOptions
{
    Mode = LdapAuthenticationMode.Certificate,
    CertificatePath = "/path/to/client.pfx",
    CertificatePassword = "password"
};
var result = await ldapService.AuthenticateAsync(authOptions);
```

#### Azure AD Interactive Authentication

```csharp
// Device code flow (for CLI apps)
var result = await azureAdService.AuthenticateWithDeviceCodeAsync(async deviceCode =>
{
    Console.WriteLine($"Go to {deviceCode.VerificationUri}");
    Console.WriteLine($"Enter code: {deviceCode.UserCode}");
});

if (result.IsAuthenticated)
{
    Console.WriteLine($"Token: {result.Token}");
    Console.WriteLine($"Expires: {result.ExpiresAt}");
}

// Interactive browser flow (for desktop apps)
var result = await azureAdService.AuthenticateWithInteractiveBrowserAsync();

// Username/Password (ROPC - not recommended)
var result = await azureAdService.AuthenticateWithUsernamePasswordAsync(
    "user@domain.com",
    "password");
```

#### Querying Supported Modes

```csharp
// Get supported authentication modes for the service
var supportedModes = ldapService.GetSupportedAuthenticationModes();

foreach (var mode in supportedModes)
{
    Console.WriteLine($"Supported: {mode}");
}
```

#### Authentication Result

The `LdapAuthenticationResult` contains:

| Property | Type | Description |
|----------|------|-------------|
| `IsAuthenticated` | bool | Whether authentication succeeded |
| `Username` | string? | Authenticated username |
| `UserDistinguishedName` | string? | User's DN |
| `AuthenticationMode` | LdapAuthenticationMode | Mode used |
| `DirectoryType` | LdapDirectoryType | Directory type |
| `ErrorMessage` | string? | Error description (if failed) |
| `ErrorCode` | string? | LDAP error code |
| `Groups` | IReadOnlyList<string>? | User's groups (if requested) |
| `Claims` | IDictionary<string, object>? | Additional claims |
| `AuthenticatedAt` | DateTimeOffset? | Authentication timestamp |
| `Token` | string? | OAuth token (Azure AD) |
| `ExpiresAt` | DateTimeOffset? | Token expiration |

---

## SSO Services

Single Sign-On services for session management, credential storage, and automatic token refresh.

### Registration

```csharp
using Toolbox.Core.Extensions;
using Toolbox.Core.Options;

// Basic SSO services
services.AddSsoServices();

// With custom configuration
services.AddSsoServices(
    sso =>
    {
        sso.DefaultSessionDuration = TimeSpan.FromHours(8);
        sso.MaxSessionDuration = TimeSpan.FromDays(7);
        sso.EnableAutoRefresh = true;
        sso.RefreshThreshold = 0.8; // Refresh at 80% of lifetime
        sso.MaxSessionsPerUser = 5;
        sso.PersistSessions = true;
    },
    credStore =>
    {
        credStore.Provider = CredentialStoreProvider.Auto; // Auto-detect platform
        credStore.ApplicationName = "MyApp";
    });

// From configuration
services.AddSsoServices(configuration);
```

### Session Management

```csharp
public class AuthController
{
    private readonly ISsoSessionManager _sessionManager;
    private readonly ILdapService _ldapService;

    public AuthController(ISsoSessionManager sessionManager, ILdapService ldapService)
    {
        _sessionManager = sessionManager;
        _ldapService = ldapService;
    }

    public async Task<SsoSession> LoginAsync(string username, string password)
    {
        // Authenticate with LDAP
        var authResult = await _ldapService.AuthenticateAsync(new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Simple,
            Username = username,
            Password = password,
            IncludeGroups = true
        });

        if (!authResult.IsAuthenticated)
            throw new AuthenticationException(authResult.ErrorMessage);

        // Create SSO session
        return await _sessionManager.CreateSessionAsync(authResult, _ldapService);
    }

    public async Task<SsoSession> LoginWithDeviceBindingAsync(
        string username,
        string password,
        string deviceId,
        string ipAddress,
        string userAgent)
    {
        var authResult = await _ldapService.AuthenticateAsync(new LdapAuthenticationOptions
        {
            Mode = LdapAuthenticationMode.Simple,
            Username = username,
            Password = password
        });

        if (!authResult.IsAuthenticated)
            throw new AuthenticationException(authResult.ErrorMessage);

        // Create session with device binding
        return await _sessionManager.CreateSessionAsync(
            authResult,
            _ldapService,
            deviceId,
            ipAddress,
            userAgent);
    }

    public async Task<bool> ValidateSessionAsync(string sessionId)
    {
        var result = await _sessionManager.ValidateSessionAsync(sessionId);
        return result.IsValid;
    }

    public async Task LogoutAsync(string sessionId)
    {
        await _sessionManager.RevokeSessionAsync(sessionId);
    }

    public async Task LogoutAllDevicesAsync(string userId)
    {
        await _sessionManager.RevokeAllUserSessionsAsync(userId);
    }

    public async Task LogoutOtherDevicesAsync(string userId, string currentSessionId)
    {
        await _sessionManager.RevokeOtherSessionsAsync(userId, currentSessionId);
    }
}
```

### Session Validation

```csharp
public class SessionMiddleware
{
    private readonly ISsoSessionManager _sessionManager;

    public SessionMiddleware(ISsoSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public async Task<SsoSessionValidationResult> ValidateAsync(
        string sessionId,
        string? deviceId = null,
        string? ipAddress = null)
    {
        var result = await _sessionManager.ValidateSessionAsync(sessionId, deviceId, ipAddress);

        if (!result.IsValid)
        {
            switch (result.FailureReason)
            {
                case SsoValidationFailureReason.SessionNotFound:
                    // Session doesn't exist
                    break;
                case SsoValidationFailureReason.SessionExpired:
                    // Session has expired
                    break;
                case SsoValidationFailureReason.SessionRevoked:
                    // Session was explicitly revoked
                    break;
                case SsoValidationFailureReason.DeviceMismatch:
                    // Request from different device
                    break;
                case SsoValidationFailureReason.IpMismatch:
                    // Request from different IP
                    break;
            }
        }

        return result;
    }
}
```

### Session Events

```csharp
public class SessionEventHandler
{
    public SessionEventHandler(ISsoSessionManager sessionManager)
    {
        sessionManager.SessionCreated += OnSessionCreated;
        sessionManager.SessionExpiring += OnSessionExpiring;
        sessionManager.SessionRefreshed += OnSessionRefreshed;
        sessionManager.SessionExpired += OnSessionExpired;
        sessionManager.SessionRevoked += OnSessionRevoked;
    }

    private void OnSessionCreated(object? sender, SsoSessionCreatedEventArgs e)
    {
        Console.WriteLine($"Session created: {e.Session.SessionId} for {e.Session.UserId}");
    }

    private void OnSessionExpiring(object? sender, SsoSessionExpiringEventArgs e)
    {
        Console.WriteLine($"Session expiring in {e.TimeToExpiry}: {e.Session.SessionId}");
    }

    private void OnSessionRefreshed(object? sender, SsoSessionRefreshedEventArgs e)
    {
        Console.WriteLine($"Session refreshed: {e.Session.SessionId}, new expiry: {e.NewExpiresAt}");
    }

    private void OnSessionExpired(object? sender, SsoSessionExpiredEventArgs e)
    {
        Console.WriteLine($"Session expired: {e.SessionId}");
    }

    private void OnSessionRevoked(object? sender, SsoSessionRevokedEventArgs e)
    {
        Console.WriteLine($"Session revoked: {e.SessionId}, reason: {e.Reason}");
    }
}
```

---

### Credential Storage

Secure credential storage with platform-specific implementations.

#### Providers

| Provider | Platform | Description |
|----------|----------|-------------|
| `Auto` | All | Auto-detect best provider |
| `WindowsCredentialManager` | Windows | Windows Credential Manager with DPAPI |
| `MacOsKeychain` | macOS | macOS Keychain Services (planned) |
| `LinuxSecretService` | Linux | GNOME Keyring/KDE Wallet (planned) |
| `EncryptedFile` | All | AES-256-GCM encrypted JSON file |
| `InMemory` | All | Non-persistent, for testing |

#### Usage

```csharp
public class CredentialService
{
    private readonly ICredentialStore _credentialStore;

    public CredentialService(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    public async Task StoreTokenAsync(string userId, string accessToken, string refreshToken)
    {
        var credential = new SsoCredential
        {
            UserId = userId,
            Type = CredentialType.AccessToken,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            DirectoryType = LdapDirectoryType.ActiveDirectory
        };

        await _credentialStore.StoreCredentialAsync($"token:{userId}", credential);
    }

    public async Task<string?> GetTokenAsync(string userId)
    {
        var credential = await _credentialStore.GetCredentialAsync($"token:{userId}");
        return credential?.AccessToken;
    }

    public async Task RemoveTokenAsync(string userId)
    {
        await _credentialStore.RemoveCredentialAsync($"token:{userId}");
    }

    public async Task<IReadOnlyList<SsoCredential>> GetUserCredentialsAsync(string userId)
    {
        return await _credentialStore.GetUserCredentialsAsync(userId);
    }

    public async Task CleanupExpiredAsync()
    {
        var removed = await _credentialStore.CleanupExpiredAsync();
        Console.WriteLine($"Removed {removed} expired credentials");
    }
}
```

#### Credential Store Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Provider` | CredentialStoreProvider | Auto | Storage provider |
| `ApplicationName` | string | "Toolbox" | Application identifier for credentials |
| `FallbackStorePath` | string? | null | Path for encrypted file store |
| `UseOsKeychain` | bool | true | Prefer OS-native credential storage |

---

### Automatic Token Refresh

The token refresh service automatically refreshes tokens before they expire.

#### Configuration

```csharp
services.AddSsoServices(sso =>
{
    sso.EnableAutoRefresh = true;
    sso.RefreshThreshold = 0.8;          // Refresh at 80% of lifetime
    sso.RefreshCheckInterval = TimeSpan.FromMinutes(1);
    sso.MaxRefreshRetries = 3;
    sso.RefreshRetryDelay = TimeSpan.FromSeconds(5);
    sso.UseExponentialBackoff = true;
});
```

#### Manual Refresh

```csharp
public class TokenService
{
    private readonly ITokenRefreshService _refreshService;
    private readonly ISsoSessionManager _sessionManager;

    public TokenService(ITokenRefreshService refreshService, ISsoSessionManager sessionManager)
    {
        _refreshService = refreshService;
        _sessionManager = sessionManager;
    }

    public async Task<SsoSession?> RefreshNowAsync(string sessionId)
    {
        // Immediate refresh
        return await _refreshService.RefreshNowAsync(sessionId);
    }

    public async Task<int> RefreshAllPendingAsync()
    {
        // Refresh all sessions that need it
        return await _refreshService.RefreshAllPendingAsync();
    }

    public void CheckStatus()
    {
        Console.WriteLine($"Service running: {_refreshService.IsRunning}");
        Console.WriteLine($"Registered sessions: {_refreshService.RegisteredSessionCount}");
        Console.WriteLine($"Successful refreshes: {_refreshService.SuccessfulRefreshCount}");
        Console.WriteLine($"Failed refreshes: {_refreshService.FailedRefreshCount}");
        Console.WriteLine($"Last check: {_refreshService.LastCheckTime}");
        Console.WriteLine($"Next check: {_refreshService.NextCheckTime}");
    }
}
```

#### Refresh Events

```csharp
public class RefreshEventHandler
{
    public RefreshEventHandler(ITokenRefreshService refreshService)
    {
        refreshService.RefreshNeeded += OnRefreshNeeded;
        refreshService.RefreshCompleted += OnRefreshCompleted;
        refreshService.RefreshFailed += OnRefreshFailed;
    }

    private void OnRefreshNeeded(object? sender, TokenRefreshNeededEventArgs e)
    {
        Console.WriteLine($"Refresh needed for {e.Session.SessionId}");
        Console.WriteLine($"  Elapsed: {e.LifetimeElapsedPercent:P0}");
        Console.WriteLine($"  Time to expiry: {e.TimeToExpiry}");
    }

    private void OnRefreshCompleted(object? sender, TokenRefreshCompletedEventArgs e)
    {
        Console.WriteLine($"Refresh completed for {e.SessionId}");
        Console.WriteLine($"  New expiry: {e.NewExpiresAt}");
        Console.WriteLine($"  Duration: {e.RefreshDuration.TotalMilliseconds}ms");
    }

    private void OnRefreshFailed(object? sender, TokenRefreshFailedEventArgs e)
    {
        Console.WriteLine($"Refresh failed for {e.SessionId}");
        Console.WriteLine($"  Error: {e.Exception.Message}");
        Console.WriteLine($"  Retry: {e.RetryAttempt}/{e.MaxRetries}");
        Console.WriteLine($"  Will retry: {e.WillRetry}");
    }
}
```

### SSO Session Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DefaultSessionDuration` | TimeSpan | 8 hours | Default session lifetime |
| `MaxSessionDuration` | TimeSpan | 7 days | Maximum session lifetime |
| `SlidingExpiration` | TimeSpan? | 30 min | Sliding expiration window |
| `RefreshThreshold` | double | 0.8 | Refresh at % of lifetime |
| `RefreshCheckInterval` | TimeSpan | 1 min | Background check interval |
| `EnableAutoRefresh` | bool | true | Enable automatic refresh |
| `PersistSessions` | bool | true | Persist sessions to store |
| `MaxSessionsPerUser` | int | 5 | Max concurrent sessions |
| `RevokeOldestOnMaxReached` | bool | true | Revoke oldest when max reached |
| `EnforceDeviceBinding` | bool | false | Require same device |
| `EnforceIpBinding` | bool | false | Require same IP address |

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
| `toolbox.disposals.count` | Counter | Service disposal events |
| `toolbox.instances.active` | UpDownCounter | Active service instances |
| `toolbox.crypto.encrypt.count` | Counter | Encryption operations |
| `toolbox.crypto.decrypt.count` | Counter | Decryption operations |
| `toolbox.crypto.data.size` | Histogram | Data size (bytes) |
| `toolbox.filetransfer.upload.count` | Counter | File uploads |
| `toolbox.filetransfer.download.count` | Counter | File downloads |
| `toolbox.filetransfer.size` | Histogram | File size (bytes) |
| `toolbox.filetransfer.errors.count` | Counter | File transfer errors |
| `toolbox.mailing.sent.count` | Counter | Emails sent |
| `toolbox.api.requests.count` | Counter | API requests |
| `toolbox.sso.sessions.created` | Counter | SSO sessions created |
| `toolbox.sso.sessions.expired` | Counter | SSO sessions expired |
| `toolbox.sso.sessions.active` | UpDownCounter | Active SSO sessions |
| `toolbox.sso.validations.count` | Counter | Session validations |
| `toolbox.sso.refresh.count` | Counter | Token refreshes |

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
