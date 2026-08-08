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
/// Background service that writes values to OPC UA tags,
/// simulating the station-data handshake of the demo nodes file.
/// </summary>
public class OpcTagWriterService : BackgroundService
{
    private readonly ILogger<OpcTagWriterService> _logger;
    private readonly TimeService _timeService;
    private readonly TagWriterConfiguration _config;
    private readonly OpcPlcServer _opcPlcServer;

    public OpcTagWriterService(
        IOptions<OpcPlcConfiguration> options,
        ILogger<OpcTagWriterService> logger,
        TimeService timeService,
        OpcPlcServer opcPlcServer)
    {
        _config = options.Value.TagWriter;
        _logger = logger;
        _timeService = timeService;
        _opcPlcServer = opcPlcServer;
    }

    /// <summary>
    /// Execute the background service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("OPC Tag Writer Service is disabled via configuration");
            return;
        }

        _logger.LogInformation("OPC Tag Writer Service starting ...");

        // Wait for the OPC UA server to be up and its node manager initialized.
        await _opcPlcServer.WaitUntilReadyAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation(
            "OPC Tag Writer Service started. Namespace: {Namespace}, step delay: {StepDelay} ms, write interval: {Interval} ms",
            _config.NamespaceIndex,
            _config.StepDelayMs,
            _config.WriteIntervalMs);

        try
        {
            await RunWriteSequenceAsync(_opcPlcServer.PlcServer, stoppingToken).ConfigureAwait(false);
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

        WriteValue(plcServer, ReadCompleteNodeId(), false);
        WriteValue(plcServer, WriteCompleteNodeId(), false);
        await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Beginning write sequence loop");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                WriteValue(plcServer, ReadCompleteNodeId(), true);
                await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);

                WriteValue(plcServer, WriteCompleteNodeId(), true);
                await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);

                if (sequenceCounter % 5 != 0)
                {
                    WriteValue(plcServer, new NodeId(_config.UnitIdNodeId, _config.NamespaceIndex), sequenceCounter.ToString());
                    await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation(sequenceCounter % 5 != 0
                                        ? "Writing UnitId this cycle"
                                        : "Skipping UnitId write this cycle");

                if (sequenceCounter % 6 != 0)
                {
                    WriteValue(plcServer, new NodeId(_config.PalletNumberNodeId, _config.NamespaceIndex), sequenceCounter);
                    await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation(sequenceCounter % 6 != 0
                                        ? "Writing PalletNumber this cycle"
                                        : "Skipping PalletNumber write this cycle");

                if (sequenceCounter % 8 != 0)
                {
                    WriteValue(plcServer, WriteCompleteNodeId(), true);
                    await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);
                }

                if (sequenceCounter % 100 != 0)
                {
                    WriteValue(plcServer, ReadCompleteNodeId(), false);
                    WriteValue(plcServer, WriteCompleteNodeId(), false);
                    await Task.Delay(_config.StepDelayMs, stoppingToken).ConfigureAwait(false);
                }

                _logger.LogInformation(sequenceCounter % 100 != 0
                        ? "Clearing Read/Write Status"
                        : "Simulating clear Read/Write failure");

                sequenceCounter = sequenceCounter == int.MaxValue ? 0 : sequenceCounter + 1;
                _logger.LogDebug("Write sequence step {Counter} completed", sequenceCounter);

                await Task.Delay(_config.WriteIntervalMs, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to be handled by outer catch
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write value");
                // Continue the sequence even if a write fails
                await Task.Delay(_config.WriteIntervalMs, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private NodeId ReadCompleteNodeId() => new(_config.ReadCompleteNodeId, _config.NamespaceIndex);

    private NodeId WriteCompleteNodeId() => new(_config.WriteCompleteNodeId, _config.NamespaceIndex);

    /// <summary>
    /// Write a value to the specified OPC UA node.
    /// </summary>
    private void WriteValue(PlcServer plcServer, NodeId nodeId, object value)
    {
        if (plcServer?.PlcNodeManager == null)
        {
            _logger.LogWarning("PLC server or node manager not available");
            return;
        }

        var node = plcServer.PlcNodeManager.FindPredefinedNode(nodeId, typeof(BaseDataVariableState));

        if (node is BaseDataVariableState variable)
        {
            variable.Value = value;
            variable.Timestamp = _timeService.UtcNow();
            variable.StatusCode = StatusCodes.Good;

            // Notify clients of the change.
            variable.ClearChangeMasks(plcServer.PlcNodeManager.SystemContext, false);

            _logger.LogDebug("Successfully wrote value {Value} to node {NodeId}", value, nodeId);
        }
        else
        {
            _logger.LogWarning("Node {NodeId} not found or is not a variable", nodeId);
        }
    }

    /// <summary>
    /// Cleanup when the service is stopping.
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OPC Tag Writer Service stopping ...");
        return base.StopAsync(cancellationToken);
    }
}
