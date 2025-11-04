namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using OpcPlc.Configuration.Validators;
using System;

/// <summary>
/// Options for OPC UA server configuration.
/// </summary>
public class OpcUaServerOptions : IOptionGroup
{
    private readonly OpcPlcConfiguration _config;

    public OpcUaServerOptions(OpcPlcConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RegisterOptions(OptionSet options)
    {
        var positiveIntValidator = new PositiveNumberValidator<int>(0);
        var nonNegativeIntValidator = new NonNegativeNumberValidator<int>(0);

        options.Add(
            "pn|portnum=",
            $"the server port of the OPC server endpoint.\nDefault: {_config.OpcUa.ServerPort}",
            (ushort i) => _config.OpcUa.ServerPort = i);

        options.Add(
            "op|path=",
            $"the endpoint URL path part of the OPC server endpoint.\nDefault: '{_config.OpcUa.ServerPath}'",
            (s) => _config.OpcUa.ServerPath = s);

        options.Add(
            "ph|plchostname=",
            $"the fully-qualified hostname of the PLC.\nDefault: {_config.OpcUa.Hostname}",
            (s) => _config.OpcUa.Hostname = s);

        options.Add(
            "ol|opcmaxstringlen=",
            $"the max length of a string OPC can transmit/receive.\nDefault: {_config.OpcUa.OpcMaxStringLength}",
            (int i) =>
            {
                positiveIntValidator.Validate(i, "opcmaxstringlen");
                _config.OpcUa.OpcMaxStringLength = i;
            });

        options.Add(
            "lr|ldsreginterval=",
            $"the LDS(-ME) registration interval in ms. If 0, then the registration is disabled.\nDefault: {_config.OpcUa.LdsRegistrationInterval}",
            (int i) =>
            {
                nonNegativeIntValidator.Validate(i, "ldsreginterval");
                _config.OpcUa.LdsRegistrationInterval = i;
            });

        options.Add(
            "aa|autoaccept",
            $"all certs are trusted when a connection is established.\nDefault: {_config.OpcUa.AutoAcceptCerts}",
            (s) => _config.OpcUa.AutoAcceptCerts = s != null);

        options.Add(
            "drurs|dontrejectunknownrevocationstatus",
            $"Don't reject chain validation with CA certs with unknown revocation status, e.g. when the CRL is not available or the OCSP provider is offline.\nDefault: {_config.OpcUa.DontRejectUnknownRevocationStatus}",
            (s) => _config.OpcUa.DontRejectUnknownRevocationStatus = s != null);

        options.Add(
            "ut|unsecuretransport",
            $"enables the unsecured transport.\nDefault: {_config.OpcUa.EnableUnsecureTransport}",
            (s) => _config.OpcUa.EnableUnsecureTransport = s != null);

        options.Add(
            "to|trustowncert",
            $"the own certificate is put into the trusted certificate store automatically.\nDefault: {_config.OpcUa.TrustMyself}",
            (s) => _config.OpcUa.TrustMyself = s != null);

        options.Add(
            "msec|maxsessioncount=",
            $"maximum number of parallel sessions.\nDefault: {_config.OpcUa.MaxSessionCount}",
            (int i) => _config.OpcUa.MaxSessionCount = i);

        options.Add(
            "mset|maxsessiontimeout=",
            $"maximum time that a session can remain open without communication in milliseconds.\nDefault: {_config.OpcUa.MaxSessionTimeout}",
            (int i) => _config.OpcUa.MaxSessionTimeout = i);

        options.Add(
            "msuc|maxsubscriptioncount=",
            $"maximum number of subscriptions.\nDefault: {_config.OpcUa.MaxSubscriptionCount}",
            (int i) => _config.OpcUa.MaxSubscriptionCount = i);

        options.Add(
            "mqrc|maxqueuedrequestcount=",
            $"maximum number of requests that will be queued waiting for a thread.\nDefault: {_config.OpcUa.MaxQueuedRequestCount}",
            (int i) => _config.OpcUa.MaxQueuedRequestCount = i);
    }
}
