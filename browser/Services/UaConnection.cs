namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Details of a server certificate awaiting a trust decision.
/// </summary>
public sealed record UaPendingCertificate(
    string Subject,
    string Issuer,
    string Thumbprint,
    DateTime NotBefore,
    DateTime NotAfter,
    string Error,
    byte[] RawData);

/// <summary>
/// Manages the OPC UA session for one browser circuit:
/// discovery, connect/disconnect, certificate trust and automatic reconnect.
/// </summary>
public sealed class UaConnection : IAsyncDisposable
{
    private readonly UaApplicationProvider _applicationProvider;
    private readonly ILogger<UaConnection> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly HashSet<string> _sessionTrustedThumbprints = [];

    private ApplicationConfiguration? _configuration;
    private SessionReconnectHandler? _reconnectHandler;
    private UaPendingCertificate? _lastRejectedCertificate;

    public UaConnection(UaApplicationProvider applicationProvider, ILogger<UaConnection> logger)
    {
        _applicationProvider = applicationProvider;
        _logger = logger;
    }

    /// <summary>The active session, if connected.</summary>
    public Session? Session { get; private set; }

    /// <summary>Decodes server-defined structure types, when loaded.</summary>
    public ComplexTypeSystem? TypeSystem { get; private set; }

    public UaConnectionState State { get; private set; } = UaConnectionState.Disconnected;

    public string? LastError { get; private set; }

    public UaSessionInfo? SessionInfo { get; private set; }

    /// <summary>
    /// Set when the last connect attempt failed because the server certificate
    /// is untrusted; the UI offers a trust decision.
    /// </summary>
    public UaPendingCertificate? PendingCertificate { get; private set; }

    /// <summary>Raised on any connection state change. May fire on a background thread.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Raised after a reconnect replaced the session object. Subscribers
    /// holding session-bound objects (subscriptions) must re-bind.
    /// May fire on a background thread.
    /// </summary>
    public event Action<Session>? SessionReplaced;

    /// <summary>
    /// Whether untrusted server certificates are accepted automatically
    /// without a trust prompt. Off by default.
    /// </summary>
    public bool AutoAcceptServerCertificate { get; set; }

    /// <summary>
    /// Discover the endpoints offered by a server.
    /// </summary>
    public async Task<List<UaEndpointInfo>> DiscoverEndpointsAsync(string discoveryUrl, CancellationToken ct = default)
    {
        var config = await GetConfigurationAsync().ConfigureAwait(false);

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
    /// Returns false when the connect failed because the server certificate is
    /// untrusted; <see cref="PendingCertificate"/> then holds its details.
    /// </summary>
    public async Task<bool> ConnectAsync(
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

            PendingCertificate = null;
            _lastRejectedCertificate = null;
            SetState(UaConnectionState.Connecting, error: null);

            var config = await GetConfigurationAsync().ConfigureAwait(false);

            EndpointDescription description = endpoint
                ?? CoreClientUtils.SelectEndpoint(config, endpointUrl, useSecurity, discoverTimeout: 15_000);

            // Servers often advertise an internal hostname (e.g. a container
            // name) in their endpoints; connect to the address the user typed.
            description.EndpointUrl = RewriteEndpointHost(description.EndpointUrl, endpointUrl);

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
            catch (Exception ex) when (_lastRejectedCertificate is not null)
            {
                PendingCertificate = _lastRejectedCertificate;
                SetState(UaConnectionState.Disconnected, error: $"Server certificate is not trusted ({_lastRejectedCertificate.Error}).");
                _logger.LogInformation(ex, "Connect blocked by untrusted server certificate {Thumbprint}", _lastRejectedCertificate.Thumbprint);
                return false;
            }

            Session.KeepAlive += OnKeepAlive;
            Session.DeleteSubscriptionsOnClose = true;

            await LoadTypeSystemAsync().ConfigureAwait(false);

            BuildSessionInfo(description, identity);
            SetState(UaConnectionState.Connected, error: null);

            _logger.LogInformation("Connected to {EndpointUrl}", description.EndpointUrl);
            return true;
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
    /// Dismiss the pending certificate without trusting it.
    /// </summary>
    public void DismissPendingCertificate()
    {
        PendingCertificate = null;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Trust the pending server certificate for this circuit only.
    /// </summary>
    public void TrustPendingCertificateOnce()
    {
        if (PendingCertificate is { } pending)
        {
            _sessionTrustedThumbprints.Add(pending.Thumbprint);
            PendingCertificate = null;
        }
    }

    /// <summary>
    /// Trust the pending server certificate permanently by adding it to the
    /// trusted certificate store on disk.
    /// </summary>
    public async Task TrustPendingCertificatePermanentlyAsync()
    {
        if (PendingCertificate is not { } pending)
        {
            return;
        }

        var config = await GetConfigurationAsync().ConfigureAwait(false);

        using var cert = X509CertificateLoader.LoadCertificate(pending.RawData);
        using (var store = config.SecurityConfiguration.TrustedPeerCertificates.OpenStore())
        {
            await store.Add(cert).ConfigureAwait(false);
        }

        _sessionTrustedThumbprints.Add(pending.Thumbprint);
        PendingCertificate = null;
        _logger.LogInformation("Added server certificate {Thumbprint} to the trusted store", pending.Thumbprint);
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

    private async Task<ApplicationConfiguration> GetConfigurationAsync()
    {
        if (_configuration is null)
        {
            _configuration = await _applicationProvider.CreateConfigurationAsync().ConfigureAwait(false);
            _configuration.CertificateValidator.CertificateValidation += OnCertificateValidation;
        }

        return _configuration;
    }

    private async Task LoadTypeSystemAsync()
    {
        TypeSystem = null;
        try
        {
            var typeSystem = new ComplexTypeSystem(Session);
            await typeSystem.Load().ConfigureAwait(false);
            TypeSystem = typeSystem;
        }
        catch (Exception ex)
        {
            // Structure values will render as raw extension objects.
            _logger.LogWarning(ex, "Could not load the server's complex type system");
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

        TypeSystem = null;
        SessionInfo = null;
    }

    private void OnCertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
    {
        if (e.Error.StatusCode != StatusCodes.BadCertificateUntrusted)
        {
            return;
        }

        string thumbprint = e.Certificate?.Thumbprint ?? "";

        if (AutoAcceptServerCertificate || _sessionTrustedThumbprints.Contains(thumbprint))
        {
            e.Accept = true;
            return;
        }

        if (e.Certificate is not null)
        {
            _lastRejectedCertificate = new UaPendingCertificate(
                e.Certificate.Subject,
                e.Certificate.Issuer,
                e.Certificate.Thumbprint,
                e.Certificate.NotBefore,
                e.Certificate.NotAfter,
                e.Error.ToString(),
                e.Certificate.RawData);
        }
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

        var handler = _reconnectHandler;
        _reconnectHandler = null;

        bool replaced = false;
        if (handler?.Session is Session reconnected && !ReferenceEquals(reconnected, Session))
        {
            // The session was recreated (e.g. after a server restart);
            // subscriptions were cloned onto the new session.
            Session?.Dispose();
            Session = reconnected;
            Session.KeepAlive -= OnKeepAlive;
            Session.KeepAlive += OnKeepAlive;
            replaced = true;
        }

        // A null handler session means the keep-alive recovered on its own;
        // either way the connection is usable again.
        handler?.Dispose();

        _logger.LogInformation("Reconnected (session {Replaced})", replaced ? "recreated" : "kept");
        SetState(UaConnectionState.Connected, error: null);

        if (replaced && Session is not null)
        {
            SessionReplaced?.Invoke(Session);
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

    private static string RewriteEndpointHost(string advertisedUrl, string requestedUrl)
    {
        try
        {
            var advertised = new Uri(advertisedUrl);
            var requested = new Uri(requestedUrl);

            if (string.Equals(advertised.Host, requested.Host, StringComparison.OrdinalIgnoreCase))
            {
                return advertisedUrl;
            }

            var builder = new UriBuilder(advertised)
            {
                Host = requested.Host,
                Port = requested.IsDefaultPort ? advertised.Port : requested.Port,
            };

            return builder.Uri.ToString();
        }
        catch (Exception)
        {
            return advertisedUrl;
        }
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
