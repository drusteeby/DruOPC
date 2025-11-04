namespace OpcPlc.Configuration.Parsers;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Parser for comma-separated list of strings with optional double quotes.
/// </summary>
public static class StringListParser
{
    /// <summary>
    /// Parses a comma-separated list of strings.
    /// Supports both formats:
    /// - "string1,string2,string3"
    /// - ""string1","string2","string3""
    /// </summary>
    public static List<string> Parse(string list)
    {
        if (string.IsNullOrWhiteSpace(list))
        {
            return new List<string>();
        }

        var strings = new List<string>();

        // Check if the list uses double quotes
        if (list[0] == '"' && list.Count(c => c.Equals('"')) % 2 == 0)
        {
            return ParseQuotedList(list);
        }
        else if (list.Contains(','))
        {
            return list.Split(',')
                .Select(st => st.Trim())
                .ToList();
        }
        else
        {
            return new List<string> { list };
        }
    }

    private static List<string> ParseQuotedList(string list)
    {
        var strings = new List<string>();

        while (list.Contains('"'))
        {
            int first = list.IndexOf('"');
            int next = list.IndexOf('"', first + 1);

            if (next == -1)
            {
                break;
            }

            strings.Add(list.Substring(first + 1, next - first - 1));
            list = list.Substring(next + 1);
        }

        return strings;
    }
}
