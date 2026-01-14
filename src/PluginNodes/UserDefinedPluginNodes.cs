namespace OpcPlc.PluginNodes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Opc.Ua;
using OpcPlc.Configuration;
using OpcPlc.Helpers;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Nodes that are configured via JSON file.
/// </summary>
public class UserDefinedPluginNodes : PluginNodeBase, IPluginNodes
{
    private readonly string _nodesFileName;
    private PlcNodeManager _plcNodeManager;

    public UserDefinedPluginNodes(TimeService timeService, ILogger<UserDefinedPluginNodes> logger, IOptions<OpcPlcConfiguration> options)
        : base(timeService, logger)
    {
        _nodesFileName = options.Value.NodesFile;
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;

        if (!string.IsNullOrEmpty(_nodesFileName))
        {
            AddNodes((FolderState)telemetryFolder.Parent); // Root.
        }
    }

    public void StartSimulation()
    {
        // No simulation.
    }

    public void StopSimulation()
    {
        // No simulation.
    }

    private void AddNodes(FolderState folder)
    {
        try
        {
            string json = File.ReadAllText(_nodesFileName);

            var cfgFolder = JsonConvert.DeserializeObject<ConfigFolder>(json, new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None,
            });

            _logger.LogInformation($"Processing node information configured in {_nodesFileName}");

            Nodes = AddNodes(folder, cfgFolder).ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error loading user defined node file {File}: {Error}", _nodesFileName, e.Message);
        }


        _logger.LogInformation("Completed processing user defined node file");
    }

    private IEnumerable<NodeWithIntervals> AddNodes(FolderState folder, ConfigFolder cfgFolder)
    {
        _logger.LogDebug($"Create folder {cfgFolder.Folder}");
        FolderState userNodesFolder = _plcNodeManager.CreateFolder(
            folder,
            path: cfgFolder.Folder,
            name: cfgFolder.Folder,
            NamespaceType.OpcPlcApplications);

        // Check if NodeList is not null before iterating
        if (cfgFolder.NodeList != null)
        {
            foreach (var node in cfgFolder.NodeList)
            {
                bool isDecimal = node.NodeId is long;
                bool isString = node.NodeId is string;

                if (!isDecimal && !isString)
                {
                    _logger.LogError($"The type of the node configuration for node with name {node.Name} ({node.NodeId.GetType()}) is not supported. Only decimal, string, and GUID are supported. Defaulting to string.");
                    node.NodeId = node.NodeId.ToString();
                }

                bool isGuid = false;
                if (Guid.TryParse(node.NodeId.ToString(), out Guid guidNodeId))
                {
                    isGuid = true;
                    node.NodeId = guidNodeId;
                }

                string typedNodeId = isDecimal
                    ? $"i={node.NodeId.ToString()}"
                    : isGuid
                        ? $"g={node.NodeId.ToString()}"
                        : $"s={node.NodeId.ToString()}";

                if (node.ValueRank == 1 && node.Value is JArray jArrayValue)
                {
                    node.Value = UpdateArrayValue(node, jArrayValue);
                }

                if (string.IsNullOrEmpty(node.Name))
                {
                    node.Name = typedNodeId;
                }

                if (string.IsNullOrEmpty(node.Description))
                {
                    node.Description = node.Name;
                }

                // Determine the namespace index to use
                ushort namespaceIndex;
                if (node.NamespaceIndex.HasValue)
                {
                    // Use explicit namespace index
                    namespaceIndex = node.NamespaceIndex.Value;
                    _logger.LogDebug("Using explicit namespace index {NamespaceIndex} for node {NodeId}",
                        namespaceIndex, typedNodeId);
                }
                else if (!string.IsNullOrEmpty(node.Namespace))
                {
                    // Register or get namespace URI
                    namespaceIndex = _plcNodeManager.GetNamespaceIndex(node.Namespace);
                    _logger.LogDebug("Using namespace URI '{Namespace}' (index: {NamespaceIndex}) for node {NodeId}",
                        node.Namespace, namespaceIndex, typedNodeId);
                }
                else
                {
                    // Use default namespace
                    namespaceIndex = _plcNodeManager.NamespaceIndexes[(int)NamespaceType.OpcPlcApplications];
                    _logger.LogDebug("Using default namespace index {NamespaceIndex} for node {NodeId}",
                        namespaceIndex, typedNodeId);
                }

                _logger.LogDebug("Create node with Id {TypedNodeId}, BrowseName {Name} and type {Type} in namespace with index {NamespaceIndex}",
                    typedNodeId,
                    node.Name,
                    (string)node.NodeId.GetType().Name,
                    namespaceIndex);

                CreateBaseVariable(userNodesFolder, node, namespaceIndex);

                NodeId nodeId;
                if (isDecimal)
                {
                    nodeId = new NodeId((uint)node.NodeId, namespaceIndex);
                }
                else if (isGuid)
                {
                    nodeId = new NodeId((Guid)node.NodeId, namespaceIndex);
                }
                else
                {
                    nodeId = new NodeId((string)node.NodeId, namespaceIndex);
                }

                yield return PluginNodesHelper.GetNodeWithIntervals(nodeId, _plcNodeManager);
            }
        }

        foreach (var childNode in AddFolders(userNodesFolder, cfgFolder))
        {
            yield return childNode;
        }
    }

    private IEnumerable<NodeWithIntervals> AddFolders(FolderState folder, ConfigFolder cfgFolder)
    {
        if (cfgFolder.FolderList is null)
        {
            yield break;
        }

        foreach (var childFolder in cfgFolder.FolderList)
        {
            foreach (var node in AddNodes(folder, childFolder))
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Creates a new variable with custom namespace support.
    /// </summary>
    public void CreateBaseVariable(NodeState parent, ConfigNode node, ushort namespaceIndex)
    {
        if (!Enum.TryParse(node.DataType, out BuiltInType nodeDataType))
        {
            _logger.LogError($"Value {node.DataType} of node {node.NodeId} cannot be parsed. Defaulting to Int32");
            node.DataType = "Int32";
        }

        // We have to hard code the conversion here, because AccessLevel is defined as byte in OPC UA lib.
        byte accessLevel;
        try
        {
            accessLevel = (byte)(typeof(AccessLevels).GetField(node.AccessLevel).GetValue(null));
        }
        catch
        {
            _logger.LogError($"AccessLevel {node.AccessLevel} of node {node.Name} is not supported. Defaulting to CurrentReadOrWrite");
            node.AccessLevel = "CurrentRead";
            accessLevel = AccessLevels.CurrentReadOrWrite;
        }

        _plcNodeManager.CreateBaseVariableWithNamespace(
            parent, 
            node.NodeId, 
            node.Name, 
            new NodeId((uint)nodeDataType), 
            node.ValueRank, 
            accessLevel, 
            node.Description, 
            namespaceIndex, 
            node?.Value);
    }

    private static object UpdateArrayValue(ConfigNode node, JArray jArrayValue)
    {
        return node.DataType switch {
            "String" => jArrayValue.ToObject<string[]>(),
            "Boolean" => jArrayValue.ToObject<bool[]>(),
            "Float" => jArrayValue.ToObject<float[]>(),
            "UInt32" => jArrayValue.ToObject<uint[]>(),
            "Int32" => jArrayValue.ToObject<int[]>(),
            _ => throw new NotImplementedException($"Node type not implemented: {node.DataType}."),
        };
    }
}
