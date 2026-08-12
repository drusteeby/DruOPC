namespace OpcPlc.CompanionSpecs.DI;

using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Reflection;

/// <summary>
/// Node manager for a server that exposes the Device Information (DI) companion spec.
/// https://opcfoundation.org/developer-tools/documents/view/197
/// </summary>
public sealed class DiNodeManager : CustomNodeManager2
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiNodeManager"/> class.
    /// </summary>
    public DiNodeManager(IServerInternal server, ApplicationConfiguration _)
    :
        base(server, _)
    {
        SystemContext.NodeIdFactory = this;

        // Set one namespace for the type model and one namespace for dynamically created nodes.
        string[] namespaceUrls = [OpcPlc.Namespaces.DI];
        SetNamespaces(namespaceUrls);
    }

    /// <summary>
    /// Loads a node set from a file or resource and adds them to the set of predefined nodes.
    /// </summary>
    protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
    {
        var uanodesPath = "OpcUaDi"; // embedded resource, LogicalName in opc-plc.csproj

        var predefinedNodes = new NodeStateCollection();
        predefinedNodes.LoadFromBinaryResource(context,
            uanodesPath,
            typeof(DiNodeManager).GetTypeInfo().Assembly,
            updateTables: true);

        return predefinedNodes;
    }
}
