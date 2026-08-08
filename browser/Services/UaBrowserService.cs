namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// Address-space operations on the current session:
/// browsing, attribute reads, writes, method calls and search.
/// </summary>
public sealed class UaBrowserService
{
    private readonly UaConnection _connection;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<NodeId, string> _displayNameCache = [];

    public UaBrowserService(UaConnection connection)
    {
        _connection = connection;
    }

    private Session Session => _connection.Session
        ?? throw new InvalidOperationException("Not connected to a server.");

    /// <summary>
    /// Reset per-connection caches. Call after connecting to a new server.
    /// </summary>
    public void Reset() => _displayNameCache.Clear();

    /// <summary>
    /// Browse the hierarchical children of a node (with continuation point handling).
    /// </summary>
    public async Task<List<UaTreeNode>> BrowseChildrenAsync(NodeId nodeId, CancellationToken ct = default)
    {
        var references = await BrowseAsync(
            nodeId,
            BrowseDirection.Forward,
            ReferenceTypeIds.HierarchicalReferences,
            ct).ConfigureAwait(false);

        var children = new List<UaTreeNode>();
        foreach (var reference in references)
        {
            var childId = ExpandedNodeId.ToNodeId(reference.NodeId, Session.NamespaceUris);
            if (childId is null)
            {
                continue;
            }

            children.Add(new UaTreeNode
            {
                NodeId = childId,
                DisplayName = reference.DisplayName?.Text ?? reference.BrowseName?.Name ?? childId.ToString(),
                BrowseName = FormatQualifiedName(reference.BrowseName),
                NodeClass = reference.NodeClass,
                TypeDefinition = ExpandedNodeId.ToNodeId(reference.TypeDefinition, Session.NamespaceUris),
            });
        }

        return children
            .OrderBy(c => c.NodeClass switch
            {
                NodeClass.Object => 0,
                NodeClass.Variable => 1,
                NodeClass.Method => 2,
                _ => 3,
            })
            .ThenBy(c => c.DisplayName, NaturalStringComparer.Instance)
            .ToList();
    }

    /// <summary>
    /// Get all references (forward and inverse) of a node for the references panel.
    /// </summary>
    public async Task<List<UaReferenceRow>> GetReferencesAsync(NodeId nodeId, CancellationToken ct = default)
    {
        var raw = new List<(ReferenceDescription Reference, bool IsForward)>();

        foreach (var direction in new[] { BrowseDirection.Forward, BrowseDirection.Inverse })
        {
            var references = await BrowseAsync(nodeId, direction, ReferenceTypeIds.References, ct).ConfigureAwait(false);
            raw.AddRange(references.Select(r => (r, direction == BrowseDirection.Forward)));
        }

        // Resolve all reference-type and type-definition names in one batched read.
        var namesToResolve = raw
            .SelectMany(r => new[]
            {
                ExpandedNodeId.ToNodeId(r.Reference.ReferenceTypeId, Session.NamespaceUris),
                ExpandedNodeId.ToNodeId(r.Reference.TypeDefinition, Session.NamespaceUris),
            })
            .Where(id => id is not null && !NodeId.IsNull(id))
            .Distinct()
            .ToList();

        await PrefetchDisplayNamesAsync(namesToResolve!, ct).ConfigureAwait(false);

        var rows = new List<UaReferenceRow>();
        foreach ((var reference, bool isForward) in raw)
        {
            var targetId = ExpandedNodeId.ToNodeId(reference.NodeId, Session.NamespaceUris);
            string referenceTypeName = CachedDisplayName(ExpandedNodeId.ToNodeId(reference.ReferenceTypeId, Session.NamespaceUris));
            string typeDefinition = reference.TypeDefinition is null || reference.TypeDefinition.IsNull
                ? ""
                : CachedDisplayName(ExpandedNodeId.ToNodeId(reference.TypeDefinition, Session.NamespaceUris));

            rows.Add(new UaReferenceRow(
                referenceTypeName,
                isForward,
                reference.DisplayName?.Text ?? reference.BrowseName?.Name ?? "",
                reference.NodeClass.ToString(),
                targetId,
                typeDefinition));
        }

        return rows;
    }

    /// <summary>
    /// Read all attributes of a node, formatted for display.
    /// </summary>
    public async Task<List<UaAttributeRow>> ReadAttributesAsync(NodeId nodeId, CancellationToken ct = default)
    {
        uint[] attributeIds = Attributes.GetIdentifiers();

        var readValueIds = new ReadValueIdCollection(
            attributeIds.Select(id => new ReadValueId { NodeId = nodeId, AttributeId = id }));

        var response = await Session.ReadAsync(
            requestHeader: null,
            maxAge: 0,
            TimestampsToReturn.Both,
            readValueIds,
            ct).ConfigureAwait(false);

        var rows = new List<UaAttributeRow>();

        for (int i = 0; i < attributeIds.Length; i++)
        {
            var dataValue = response.Results[i];
            uint attributeId = attributeIds[i];

            if (dataValue.StatusCode == StatusCodes.BadAttributeIdInvalid)
            {
                continue; // Attribute not supported by this node class.
            }

            string name = Attributes.GetBrowseName(attributeId);
            (string value, string detail) = await FormatAttributeAsync(attributeId, dataValue, ct).ConfigureAwait(false);

            rows.Add(new UaAttributeRow(name, value, detail, StatusCode.IsBad(dataValue.StatusCode)));
        }

        return rows;
    }

    /// <summary>
    /// Read the current value of a variable node.
    /// </summary>
    public async Task<DataValue> ReadValueAsync(NodeId nodeId, CancellationToken ct = default)
        => await Session.ReadValueAsync(nodeId, ct).ConfigureAwait(false);

    /// <summary>
    /// Read data type and access info needed to write to a variable.
    /// </summary>
    public async Task<(NodeId DataTypeId, int ValueRank, byte AccessLevel)> ReadVariableMetadataAsync(
        NodeId nodeId, CancellationToken ct = default)
    {
        var readValueIds = new ReadValueIdCollection
        {
            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.DataType },
            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.ValueRank },
            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.UserAccessLevel },
        };

        var response = await Session.ReadAsync(null, 0, TimestampsToReturn.Neither, readValueIds, ct).ConfigureAwait(false);

        var dataTypeId = response.Results[0].Value as NodeId ?? NodeId.Null;
        int valueRank = response.Results[1].Value is int rank ? rank : ValueRanks.Scalar;
        byte accessLevel = response.Results[2].Value is byte b ? b : (byte)0;

        return (dataTypeId, valueRank, accessLevel);
    }

    /// <summary>
    /// Write a value (parsed from text) to a variable node.
    /// </summary>
    public async Task<StatusCode> WriteValueAsync(
        NodeId nodeId,
        NodeId dataTypeId,
        int valueRank,
        string text,
        CancellationToken ct = default)
    {
        object value = UaFormat.ParseValue(text, dataTypeId, valueRank, Session.TypeTree);

        var writeValues = new WriteValueCollection
        {
            new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value)),
            },
        };

        var response = await Session.WriteAsync(null, writeValues, ct).ConfigureAwait(false);
        return response.Results[0];
    }

    /// <summary>
    /// Resolve the input/output argument definitions of a method node.
    /// </summary>
    public async Task<(List<UaMethodArgument> Inputs, List<UaMethodArgument> Outputs)> GetMethodArgumentsAsync(
        NodeId methodId, CancellationToken ct = default)
    {
        var inputs = new List<UaMethodArgument>();
        var outputs = new List<UaMethodArgument>();

        var references = await BrowseAsync(methodId, BrowseDirection.Forward, ReferenceTypeIds.HasProperty, ct).ConfigureAwait(false);

        foreach (var reference in references)
        {
            bool isInput = reference.BrowseName?.Name == BrowseNames.InputArguments;
            bool isOutput = reference.BrowseName?.Name == BrowseNames.OutputArguments;
            if (!isInput && !isOutput)
            {
                continue;
            }

            var propertyId = ExpandedNodeId.ToNodeId(reference.NodeId, Session.NamespaceUris);
            if (propertyId is null)
            {
                continue;
            }

            var dataValue = await Session.ReadValueAsync(propertyId, ct).ConfigureAwait(false);

            if (ExtensionObject.ToArray(dataValue.Value, typeof(Argument)) is not Argument[] arguments)
            {
                continue;
            }

            foreach (var argument in arguments)
            {
                var model = new UaMethodArgument
                {
                    Name = argument.Name,
                    DataTypeId = argument.DataType,
                    DataTypeName = await GetDisplayNameAsync(argument.DataType, ct).ConfigureAwait(false),
                    ValueRank = argument.ValueRank,
                    Description = argument.Description?.Text ?? "",
                };

                (isInput ? inputs : outputs).Add(model);
            }
        }

        return (inputs, outputs);
    }

    /// <summary>
    /// Find the object node a method belongs to. The HasComponent owner is
    /// the correct Call target; other hierarchical parents (e.g. Organizes
    /// folders) are only a fallback.
    /// </summary>
    public async Task<NodeId?> FindMethodParentAsync(NodeId methodId, CancellationToken ct = default)
    {
        var components = await BrowseAsync(methodId, BrowseDirection.Inverse, ReferenceTypeIds.HasComponent, ct).ConfigureAwait(false);
        var owner = components.FirstOrDefault();
        if (owner is not null)
        {
            return ExpandedNodeId.ToNodeId(owner.NodeId, Session.NamespaceUris);
        }

        var references = await BrowseAsync(methodId, BrowseDirection.Inverse, ReferenceTypeIds.HierarchicalReferences, ct).ConfigureAwait(false);
        var parent = references.FirstOrDefault(r => r.NodeClass is NodeClass.Object or NodeClass.ObjectType)
            ?? references.FirstOrDefault();
        return parent is null ? null : ExpandedNodeId.ToNodeId(parent.NodeId, Session.NamespaceUris);
    }

    /// <summary>
    /// Call a method with the given (already parsed) input arguments.
    /// </summary>
    public async Task<IList<object>> CallMethodAsync(
        NodeId objectId,
        NodeId methodId,
        List<UaMethodArgument> inputs,
        CancellationToken ct = default)
    {
        object[] args = inputs
            .Select(i => UaFormat.ParseValue(i.Value, i.DataTypeId, i.ValueRank, Session.TypeTree))
            .ToArray();

        return await Session.CallAsync(objectId, methodId, ct, args).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve a node from its string representation (e.g. "ns=3;s=MyNode").
    /// </summary>
    public async Task<UaTreeNode?> ResolveNodeAsync(string nodeIdText, CancellationToken ct = default)
    {
        NodeId nodeId;
        try
        {
            nodeId = NodeId.Parse(nodeIdText);
        }
        catch (Exception)
        {
            return null;
        }

        var readValueIds = new ReadValueIdCollection
        {
            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.DisplayName },
            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.BrowseName },
            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.NodeClass },
        };

        var response = await Session.ReadAsync(null, 0, TimestampsToReturn.Neither, readValueIds, ct).ConfigureAwait(false);

        if (StatusCode.IsBad(response.Results[0].StatusCode))
        {
            return null;
        }

        return new UaTreeNode
        {
            NodeId = nodeId,
            DisplayName = (response.Results[0].Value as LocalizedText)?.Text ?? nodeIdText,
            BrowseName = FormatQualifiedName(response.Results[1].Value as QualifiedName),
            NodeClass = response.Results[2].Value is int nc ? (NodeClass)nc : NodeClass.Unspecified,
        };
    }

    /// <summary>
    /// Get the browse path of a node from the root, by walking inverse
    /// hierarchical references. Returns the path root-first, excluding the
    /// Root folder itself.
    /// </summary>
    public async Task<List<(NodeId NodeId, string DisplayName)>> GetBrowsePathAsync(NodeId nodeId, CancellationToken ct = default)
    {
        var path = new List<(NodeId, string)>();
        var current = nodeId;
        var visited = new HashSet<NodeId>();

        for (int depth = 0; depth < 50 && current is not null && !visited.Contains(current); depth++)
        {
            visited.Add(current);

            if (current == ObjectIds.RootFolder)
            {
                break;
            }

            path.Add((current, await GetDisplayNameAsync(current, ct).ConfigureAwait(false)));

            var parents = await BrowseAsync(current, BrowseDirection.Inverse, ReferenceTypeIds.HierarchicalReferences, ct).ConfigureAwait(false);
            current = parents.Count > 0
                ? ExpandedNodeId.ToNodeId(parents[0].NodeId, Session.NamespaceUris)
                : null;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Search the address space (breadth-first from the Objects folder) for
    /// nodes whose display or browse name contains the given text.
    /// </summary>
    public async Task<List<UaTreeNode>> SearchAsync(
        string text,
        int maxResults = 50,
        int maxNodesVisited = 5000,
        CancellationToken ct = default)
    {
        var results = new List<UaTreeNode>();
        var visited = new HashSet<NodeId> { ObjectIds.ObjectsFolder };
        var frontier = new List<NodeId> { ObjectIds.ObjectsFolder };

        while (frontier.Count > 0 && visited.Count < maxNodesVisited && results.Count < maxResults)
        {
            ct.ThrowIfCancellationRequested();

            // Browse up to 50 nodes per request round.
            var batch = frontier.Take(50).ToList();
            frontier.RemoveRange(0, batch.Count);

            var nodesToBrowse = new BrowseDescriptionCollection(batch.Select(id => new BrowseDescription
            {
                NodeId = id,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = 0,
                ResultMask = (uint)BrowseResultMask.All,
            }));

            var response = await Session.BrowseAsync(null, null, 0, nodesToBrowse, ct).ConfigureAwait(false);

            foreach (var result in response.Results)
            {
                if (StatusCode.IsBad(result.StatusCode))
                {
                    continue;
                }

                var references = new List<ReferenceDescription>(result.References);

                // Follow continuation points so large folders are fully searched.
                var continuationPoint = result.ContinuationPoint;
                while (continuationPoint is { Length: > 0 })
                {
                    var next = await Session.BrowseNextAsync(null, false, new ByteStringCollection { continuationPoint }, ct).ConfigureAwait(false);
                    if (StatusCode.IsBad(next.Results[0].StatusCode))
                    {
                        break;
                    }

                    references.AddRange(next.Results[0].References);
                    continuationPoint = next.Results[0].ContinuationPoint;
                }

                foreach (var reference in references)
                {
                    var childId = ExpandedNodeId.ToNodeId(reference.NodeId, Session.NamespaceUris);
                    if (childId is null || !visited.Add(childId))
                    {
                        continue;
                    }

                    string displayName = reference.DisplayName?.Text ?? "";
                    string browseName = reference.BrowseName?.Name ?? "";

                    if (displayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || browseName.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || (childId.IdType == IdType.String && childId.Identifier.ToString()!.Contains(text, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new UaTreeNode
                        {
                            NodeId = childId,
                            DisplayName = displayName,
                            BrowseName = FormatQualifiedName(reference.BrowseName),
                            NodeClass = reference.NodeClass,
                        });

                        if (results.Count >= maxResults)
                        {
                            break;
                        }
                    }

                    frontier.Add(childId);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Raw browse with continuation point handling.
    /// </summary>
    public async Task<List<ReferenceDescription>> BrowseAsync(
        NodeId nodeId,
        BrowseDirection direction,
        NodeId referenceTypeId,
        CancellationToken ct = default)
    {
        var results = new List<ReferenceDescription>();

        var nodesToBrowse = new BrowseDescriptionCollection
        {
            new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = direction,
                ReferenceTypeId = referenceTypeId,
                IncludeSubtypes = true,
                NodeClassMask = 0,
                ResultMask = (uint)BrowseResultMask.All,
            },
        };

        var response = await Session.BrowseAsync(
            requestHeader: null,
            view: null,
            requestedMaxReferencesPerNode: 0,
            nodesToBrowse,
            ct).ConfigureAwait(false);

        var result = response.Results[0];
        if (StatusCode.IsBad(result.StatusCode))
        {
            throw new ServiceResultException(result.StatusCode);
        }

        results.AddRange(result.References);

        var continuationPoint = result.ContinuationPoint;
        try
        {
            while (continuationPoint is { Length: > 0 })
            {
                var continuationPoints = new ByteStringCollection { continuationPoint };
                var nextResponse = await Session.BrowseNextAsync(
                    requestHeader: null,
                    releaseContinuationPoints: false,
                    continuationPoints,
                    ct).ConfigureAwait(false);

                var nextResult = nextResponse.Results[0];
                if (StatusCode.IsBad(nextResult.StatusCode))
                {
                    break;
                }

                results.AddRange(nextResult.References);
                continuationPoint = nextResult.ContinuationPoint;
            }
        }
        finally
        {
            // Do not leak the continuation point on abnormal exit; servers
            // have a limited number of them per session.
            if (continuationPoint is { Length: > 0 })
            {
                try
                {
                    await Session.BrowseNextAsync(
                        requestHeader: null,
                        releaseContinuationPoints: true,
                        new ByteStringCollection { continuationPoint },
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Best effort only.
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Get the display name of a node (cached).
    /// </summary>
    public async Task<string> GetDisplayNameAsync(NodeId? nodeId, CancellationToken ct = default)
    {
        if (nodeId is null || NodeId.IsNull(nodeId))
        {
            return "";
        }

        if (_displayNameCache.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }

        await PrefetchDisplayNamesAsync([nodeId], ct).ConfigureAwait(false);
        return CachedDisplayName(nodeId);
    }

    /// <summary>
    /// Batch-resolve display names for the given nodes into the cache.
    /// </summary>
    private async Task PrefetchDisplayNamesAsync(IReadOnlyList<NodeId> nodeIds, CancellationToken ct)
    {
        var missing = nodeIds
            .Where(id => id is not null && !NodeId.IsNull(id) && !_displayNameCache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        try
        {
            var readValueIds = new ReadValueIdCollection(
                missing.Select(id => new ReadValueId { NodeId = id, AttributeId = Attributes.DisplayName }));

            var response = await Session.ReadAsync(null, 0, TimestampsToReturn.Neither, readValueIds, ct).ConfigureAwait(false);

            for (int i = 0; i < missing.Count; i++)
            {
                _displayNameCache[missing[i]] = StatusCode.IsGood(response.Results[i].StatusCode)
                    ? (response.Results[i].Value as LocalizedText)?.Text ?? missing[i].ToString()
                    : missing[i].ToString();
            }
        }
        catch (Exception)
        {
            foreach (var id in missing)
            {
                _displayNameCache.TryAdd(id, id.ToString());
            }
        }
    }

    private string CachedDisplayName(NodeId? nodeId)
        => nodeId is null || NodeId.IsNull(nodeId)
            ? ""
            : _displayNameCache.TryGetValue(nodeId, out var name) ? name : nodeId.ToString();

    private async Task<(string Value, string Detail)> FormatAttributeAsync(
        uint attributeId, DataValue dataValue, CancellationToken ct)
    {
        if (StatusCode.IsBad(dataValue.StatusCode))
        {
            return (StatusCodes.GetBrowseName(dataValue.StatusCode.Code), "");
        }

        object? value = dataValue.Value;

        switch (attributeId)
        {
            case Attributes.NodeClass:
                return (value is int nc ? ((NodeClass)nc).ToString() : "", "");

            case Attributes.DataType:
                var dataTypeId = value as NodeId;
                string typeName = await GetDisplayNameAsync(dataTypeId, ct).ConfigureAwait(false);
                return (typeName, dataTypeId?.ToString() ?? "");

            case Attributes.ValueRank:
                return (value is int rank ? UaFormat.ValueRankName(rank) : "", value?.ToString() ?? "");

            case Attributes.AccessLevel:
            case Attributes.UserAccessLevel:
                return (value is byte al ? UaFormat.AccessLevelText(al) : "", value?.ToString() ?? "");

            case Attributes.AccessLevelEx:
                return (value is uint alx ? UaFormat.AccessLevelText((byte)alx) : "", value?.ToString() ?? "");

            case Attributes.EventNotifier:
                return (value is byte en ? UaFormat.EventNotifierText(en) : "", value?.ToString() ?? "");

            case Attributes.MinimumSamplingInterval:
                return (value is double msi ? $"{msi:0.###} ms" : "", "");

            case Attributes.Value:
                string formatted = UaFormat.FormatValue(value);
                string detail = $"{UaFormat.FormatStatusCode(dataValue.StatusCode)} | src: {dataValue.SourceTimestamp:HH:mm:ss.fff} | srv: {dataValue.ServerTimestamp:HH:mm:ss.fff}";
                return (formatted, detail);

            default:
                return (UaFormat.FormatValue(value), "");
        }
    }

    private static string FormatQualifiedName(QualifiedName? browseName)
        => browseName is null ? "" : $"{browseName.NamespaceIndex}:{browseName.Name}";
}

/// <summary>
/// Orders strings so that embedded numbers compare numerically
/// (Channel2 before Channel10).
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return string.CompareOrdinal(x, y);
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int startI = i, startJ = j;
                while (i < x.Length && char.IsDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsDigit(y[j]))
                {
                    j++;
                }

                var numX = x.AsSpan(startI, i - startI).TrimStart('0');
                var numY = y.AsSpan(startJ, j - startJ).TrimStart('0');

                if (numX.Length != numY.Length)
                {
                    return numX.Length - numY.Length;
                }

                int numCompare = numX.CompareTo(numY, StringComparison.Ordinal);
                if (numCompare != 0)
                {
                    return numCompare;
                }
            }
            else
            {
                int charCompare = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (charCompare != 0)
                {
                    return charCompare;
                }

                i++;
                j++;
            }
        }

        return (x.Length - i) - (y.Length - j);
    }
}
