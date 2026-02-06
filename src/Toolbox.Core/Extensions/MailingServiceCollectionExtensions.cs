// @file MailingServiceCollectionExtensions.cs
// @brief DI extensions for mailing services
// @details Provides extension methods to register mailing services
// @note Includes configuration via IOptions pattern

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Options;
using Toolbox.Core.Services.Mailing;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for registering mailing services with dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// These extensions simplify the registration of mailing services
/// with the Microsoft.Extensions.DependencyInjection container.
/// </para>
/// </remarks>
public static class MailingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SMTP mailing service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure mailing options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// services.AddSmtpMailing(options =>
    /// {
    ///     options.Host = "smtp.example.com";
    ///     options.Port = 587;
    ///     options.SecurityMode = SmtpSecurityMode.StartTls;
    ///     options.Username = "user@example.com";
    ///     options.Password = "password";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSmtpMailing(
        this IServiceCollection services,
        Action<MailingOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<IMailingService, SmtpMailingService>();

        return services;
    }

    /// <summary>
    /// Adds the SMTP mailing service to the service collection with a configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing mailing options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // {
    /// //   "Mailing": {
    /// //     "Host": "smtp.example.com",
    /// //     "Port": 587,
    /// //     "SecurityMode": "StartTls",
    /// //     "Username": "user@example.com",
    /// //     "Password": "password"
    /// //   }
    /// // }
    ///
    /// services.AddSmtpMailing(configuration.GetSection("Mailing"));
    /// </code>
    /// </example>
    public static IServiceCollection AddSmtpMailing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<MailingOptions>(configuration);
        services.TryAddSingleton<IMailingService, SmtpMailingService>();

        return services;
    }

    /// <summary>
    /// Adds the SMTP mailing service to the service collection with pre-configured options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The mailing options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddSmtpMailing(
        this IServiceCollection services,
        MailingOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<IMailingService, SmtpMailingService>();

        return services;
    }

    /// <summary>
    /// Adds the SMTP mailing service with Gmail configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="email">The Gmail email address.</param>
    /// <param name="appPassword">The Gmail app password (not the regular password).</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// To use Gmail, you need to create an App Password:
    /// https://support.google.com/accounts/answer/185833
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGmailMailing(
        this IServiceCollection services,
        string email,
        string appPassword)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(appPassword);

        return services.AddSmtpMailing(options =>
        {
            options.Host = "smtp.gmail.com";
            options.Port = 587;
            options.SecurityMode = SmtpSecurityMode.StartTls;
            options.Username = email;
            options.Password = appPassword;
            options.DefaultFrom = new EmailAddress(email);
        });
    }

    /// <summary>
    /// Adds the SMTP mailing service with Office 365 / Outlook configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="email">The Microsoft email address.</param>
    /// <param name="password">The account password or app password.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters are <c>null</c>.</exception>
    public static IServiceCollection AddOutlookMailing(
        this IServiceCollection services,
        string email,
        string password)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        return services.AddSmtpMailing(options =>
        {
            options.Host = "smtp-mail.outlook.com";
            options.Port = 587;
            options.SecurityMode = SmtpSecurityMode.StartTls;
            options.Username = email;
            options.Password = password;
            options.DefaultFrom = new EmailAddress(email);
        });
    }
}
