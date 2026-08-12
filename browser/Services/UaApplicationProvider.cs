namespace DruOpc.Services;

using Opc.Ua;
using Opc.Ua.Configuration;

/// <summary>
/// Builds OPC UA client application configurations. The client application
/// certificate is created/validated once per process; each circuit gets its
/// OWN configuration instance so certificate-validation decisions of one
/// browser tab never leak into another.
/// </summary>
public sealed class UaApplicationProvider
{
    private static readonly SemaphoreSlim _certificateLock = new(1, 1);
    private static bool _certificateChecked;

    /// <summary>
    /// Root directory for the client PKI stores.
    /// </summary>
    public static string PkiRoot { get; } = Path.Combine(
        // SpecialFolderOption.Create: with the default option the folder resolves
        // to "" when ~/.local/share does not exist (e.g. in containers), which
        // silently turns PkiRoot into a CWD-relative path.
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create),
        "DruOPC",
        "pki");

    /// <summary>
    /// Create a new client application configuration (one per circuit).
    /// </summary>
    public async Task<ApplicationConfiguration> CreateConfigurationAsync()
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = "DruOPC",
            ApplicationUri = $"urn:{Utils.GetHostName()}:DruOPC",
            ProductUri = "urn:druopc:opcua:browser",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(PkiRoot, "own"),
                    SubjectName = $"CN=DruOPC, DC={Utils.GetHostName()}",
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

        await EnsureClientCertificateAsync(config).ConfigureAwait(false);

        return config;
    }

    private static async Task EnsureClientCertificateAsync(ApplicationConfiguration config)
    {
        if (_certificateChecked)
        {
            // The certificate exists on disk; load it into this configuration.
            await config.SecurityConfiguration.ApplicationCertificate
                .Find(needPrivateKey: true).ConfigureAwait(false);
            return;
        }

        await _certificateLock.WaitAsync().ConfigureAwait(false);
        try
        {
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
                throw new InvalidOperationException("Could not create or load the DruOPC client application certificate.");
            }

            _certificateChecked = true;
        }
        finally
        {
            _certificateLock.Release();
        }
    }
}
