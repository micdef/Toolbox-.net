// @file TelemetryConstants.cs
// @brief Constants for OpenTelemetry instrumentation
// @details Defines standard names and versions for telemetry sources
// @note These constants ensure consistent naming across all instrumented services

namespace Toolbox.Core.Telemetry;

/// <summary>
/// Constants used for OpenTelemetry instrumentation across Toolbox services.
/// </summary>
/// <remarks>
/// Using consistent names for activity sources and meters ensures proper
/// correlation and filtering in observability backends.
/// </remarks>
public static class TelemetryConstants
{
    /// <summary>
    /// The name of the root activity source for Toolbox services.
    /// </summary>
    public const string ActivitySourceName = "Toolbox.Core";

    /// <summary>
    /// The name of the root meter for Toolbox metrics.
    /// </summary>
    public const string MeterName = "Toolbox.Core";

    /// <summary>
    /// The current version of the telemetry instrumentation.
    /// </summary>
    /// <remarks>
    /// This version should be updated when breaking changes are made to
    /// telemetry semantics (attribute names, metric units, etc.).
    /// </remarks>
    public const string Version = "1.0.0";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public const string ActivitySourceVersion = "1.0.0";

    /// <summary>
    /// Standard attribute names for service telemetry.
    /// </summary>
    public static class Attributes
    {
        /// <summary>Attribute name for the service name.</summary>
        public const string ServiceName = "toolbox.service.name";

        /// <summary>Attribute name for operation names.</summary>
        public const string OperationName = "toolbox.operation.name";

        /// <summary>Attribute name for error types.</summary>
        public const string ErrorType = "toolbox.error.type";

        /// <summary>Attribute name for disposal reasons.</summary>
        public const string DisposalReason = "toolbox.disposal.reason";
    }

    /// <summary>
    /// Standard metric names for service instrumentation.
    /// </summary>
    public static class Metrics
    {
        /// <summary>Counter for service operations.</summary>
        public const string OperationCount = "toolbox.operations.count";

        /// <summary>Histogram for operation duration.</summary>
        public const string OperationDuration = "toolbox.operations.duration";

        /// <summary>Counter for disposal events.</summary>
        public const string DisposalCount = "toolbox.disposals.count";

        /// <summary>Gauge for active service instances.</summary>
        public const string ActiveInstances = "toolbox.instances.active";

        /// <summary>Counter for cryptography encrypt operations.</summary>
        public const string CryptoEncryptCount = "toolbox.crypto.encrypt.count";

        /// <summary>Counter for cryptography decrypt operations.</summary>
        public const string CryptoDecryptCount = "toolbox.crypto.decrypt.count";

        /// <summary>Histogram for encrypted data size in bytes.</summary>
        public const string CryptoDataSize = "toolbox.crypto.data.size";

        /// <summary>Counter for file transfer upload operations.</summary>
        public const string FileTransferUploadCount = "toolbox.filetransfer.upload.count";

        /// <summary>Counter for file transfer download operations.</summary>
        public const string FileTransferDownloadCount = "toolbox.filetransfer.download.count";

        /// <summary>Histogram for file transfer size in bytes.</summary>
        public const string FileTransferSize = "toolbox.filetransfer.size";

        /// <summary>Counter for file transfer errors.</summary>
        public const string FileTransferErrorCount = "toolbox.filetransfer.errors.count";

        /// <summary>Counter for email sent operations.</summary>
        public const string EmailSentCount = "toolbox.mailing.sent.count";

        /// <summary>Counter for API request operations.</summary>
        public const string ApiRequestCount = "toolbox.api.requests.count";

        /// <summary>Counter for LDAP query operations.</summary>
        public const string LdapQueryCount = "toolbox.ldap.queries.count";
    }

    /// <summary>
    /// Additional attribute names for specific service categories.
    /// </summary>
    public static class CryptoAttributes
    {
        /// <summary>Attribute name for encryption algorithm.</summary>
        public const string Algorithm = "toolbox.crypto.algorithm";

        /// <summary>Attribute name for key size in bits.</summary>
        public const string KeySize = "toolbox.crypto.keysize";

        /// <summary>Attribute name for padding mode.</summary>
        public const string PaddingMode = "toolbox.crypto.padding";
    }

    /// <summary>
    /// Additional attribute names for file transfer services.
    /// </summary>
    public static class FileTransferAttributes
    {
        /// <summary>Attribute name for transfer protocol.</summary>
        public const string Protocol = "toolbox.filetransfer.protocol";

        /// <summary>Attribute name for remote host.</summary>
        public const string Host = "toolbox.filetransfer.host";

        /// <summary>Attribute name for transfer direction.</summary>
        public const string Direction = "toolbox.filetransfer.direction";

        /// <summary>Attribute name for file path.</summary>
        public const string FilePath = "toolbox.filetransfer.path";

        /// <summary>Attribute name for file count in batch operations.</summary>
        public const string FileCount = "toolbox.filetransfer.filecount";
    }

    /// <summary>
    /// Additional attribute names for mailing services.
    /// </summary>
    public static class MailingAttributes
    {
        /// <summary>Attribute name for SMTP host.</summary>
        public const string Host = "toolbox.mailing.host";

        /// <summary>Attribute name for recipient count.</summary>
        public const string Recipients = "toolbox.mailing.recipients";

        /// <summary>Attribute name for attachment indicator.</summary>
        public const string HasAttachments = "toolbox.mailing.has_attachments";

        /// <summary>Attribute name for HTML body indicator.</summary>
        public const string IsHtml = "toolbox.mailing.is_html";
    }

    /// <summary>
    /// Additional attribute names for API services.
    /// </summary>
    public static class ApiAttributes
    {
        /// <summary>Attribute name for HTTP method.</summary>
        public const string HttpMethod = "toolbox.api.http_method";

        /// <summary>Attribute name for HTTP status code.</summary>
        public const string HttpStatusCode = "toolbox.api.status_code";

        /// <summary>Attribute name for success indicator.</summary>
        public const string HttpSuccess = "toolbox.api.success";
    }

    /// <summary>
    /// Additional attribute names for LDAP services.
    /// </summary>
    public static class LdapAttributes
    {
        /// <summary>Attribute name for directory type.</summary>
        public const string DirectoryType = "toolbox.ldap.directory_type";

        /// <summary>Attribute name for LDAP host.</summary>
        public const string Host = "toolbox.ldap.host";

        /// <summary>Attribute name for query success.</summary>
        public const string QuerySuccess = "toolbox.ldap.query_success";

        /// <summary>Attribute name for user found.</summary>
        public const string UserFound = "toolbox.ldap.user_found";

        /// <summary>Attribute name for object type (user, group, computer).</summary>
        public const string ObjectType = "toolbox.ldap.object_type";

        /// <summary>Attribute name for result count.</summary>
        public const string ResultCount = "toolbox.ldap.result_count";

        /// <summary>Attribute name for page number.</summary>
        public const string PageNumber = "toolbox.ldap.page_number";

        /// <summary>Attribute name for page size.</summary>
        public const string PageSize = "toolbox.ldap.page_size";

        /// <summary>Attribute name for authentication success.</summary>
        public const string AuthSuccess = "toolbox.ldap.auth_success";
    }

    /// <summary>
    /// LDAP-specific metric names.
    /// </summary>
    public static class LdapMetrics
    {
        /// <summary>Counter for LDAP connection operations.</summary>
        public const string ConnectionCount = "toolbox.ldap.connections.count";

        /// <summary>Counter for LDAP authentication operations.</summary>
        public const string AuthenticationCount = "toolbox.ldap.auth.count";

        /// <summary>Histogram for LDAP query result count.</summary>
        public const string QueryResultCount = "toolbox.ldap.query.result_count";

        /// <summary>Counter for LDAP errors.</summary>
        public const string ErrorCount = "toolbox.ldap.errors.count";

        /// <summary>Histogram for LDAP query duration.</summary>
        public const string QueryDuration = "toolbox.ldap.query.duration";
    }

    /// <summary>
    /// Additional attribute names for SSO services.
    /// </summary>
    public static class SsoAttributes
    {
        /// <summary>Attribute name for directory type.</summary>
        public const string DirectoryType = "toolbox.sso.directory_type";

        /// <summary>Attribute name for user ID.</summary>
        public const string UserId = "toolbox.sso.user_id";

        /// <summary>Attribute name for session ID.</summary>
        public const string SessionId = "toolbox.sso.session_id";

        /// <summary>Attribute name for validation success.</summary>
        public const string ValidationSuccess = "toolbox.sso.validation_success";

        /// <summary>Attribute name for refresh success.</summary>
        public const string RefreshSuccess = "toolbox.sso.refresh_success";

        /// <summary>Attribute name for failure reason.</summary>
        public const string FailureReason = "toolbox.sso.failure_reason";
    }

    /// <summary>
    /// SSO-specific metric names.
    /// </summary>
    public static class SsoMetrics
    {
        /// <summary>Counter for SSO sessions created.</summary>
        public const string SessionCreatedCount = "toolbox.sso.sessions.created";

        /// <summary>Counter for SSO session validations.</summary>
        public const string ValidationCount = "toolbox.sso.validations.count";

        /// <summary>Counter for SSO session refreshes.</summary>
        public const string RefreshCount = "toolbox.sso.refresh.count";

        /// <summary>Counter for SSO sessions expired.</summary>
        public const string ExpiredCount = "toolbox.sso.sessions.expired";

        /// <summary>Gauge for active SSO sessions.</summary>
        public const string ActiveSessionCount = "toolbox.sso.sessions.active";

        /// <summary>Histogram for SSO session duration.</summary>
        public const string SessionDuration = "toolbox.sso.session.duration";
    }
}
