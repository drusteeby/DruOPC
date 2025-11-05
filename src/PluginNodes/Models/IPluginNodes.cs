namespace OpcPlc.PluginNodes.Models;

using Opc.Ua;
using System.Collections.Generic;

public interface IPluginNodes
{
    IReadOnlyCollection<NodeWithIntervals> Nodes { get; }

    void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager);

    void StartSimulation();

    void StopSimulation();
}
