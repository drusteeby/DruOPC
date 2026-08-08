namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Manages the OPC UA session for one browser circuit:
/// discovery, connect/disconnect, certificate trust and automatic reconnect.
/// </summary>
public sealed class UaConnection : IAsyncDisposable
{
    private readonly UaApplicationProvider _applicationProvider;
    private readonly ILogger<UaConnection> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private SessionReconnectHandler? _reconnectHandler;
    private bool _autoAccept = true;
    private string _lastCertificateError = "";

    public UaConnection(UaApplicationProvider applicationProvider, ILogger<UaConnection> logger)
    {
        _applicationProvider = applicationProvider;
        _logger = logger;
    }

    /// <summary>The active session, if connected.</summary>
    public Session? Session { get; private set; }

    public UaConnectionState State { get; private set; } = UaConnectionState.Disconnected;

    public string? LastError { get; private set; }

    public UaSessionInfo? SessionInfo { get; private set; }

    /// <summary>Raised on any connection state change. May fire on a background thread.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Whether untrusted server certificates are accepted automatically.
    /// </summary>
    public bool AutoAcceptServerCertificate
    {
        get => _autoAccept;
        set => _autoAccept = value;
    }

    /// <summary>
    /// Discover the endpoints offered by a server.
    /// </summary>
    public async Task<List<UaEndpointInfo>> DiscoverEndpointsAsync(string discoveryUrl, CancellationToken ct = default)
    {
        var config = await _applicationProvider.GetConfigurationAsync().ConfigureAwait(false);

        var uri = new Uri(discoveryUrl);
        var endpointConfiguration = EndpointConfiguration.Create(config);
        endpointConfiguration.OperationTimeout = 15_000;

        using var client = DiscoveryClient.Create(uri, endpointConfiguration);
        var endpoints = await client.GetEndpointsAsync(null, ct).ConfigureAwait(false);

        var result = new List<UaEndpointInfo>();
        foreach (var endpoint in endpoints)
        {
            var tokenTypes = endpoint.UserIdentityTokens
                .Select(t => t.TokenType.ToString())
                .Distinct()
                .ToArray();

            result.Add(new UaEndpointInfo(
                endpoint.EndpointUrl,
                endpoint.SecurityPolicyUri,
                endpoint.SecurityMode,
                endpoint.SecurityLevel,
                tokenTypes,
                endpoint));
        }

        return result
            .OrderByDescending(e => e.SecurityLevel)
            .ToList();
    }

    /// <summary>
    /// Connect to a server. When <paramref name="endpoint"/> is null the best
    /// endpoint is selected automatically based on <paramref name="useSecurity"/>.
    /// </summary>
    public async Task ConnectAsync(
        string endpointUrl,
        bool useSecurity,
        IUserIdentity identity,
        EndpointDescription? endpoint = null,
        CancellationToken ct = default)
    {
        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync().ConfigureAwait(false);

            SetState(UaConnectionState.Connecting, error: null);

            var config = await _applicationProvider.GetConfigurationAsync().ConfigureAwait(false);

            // Per-connection certificate trust decision.
            _lastCertificateError = "";
            config.CertificateValidator.CertificateValidation -= OnCertificateValidation;
            config.CertificateValidator.CertificateValidation += OnCertificateValidation;

            EndpointDescription description = endpoint
                ?? CoreClientUtils.SelectEndpoint(config, endpointUrl, useSecurity, discoverTimeout: 15_000);

            var endpointConfiguration = EndpointConfiguration.Create(config);
            var configuredEndpoint = new ConfiguredEndpoint(collection: null, description, endpointConfiguration);

            try
            {
                Session = await Opc.Ua.Client.Session.Create(
                    config,
                    reverseConnectManager: null,
                    configuredEndpoint,
                    updateBeforeConnect: false,
                    checkDomain: false,
                    sessionName: $"UaScope {Environment.MachineName}",
                    sessionTimeout: 60_000,
                    identity,
                    preferredLocales: null,
                    ct).ConfigureAwait(false);
            }
            catch (ServiceResultException sre) when (sre.StatusCode == StatusCodes.BadCertificateUntrusted
                || !string.IsNullOrEmpty(_lastCertificateError))
            {
                throw new InvalidOperationException(
                    $"The server certificate was rejected ({_lastCertificateError}). " +
                    "Enable 'auto-accept server certificate' to trust it.", sre);
            }

            Session.KeepAlive += OnKeepAlive;
            Session.DeleteSubscriptionsOnClose = true;

            BuildSessionInfo(description, identity);
            SetState(UaConnectionState.Connected, error: null);

            _logger.LogInformation("Connected to {EndpointUrl}", description.EndpointUrl);
        }
        catch (Exception ex)
        {
            Session?.Dispose();
            Session = null;
            SessionInfo = null;
            SetState(UaConnectionState.Disconnected, error: Describe(ex));
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Close the session.
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync().ConfigureAwait(false);
            SetState(UaConnectionState.Disconnected, error: null);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task DisconnectCoreAsync()
    {
        _reconnectHandler?.Dispose();
        _reconnectHandler = null;

        if (Session is not null)
        {
            Session.KeepAlive -= OnKeepAlive;
            try
            {
                await Session.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error closing session");
            }

            Session.Dispose();
            Session = null;
        }

        SessionInfo = null;
    }

    private void OnCertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
    {
        if (_autoAccept && e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
        {
            e.Accept = true;
            return;
        }

        _lastCertificateError = e.Error.ToString();
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (!ReferenceEquals(session, Session))
        {
            return;
        }

        if (ServiceResult.IsBad(e.Status))
        {
            if (_reconnectHandler is null)
            {
                _logger.LogWarning("Connection lost ({Status}), reconnecting ...", e.Status);
                SetState(UaConnectionState.Reconnecting, error: e.Status.ToString());

                _reconnectHandler = new SessionReconnectHandler(reconnectAbort: true);
                _reconnectHandler.BeginReconnect(Session, reconnectPeriod: 5_000, OnReconnectComplete);
            }
        }
    }

    private void OnReconnectComplete(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _reconnectHandler))
        {
            return;
        }

        if (_reconnectHandler?.Session is Session reconnected)
        {
            if (!ReferenceEquals(reconnected, Session))
            {
                Session?.Dispose();
                Session = reconnected;
                Session.KeepAlive -= OnKeepAlive;
                Session.KeepAlive += OnKeepAlive;
            }

            _reconnectHandler?.Dispose();
            _reconnectHandler = null;

            _logger.LogInformation("Reconnected");
            SetState(UaConnectionState.Connected, error: null);
        }
    }

    private void BuildSessionInfo(EndpointDescription endpoint, IUserIdentity identity)
    {
        string subject = "";
        string thumbprint = "";
        if (endpoint.ServerCertificate is { Length: > 0 })
        {
            try
            {
                using var cert = X509CertificateLoader.LoadCertificate(endpoint.ServerCertificate);
                subject = cert.Subject;
                thumbprint = cert.Thumbprint;
            }
            catch
            {
                subject = "(could not parse server certificate)";
            }
        }

        SessionInfo = new UaSessionInfo(
            endpoint.EndpointUrl,
            endpoint.SecurityPolicyUri[(endpoint.SecurityPolicyUri.LastIndexOf('#') + 1)..],
            endpoint.SecurityMode.ToString(),
            identity.TokenType == UserTokenType.Anonymous ? "Anonymous" : identity.DisplayName,
            Session?.SessionId?.ToString() ?? "",
            subject,
            thumbprint,
            Session?.NamespaceUris.ToArray() ?? []);
    }

    private void SetState(UaConnectionState state, string? error)
    {
        State = state;
        LastError = error;
        StateChanged?.Invoke();
    }

    private static string Describe(Exception ex)
    {
        if (ex is ServiceResultException sre)
        {
            return $"{StatusCodes.GetBrowseName(sre.StatusCode)}: {sre.Message}";
        }

        return ex.Message;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // Circuit teardown must not throw.
        }
    }
}
