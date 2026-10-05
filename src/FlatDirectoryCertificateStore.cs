namespace OpcPlc.Certs;

using Opc.Ua;
using Opc.Ua.Security.Certificates;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Flat directory certificate store that does not have internal
/// hierarchy with certs/crl/private subdirectories.
/// </summary>
public sealed class FlatDirectoryCertificateStore : ICertificateStore
{
    private const string CrtExtension = ".crt";
    private const string KeyExtension = ".key";

    private readonly DirectoryCertificateStore _innerStore;

    /// <summary>
    /// Identifier for flat directory certificate store.
    /// </summary>
    public const string StoreTypeName = "FlatDirectory";

    /// <summary>
    /// Prefix for flat directory certificate store.
    /// </summary>
    public const string StoreTypePrefix = $"{StoreTypeName}:";

    /// <summary>
    /// Initializes a new instance of the <see cref="FlatDirectoryCertificateStore"/> class.
    /// </summary>
    public FlatDirectoryCertificateStore(ITelemetryContext telemetry) => _innerStore = new DirectoryCertificateStore(noSubDirs: true, telemetry);

    /// <inheritdoc/>
    public string StoreType => StoreTypeName;

    /// <inheritdoc/>
    public string StorePath => _innerStore.StorePath;

    /// <inheritdoc/>
    public bool SupportsLoadPrivateKey => _innerStore.SupportsLoadPrivateKey;

    /// <inheritdoc/>
    public bool SupportsCRLs => _innerStore.SupportsCRLs;

    /// <inheritdoc/>
    public bool NoPrivateKeys => _innerStore.NoPrivateKeys;

    public void Dispose() => _innerStore.Dispose();

    public void Open(string location, bool noPrivateKeys = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(location);
        if (!location.StartsWith(StoreTypePrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Expected argument {nameof(location)} starting with {StoreTypePrefix}",
                nameof(location));
        }
        _innerStore.Open(location.Substring(StoreTypePrefix.Length), noPrivateKeys);
    }

    public void Close() => _innerStore.Close();

    public Task AddAsync(X509Certificate2 certificate, char[] password, CancellationToken ct) => _innerStore.AddAsync(certificate, password, ct);
    public Task AddRejectedAsync(X509Certificate2Collection certificates, int maxCertificates, CancellationToken ct) => _innerStore.AddRejectedAsync(certificates, maxCertificates, ct);
    public Task<bool> DeleteAsync(string thumbprint, CancellationToken ct) => _innerStore.DeleteAsync(thumbprint, ct);
    public async Task<X509Certificate2Collection> EnumerateAsync(CancellationToken ct)
    {
        var certificatesCollection = await _innerStore.EnumerateAsync(ct).ConfigureAwait(false);

        if (ct.IsCancellationRequested || !_innerStore.Directory.Exists) { return certificatesCollection; }

        foreach (var filePath in _innerStore.Directory.GetFiles('*' + CrtExtension).Select(f => f.FullName))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var certificates = new X509Certificate2Collection();
                certificates.ImportFromPemFile(filePath);
                certificatesCollection.AddRange(certificates);
            }
            catch (Exception e)
            {
                Utils.LogError(e, "Could not load certificate from file: {FileName}", filePath);
            }
        }
        return certificatesCollection;
    }
    public Task AddCRLAsync(X509CRL crl, CancellationToken ct) => _innerStore.AddCRLAsync(crl, ct);
    public Task<bool> DeleteCRLAsync(X509CRL crl, CancellationToken ct) => _innerStore.DeleteCRLAsync(crl, ct);
    public Task<X509CRLCollection> EnumerateCRLsAsync(CancellationToken ct) => _innerStore.EnumerateCRLsAsync(ct);
    public Task<X509CRLCollection> EnumerateCRLsAsync(X509Certificate2 issuer, bool validateUpdateTime, CancellationToken ct) => _innerStore.EnumerateCRLsAsync(issuer, validateUpdateTime, ct);
    public async Task<X509Certificate2Collection> FindByThumbprintAsync(string thumbprint, CancellationToken ct)
    {
        var certificatesCollection = await _innerStore.FindByThumbprintAsync(thumbprint, ct).ConfigureAwait(false);

        if (ct.IsCancellationRequested || !_innerStore.Directory.Exists) { return certificatesCollection; }

        foreach (var filePath in _innerStore.Directory.GetFiles('*' + CrtExtension).Select(f => f.FullName))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var certificates = new X509Certificate2Collection();
                certificates.ImportFromPemFile(filePath);
                foreach (var certificate in certificates.Cast<X509Certificate2>().Where(c => string.Equals(c.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)))
                {
                    certificatesCollection.Add(certificate);
                }
            }
            catch (Exception e)
            {
                Utils.LogError(e, "Could not load certificate from file: {FileName}", filePath);
            }
        }
        return certificatesCollection;
    }
    public Task<StatusCode> IsRevokedAsync(X509Certificate2 issuer, X509Certificate2 certificate, CancellationToken ct) => _innerStore.IsRevokedAsync(issuer, certificate, ct);
    public async Task<X509Certificate2> LoadPrivateKeyAsync(string thumbprint, string subjectName, string applicationUri, NodeId certificateType, char[] password, CancellationToken ct)
    {
        if (!_innerStore.Directory.Exists)
        {
            return await _innerStore.LoadPrivateKeyAsync(thumbprint, subjectName, applicationUri, certificateType, password, ct).ConfigureAwait(false);
        }
        foreach (var filePath in _innerStore.Directory.GetFiles('*' + CrtExtension).Select(f => f.FullName))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var keyFilePath = filePath.Replace(CrtExtension, KeyExtension, StringComparison.OrdinalIgnoreCase);
                if (!File.Exists(keyFilePath)) continue;
                using var certificate = X509CertificateLoader.LoadCertificateFromFile(filePath);
                if (!MatchCertificate(certificate, thumbprint, subjectName, certificateType)) continue;
                return X509Certificate2.CreateFromPemFile(filePath, keyFilePath);
            }
            catch (Exception e)
            {
                Utils.LogError(e, "Could not load private key for certificate file: {FileName}", filePath);
            }
        }

        return await _innerStore.LoadPrivateKeyAsync(thumbprint, subjectName, applicationUri, certificateType, password, ct).ConfigureAwait(false);
    }

    private static bool MatchCertificate(X509Certificate2 certificate, string thumbprint, string subjectName, NodeId certificateType)
    {
        if (certificateType == null || certificateType == ObjectTypeIds.RsaSha256ApplicationCertificateType || certificateType == ObjectTypeIds.RsaMinApplicationCertificateType || certificateType == ObjectTypeIds.ApplicationCertificateType)
        {
            if (!string.IsNullOrEmpty(thumbprint) && !string.Equals(certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(subjectName) && !X509Utils.CompareDistinguishedName(subjectName, certificate.Subject) && (subjectName.Contains('=', StringComparison.OrdinalIgnoreCase) || !X509Utils.ParseDistinguishedName(certificate.Subject).Any(s => s.Equals("CN=" + subjectName, StringComparison.Ordinal)))) return false;
            return X509Utils.GetRSAPublicKeySize(certificate) >= 0;
        }

        return false;
    }
}
