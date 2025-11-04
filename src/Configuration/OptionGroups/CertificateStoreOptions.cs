namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using Opc.Ua;
using OpcPlc.Certs;
using OpcPlc.Configuration.Parsers;
using OpcPlc.Configuration.Validators;
using OpcPlc.Helpers;
using System;

/// <summary>
/// Options for certificate store configuration.
/// </summary>
public class CertificateStoreOptions : IOptionGroup
{
    private readonly OpcPlcConfiguration _config;
    private readonly FileExistsValidator _fileValidator;

    public CertificateStoreOptions(OpcPlcConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _fileValidator = new FileExistsValidator();
    }

    public void RegisterOptions(Mono.Options.OptionSet options)
    {
        options.Add(
            "at|appcertstoretype=",
            $"the own application cert store type.\n(allowed values: Directory, X509Store, FlatDirectory)\nDefault: '{_config.OpcUa.OpcOwnCertStoreType}'",
            (s) =>
            {
                switch (s)
                {
                    case CertificateStoreType.X509Store:
                        _config.OpcUa.OpcOwnCertStoreType = CertificateStoreType.X509Store;
                        _config.OpcUa.OpcOwnCertStorePath = _config.OpcUa.OpcOwnCertX509StorePathDefault;
                        break;
                    case CertificateStoreType.Directory:
                        _config.OpcUa.OpcOwnCertStoreType = CertificateStoreType.Directory;
                        _config.OpcUa.OpcOwnCertStorePath = _config.OpcUa.OpcOwnCertDirectoryStorePathDefault;
                        break;
                    case FlatDirectoryCertificateStore.StoreTypeName:
                        _config.OpcUa.OpcOwnCertStoreType = FlatDirectoryCertificateStore.StoreTypeName;
                        _config.OpcUa.OpcOwnCertStorePath = _config.OpcUa.OpcOwnCertDirectoryStorePathDefault;
                        break;
                    default:
                        throw new OptionException($"Invalid certificate store type: {s}", "appcertstoretype");
                }
            });

        options.Add(
            "ap|appcertstorepath=",
            "the path where the own application cert should be stored.\nDefault (depends on store type):\n" +
            $"X509Store: '{_config.OpcUa.OpcOwnCertX509StorePathDefault}'\n" +
            $"Directory: '{_config.OpcUa.OpcOwnCertDirectoryStorePathDefault}'\n" +
            $"FlatDirectory: '{_config.OpcUa.OpcOwnCertDirectoryStorePathDefault}'",
            (s) => _config.OpcUa.OpcOwnCertStorePath = s);

        options.Add(
            "tp|trustedcertstorepath=",
            $"the path of the trusted cert store.\nDefault '{_config.OpcUa.OpcTrustedCertDirectoryStorePathDefault}'",
            (s) => _config.OpcUa.OpcTrustedCertStorePath = s);

        options.Add(
            "rp|rejectedcertstorepath=",
            $"the path of the rejected cert store.\nDefault '{_config.OpcUa.OpcRejectedCertDirectoryStorePathDefault}'",
            (s) => _config.OpcUa.OpcRejectedCertStorePath = s);

        options.Add(
            "ip|issuercertstorepath=",
            $"the path of the trusted issuer cert store.\nDefault '{_config.OpcUa.OpcIssuerCertDirectoryStorePathDefault}'",
            (s) => _config.OpcUa.OpcIssuerCertStorePath = s);

        options.Add(
            "csr",
            $"show data to create a certificate signing request.\nDefault '{_config.OpcUa.ShowCreateSigningRequestInfo}'",
            (s) => _config.OpcUa.ShowCreateSigningRequestInfo = s != null);

        options.Add(
            "ab|applicationcertbase64=",
            "update/set this application's certificate with the certificate passed in as base64 string.",
            (s) => _config.OpcUa.NewCertificateBase64String = s);

        options.Add(
            "af|applicationcertfile=",
            "update/set this application's certificate with the specified file.",
            (s) =>
            {
                _fileValidator.Validate(s, "applicationcertfile");
                _config.OpcUa.NewCertificateFileName = s;
            });

        options.Add(
            "pb|privatekeybase64=",
            "initial provisioning of the application certificate (with a PEM or PFX format) requires a private key passed in as base64 string.",
            (s) => _config.OpcUa.PrivateKeyBase64String = s);

        options.Add(
            "pk|privatekeyfile=",
            "initial provisioning of the application certificate (with a PEM or PFX format) requires a private key passed in as file.",
            (s) =>
            {
                _fileValidator.Validate(s, "privatekeyfile");
                _config.OpcUa.PrivateKeyFileName = s;
            });

        options.Add(
            "cp|certpassword=",
            "the optional password for the PEM or PFX or the installed application certificate.",
            (s) => _config.OpcUa.CertificatePassword = s);

        options.Add(
            "tb|addtrustedcertbase64=",
            "adds the certificate to the application's trusted cert store passed in as base64 string (comma separated values).",
            (s) => _config.OpcUa.TrustedCertificateBase64Strings = StringListParser.Parse(s));

        options.Add(
            "tf|addtrustedcertfile=",
            "adds the certificate file(s) to the application's trusted cert store passed in as base64 string (multiple comma separated filenames supported).",
            (s) => _config.OpcUa.TrustedCertificateFileNames = CliHelper.ParseListOfFileNames(s, "addtrustedcertfile"));

        options.Add(
            "ib|addissuercertbase64=",
            "adds the specified issuer certificate to the application's trusted issuer cert store passed in as base64 string (comma separated values).",
            (s) => _config.OpcUa.IssuerCertificateBase64Strings = StringListParser.Parse(s));

        options.Add(
            "if|addissuercertfile=",
            "adds the specified issuer certificate file(s) to the application's trusted issuer cert store (multiple comma separated filenames supported).",
            (s) => _config.OpcUa.IssuerCertificateFileNames = CliHelper.ParseListOfFileNames(s, "addissuercertfile"));

        options.Add(
            "rb|updatecrlbase64=",
            "update the CRL passed in as base64 string to the corresponding cert store (trusted or trusted issuer).",
            (s) => _config.OpcUa.CrlBase64String = s);

        options.Add(
            "uc|updatecrlfile=",
            "update the CRL passed in as file to the corresponding cert store (trusted or trusted issuer).",
            (s) =>
            {
                _fileValidator.Validate(s, "updatecrlfile");
                _config.OpcUa.CrlFileName = s;
            });

        options.Add(
            "rc|removecert=",
            "remove cert(s) with the given thumbprint(s) (comma separated values).",
            (s) => _config.OpcUa.ThumbprintsToRemove = StringListParser.Parse(s));

        options.Add(
            "cdn|certdnsnames=",
            "add additional DNS names or IP addresses to this application's certificate (comma separated values; no spaces allowed).\nDefault: DNS hostname",
            (s) => _config.OpcUa.DnsNames = StringListParser.Parse(s));
    }
}
