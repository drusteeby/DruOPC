namespace OpcPlc;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using OpcPlc.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Background service that writes values to OPC UA tags.
/// </summary>
public class OpcTagWriterService : BackgroundService
{
    private readonly ILogger<OpcTagWriterService> _logger;
    private readonly TimeService _timeService;
    private readonly OpcPlcConfiguration _config;
    private readonly Func<PlcServer> _plcServerFactory;
    private readonly IHostApplicationLifetime _lifetime;

    // Configuration for the write sequence
    private readonly int _writeIntervalMs;
    private readonly string _targetNodeId;
    private readonly ushort _namespaceIndex;

    public OpcTagWriterService(
        IOptions<OpcPlcConfiguration> options,
        ILogger<OpcTagWriterService> logger,
        TimeService timeService,
        Func<PlcServer> plcServerFactory,
        IHostApplicationLifetime lifetime)
    {
        _config = options.Value;
        _logger = logger;
        _timeService = timeService;
        _plcServerFactory = plcServerFactory;
        _lifetime = lifetime;

        // Initialize configuration - these can be moved to appsettings.json
        _writeIntervalMs = 1000; // Default 1 second between writes
        _targetNodeId = "TagWriter"; // Default node ID to write to
        _namespaceIndex = 2; // Default namespace index
    }

    /// <summary>
    /// Execute the background service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OPC Tag Writer Service starting...");

        // Wait for the PLC server to be initialized
        PlcServer plcServer = null;
        while ((plcServer = _plcServerFactory()) == null && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for PLC server initialization...");
            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        // Wait for PlcNodeManager to be initialized
        while (plcServer.PlcNodeManager == null && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for PLC node manager initialization...");
            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        _logger.LogInformation("OPC Tag Writer Service started. Target Node: {NodeId}, Namespace: {Namespace}, Interval: {Interval}ms",
            _targetNodeId, _namespaceIndex, _writeIntervalMs);

        try
        {
            await RunWriteSequenceAsync(plcServer, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OPC Tag Writer Service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OPC Tag Writer Service encountered an error");
        }
    }

    /// <summary>
    /// Run the write sequence. Override or modify this method to implement your custom write sequence.
    /// </summary>
    private async Task RunWriteSequenceAsync(PlcServer plcServer, CancellationToken stoppingToken)
    {
        int sequenceCounter = 1;

        var nodeId = new NodeId("St10_Data.status.ReadComplete", 3);
        await WriteValueAsync(plcServer, nodeId, false, stoppingToken).ConfigureAwait(false);
        nodeId = new NodeId("St10_Data.status.WriteComplete", 3);
        await WriteValueAsync(plcServer, nodeId, false, stoppingToken).ConfigureAwait(false);
        await Task.Delay(10000, stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Beginning write sequence loop");

        while (!stoppingToken.IsCancellationRequested)
        {

            try
            {
                nodeId = new NodeId("St10_Data.status.ReadComplete", 3);
                await WriteValueAsync(plcServer, nodeId, true, stoppingToken).ConfigureAwait(false);
                await Task.Delay(10000, stoppingToken).ConfigureAwait(false);

                nodeId = new NodeId("St10_Data.status.WriteComplete", 3);
                await WriteValueAsync(plcServer, nodeId, true, stoppingToken).ConfigureAwait(false);
                await Task.Delay(10000, stoppingToken).ConfigureAwait(false);

                if (sequenceCounter % 5 != 0)
                {
                    nodeId = new NodeId("St10_Data.Header.UnitId.Data", 3);
                    await WriteValueAsync(plcServer, nodeId, sequenceCounter.ToString(), stoppingToken).ConfigureAwait(false);
                    await Task.Delay(10000, stoppingToken).ConfigureAwait(false);
                }
                _logger.LogInformation(sequenceCounter % 5 != 0
                                        ? "Writing UnitId this cycle"
                                        : "Skipping UnitId write this cycle");

                if (sequenceCounter % 6 != 0)
                {
                    nodeId = new NodeId("St10_Data.Header.PalletNumber", 3);
                    await WriteValueAsync(plcServer, nodeId, sequenceCounter, stoppingToken).ConfigureAwait(false);
                    await Task.Delay(10000, stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation(sequenceCounter % 6 != 0
                                        ? "Writing PalletNumber this cycle"
                                        : "Skipping PalletNumber write this cycle");


                if (sequenceCounter % 8 != 0)
                {
                    nodeId = new NodeId("St10_Data.status.WriteComplete", 3);
                    await WriteValueAsync(plcServer, nodeId, true, stoppingToken).ConfigureAwait(false);
                    await Task.Delay(10000, stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation(sequenceCounter % 8 != 0
                        ? "Writing PalletNumber this cycle"
                        : "Skipping PalletNumber write this cycle");

                if (sequenceCounter % 100 != 0)
                {
                    nodeId = new NodeId("St10_Data.status.ReadComplete", 3);
                    await WriteValueAsync(plcServer, nodeId, false, stoppingToken).ConfigureAwait(false);
                    nodeId = new NodeId("St10_Data.status.WriteComplete", 3);
                    await WriteValueAsync(plcServer, nodeId, false, stoppingToken).ConfigureAwait(false);
                    await Task.Delay(10000, stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation(sequenceCounter % 100 != 0
                        ? "Clearing Read/Write Status"
                        : "Simulating clear Read/Write failure");

                sequenceCounter = sequenceCounter == int.MaxValue ? 0 : sequenceCounter + 1;
                _logger.LogDebug("Write sequence step {Counter} completed", sequenceCounter);

                await Task.Delay(_writeIntervalMs, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to be handled by outer catch
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write value to node {NodeId}", nodeId);
                // Continue the sequence even if a write fails
                await Task.Delay(_writeIntervalMs, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Write a value to the specified OPC UA node.
    /// </summary>
    /// <param name="plcServer">The PLC server instance</param>
    /// <param name="nodeId">The node ID to write to</param>
    /// <param name="value">The value to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task WriteValueAsync(PlcServer plcServer, NodeId nodeId, object value, CancellationToken cancellationToken)
    {
        if (plcServer?.PlcNodeManager == null)
        {
            _logger.LogWarning("PLC server or node manager not available");
            return;
        }

        await Task.Run(() => {
            try
            {
                // Find the node in the address space
                var node = plcServer.PlcNodeManager.FindPredefinedNode(nodeId, typeof(BaseDataVariableState));

                if (node is BaseDataVariableState variable)
                {
                    // Update the value
                    variable.Value = value;
                    variable.Timestamp = _timeService.UtcNow();
                    variable.StatusCode = StatusCodes.Good;

                    // Notify clients of the change
                    variable.ClearChangeMasks(plcServer.PlcNodeManager.SystemContext, false);

                    _logger.LogDebug("Successfully wrote value {Value} to node {NodeId}", value, nodeId);
                }
                else
                {
                    _logger.LogWarning("Node {NodeId} not found or is not a variable", nodeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to node {NodeId}", nodeId);
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cleanup when the service is stopping.
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OPC Tag Writer Service stopping...");
        return base.StopAsync(cancellationToken);
    }
}
