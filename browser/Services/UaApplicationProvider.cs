namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Configuration;

/// <summary>
/// Builds and caches the OPC UA client application configuration,
/// including the client application instance certificate.
/// </summary>
public sealed class UaApplicationProvider
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ApplicationConfiguration? _configuration;

    /// <summary>
    /// Root directory for the client PKI stores.
    /// </summary>
    public static string PkiRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UaScope",
        "pki");

    /// <summary>
    /// Create (once) and return the client application configuration.
    /// </summary>
    public async Task<ApplicationConfiguration> GetConfigurationAsync()
    {
        if (_configuration is not null)
        {
            return _configuration;
        }

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_configuration is not null)
            {
                return _configuration;
            }

            var config = new ApplicationConfiguration
            {
                ApplicationName = "UaScope",
                ApplicationUri = $"urn:{Utils.GetHostName()}:UaScope",
                ProductUri = "urn:uascope:opcua:browser",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(PkiRoot, "own"),
                        SubjectName = $"CN=UaScope, DC={Utils.GetHostName()}",
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(PkiRoot, "issuer"),
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(PkiRoot, "trusted"),
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.Combine(PkiRoot, "rejected"),
                    },
                    // Trust decisions are made per connection in UaConnection.
                    AutoAcceptUntrustedCertificates = false,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024,
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 60_000,
                    MaxStringLength = 4 * 1024 * 1024,
                    MaxByteStringLength = 4 * 1024 * 1024,
                    MaxArrayLength = 65_535,
                    MaxMessageSize = 16 * 1024 * 1024,
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60_000,
                    MinSubscriptionLifetime = 10_000,
                },
                CertificateValidator = new CertificateValidator(),
            };

            await config.Validate(ApplicationType.Client).ConfigureAwait(false);

            var application = new ApplicationInstance
            {
                ApplicationName = config.ApplicationName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config,
            };

            // Creates a self-signed client certificate on first run.
            bool haveCertificate = await application.CheckApplicationInstanceCertificates(silent: true).ConfigureAwait(false);
            if (!haveCertificate)
            {
                throw new InvalidOperationException("Could not create or load the UaScope client application certificate.");
            }

            _configuration = config;
            return _configuration;
        }
        finally
        {
            _lock.Release();
        }
    }
}
