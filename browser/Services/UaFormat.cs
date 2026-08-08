namespace UaScope.Services;

using Opc.Ua;
using System.Globalization;
using System.Text;

/// <summary>
/// Formatting of OPC UA values for display and parsing of user input for writes.
/// </summary>
public static class UaFormat
{
    /// <summary>
    /// Format any OPC UA value for display.
    /// </summary>
    public static string FormatValue(object? value)
    {
        switch (value)
        {
            case null:
                return "(null)";

            case string s:
                return s;

            case byte[] bytes:
                return bytes.Length <= 32
                    ? Convert.ToHexString(bytes)
                    : $"{Convert.ToHexString(bytes.AsSpan(0, 32))}… ({bytes.Length} bytes)";

            case DateTime dt:
                return dt == DateTime.MinValue ? "(no time)" : dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            case LocalizedText lt:
                return lt.Text ?? "";

            case QualifiedName qn:
                return $"{qn.NamespaceIndex}:{qn.Name}";

            case StatusCode sc:
                return FormatStatusCode(sc);

            case Uuid uuid:
                return uuid.ToString();

            case NodeId nodeId:
                return nodeId.ToString();

            case ExpandedNodeId expandedNodeId:
                return expandedNodeId.ToString();

            case ExtensionObject ext:
                return FormatExtensionObject(ext);

            case Variant variant:
                return FormatValue(variant.Value);

            case DataValue dataValue:
                return FormatValue(dataValue.Value);

            case float f:
                return f.ToString("G7", CultureInfo.InvariantCulture);

            case double d:
                return d.ToString("G15", CultureInfo.InvariantCulture);

            case System.Collections.IEnumerable enumerable and not string:
                return FormatArray(enumerable);

            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }
    }

    private static string FormatArray(System.Collections.IEnumerable enumerable)
    {
        var sb = new StringBuilder("[");
        int count = 0;
        foreach (var item in enumerable)
        {
            if (count > 0)
            {
                sb.Append(", ");
            }

            if (count >= 24)
            {
                sb.Append('…');
                break;
            }

            sb.Append(FormatValue(item));
            count++;
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string FormatExtensionObject(ExtensionObject ext)
    {
        if (ext.Body is IEncodeable encodeable)
        {
            try
            {
                // Render structure fields as JSON for readability.
                using var stream = new MemoryStream();
                using (var encoder = new JsonEncoder(ServiceMessageContext.GlobalContext, useReversibleEncoding: false, topLevelIsArray: false, stream: stream, leaveOpen: true))
                {
                    encodeable.Encode(encoder);
                    encoder.Close();
                }

                string json = Encoding.UTF8.GetString(stream.ToArray());
                return json.Length > 512 ? $"{encodeable.GetType().Name} {json[..512]}…" : $"{encodeable.GetType().Name} {json}";
            }
            catch
            {
                return encodeable.GetType().Name;
            }
        }

        return $"ExtensionObject({ext.TypeId})";
    }

    public static string FormatStatusCode(StatusCode statusCode)
    {
        string name = StatusCodes.GetBrowseName(statusCode.Code);
        return string.IsNullOrEmpty(name) ? statusCode.ToString() : name;
    }

    public static string ValueRankName(int valueRank) => valueRank switch
    {
        ValueRanks.Scalar => "Scalar",
        ValueRanks.OneDimension => "OneDimension",
        ValueRanks.OneOrMoreDimensions => "OneOrMoreDimensions",
        ValueRanks.ScalarOrOneDimension => "ScalarOrOneDimension",
        ValueRanks.Any => "Any",
        _ => $"{valueRank} dimensions",
    };

    public static string AccessLevelText(byte accessLevel)
    {
        var parts = new List<string>();
        if ((accessLevel & AccessLevels.CurrentRead) != 0)
        {
            parts.Add("Read");
        }

        if ((accessLevel & AccessLevels.CurrentWrite) != 0)
        {
            parts.Add("Write");
        }

        if ((accessLevel & AccessLevels.HistoryRead) != 0)
        {
            parts.Add("HistoryRead");
        }

        if ((accessLevel & AccessLevels.HistoryWrite) != 0)
        {
            parts.Add("HistoryWrite");
        }

        if ((accessLevel & AccessLevels.StatusWrite) != 0)
        {
            parts.Add("StatusWrite");
        }

        if ((accessLevel & AccessLevels.TimestampWrite) != 0)
        {
            parts.Add("TimestampWrite");
        }

        return parts.Count == 0 ? "None" : string.Join(" | ", parts);
    }

    public static string EventNotifierText(byte eventNotifier)
    {
        var parts = new List<string>();
        if ((eventNotifier & EventNotifiers.SubscribeToEvents) != 0)
        {
            parts.Add("SubscribeToEvents");
        }

        if ((eventNotifier & EventNotifiers.HistoryRead) != 0)
        {
            parts.Add("HistoryRead");
        }

        if ((eventNotifier & EventNotifiers.HistoryWrite) != 0)
        {
            parts.Add("HistoryWrite");
        }

        return parts.Count == 0 ? "None" : string.Join(" | ", parts);
    }

    /// <summary>
    /// Parse user-entered text into a value of the given OPC UA data type.
    /// Arrays are entered as comma-separated values.
    /// </summary>
    public static object ParseValue(string text, NodeId dataTypeId, int valueRank, ITypeTable typeTree)
    {
        BuiltInType builtInType = TypeInfo.GetBuiltInType(dataTypeId, typeTree);

        if (valueRank == ValueRanks.Scalar)
        {
            return ParseScalar(text, builtInType);
        }

        string[] parts = text.Trim().TrimStart('[').TrimEnd(']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Array array = TypeInfo.CreateArray(builtInType, parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            array.SetValue(ParseScalar(parts[i], builtInType), i);
        }

        return array;
    }

    private static object ParseScalar(string text, BuiltInType builtInType)
    {
        text = text.Trim();

        return builtInType switch
        {
            BuiltInType.Boolean => bool.Parse(text),
            BuiltInType.SByte => sbyte.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.Byte => byte.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.Int16 => short.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.UInt16 => ushort.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.Int32 or BuiltInType.Enumeration => int.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.UInt32 => uint.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.Int64 => long.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.UInt64 => ulong.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.Float => float.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.Double => double.Parse(text, CultureInfo.InvariantCulture),
            BuiltInType.String => text,
            BuiltInType.DateTime => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
            BuiltInType.Guid => Guid.Parse(text),
            BuiltInType.ByteString => Convert.FromHexString(text),
            BuiltInType.NodeId => NodeId.Parse(text),
            BuiltInType.ExpandedNodeId => ExpandedNodeId.Parse(text),
            BuiltInType.QualifiedName => QualifiedName.Parse(text),
            BuiltInType.LocalizedText => new LocalizedText(text),
            BuiltInType.StatusCode => new StatusCode(uint.Parse(text, CultureInfo.InvariantCulture)),
            _ => throw new NotSupportedException($"Writing values of type {builtInType} is not supported."),
        };
    }
}
