namespace OpcPlc;

using System;
using System.IO;
using System.Reflection;

/// <summary>
/// Version information read from assembly attributes, safe for
/// single-file publishing (where <see cref="Assembly.Location"/> is empty).
/// </summary>
internal static class VersionInfo
{
    public static readonly Version FileVersion =
        Version.TryParse(
            typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version,
            out var version)
            ? version
            : new Version(0, 0, 0);

    public static readonly string InformationalVersion =
        typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    public static readonly DateTime BuildTimestampUtc = GetBuildTimestampUtc();

    private static DateTime GetBuildTimestampUtc()
    {
        try
        {
            string path = Environment.ProcessPath is { Length: > 0 } processPath
                ? processPath
                : Path.Combine(AppContext.BaseDirectory, "opcplc.dll");
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }
}
