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
7. [Creating Custom Services](#creating-custom-services)
8. [OpenTelemetry Integration](#opentelemetry-integration)
9. [Configuration Options](#configuration-options)
10. [Best Practices](#best-practices)

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
