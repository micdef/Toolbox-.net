// @file FileTransferServiceCollectionExtensions.cs
// @brief Extension methods for registering file transfer services
// @details Provides fluent API for adding FTP/SFTP services to DI
// @note Use AddFtpFileTransfer() or AddSftpFileTransfer() to register services

using Microsoft.Extensions.Configuration;
using Toolbox.Core.Abstractions.Services;
using Toolbox.Core.Options;
using Toolbox.Core.Services.FileTransfer;

namespace Toolbox.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to configure file transfer services.
/// </summary>
/// <remarks>
/// These extensions provide a fluent API for registering FTP and SFTP file transfer services.
/// </remarks>
public static class FileTransferServiceCollectionExtensions
{
    /// <summary>
    /// Adds the FTP file transfer service with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddFtpFileTransfer(options =>
    /// {
    ///     options.Host = "ftp.example.com";
    ///     options.Username = "user";
    ///     options.Password = "password";
    ///     options.Protocol = FileTransferProtocol.Ftps;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFtpFileTransfer(
        this IServiceCollection services,
        Action<FileTransferOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.AddScoped<IFileTransferService, FtpFileTransferService>();

        return services;
    }

    /// <summary>
    /// Adds the FTP file transfer service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Binds options from the <c>Toolbox:FileTransfer</c> configuration section.
    /// </remarks>
    public static IServiceCollection AddFtpFileTransfer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FileTransferOptions>(
            configuration.GetSection(FileTransferOptions.SectionName));

        services.AddScoped<IFileTransferService, FtpFileTransferService>();

        return services;
    }

    /// <summary>
    /// Adds the SFTP file transfer service with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddSftpFileTransfer(options =>
    /// {
    ///     options.Host = "sftp.example.com";
    ///     options.Username = "user";
    ///     options.Password = "password";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSftpFileTransfer(
        this IServiceCollection services,
        Action<FileTransferOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.AddScoped<IFileTransferService, SftpFileTransferService>();

        return services;
    }

    /// <summary>
    /// Adds the SFTP file transfer service with configuration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Binds options from the <c>Toolbox:FileTransfer</c> configuration section.
    /// </remarks>
    public static IServiceCollection AddSftpFileTransfer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FileTransferOptions>(
            configuration.GetSection(FileTransferOptions.SectionName));

        services.AddScoped<IFileTransferService, SftpFileTransferService>();

        return services;
    }

    /// <summary>
    /// Adds a file transfer service based on the protocol specified in options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Automatically selects FTP or SFTP service based on the <see cref="FileTransferOptions.Protocol"/> setting.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddFileTransfer(options =>
    /// {
    ///     options.Host = "server.example.com";
    ///     options.Username = "user";
    ///     options.Password = "password";
    ///     options.Protocol = FileTransferProtocol.Sftp; // Will use SFTP service
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFileTransfer(
        this IServiceCollection services,
        Action<FileTransferOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new FileTransferOptions();
        configureOptions(options);

        services.Configure(configureOptions);

        if (options.Protocol == FileTransferProtocol.Sftp)
        {
            services.AddScoped<IFileTransferService, SftpFileTransferService>();
        }
        else
        {
            services.AddScoped<IFileTransferService, FtpFileTransferService>();
        }

        return services;
    }
}
