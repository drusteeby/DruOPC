namespace OpcPlc.Helpers;

using System;
using System.IO;

/// <summary>
/// Resolves files that are shipped alongside the application binaries.
/// </summary>
public static class AppFilesHelper
{
    /// <summary>
    /// Absolute path of a file copied to the build output, anchored to the
    /// snap root or the app base directory (not the CWD), so the app works
    /// when started from any directory.
    /// </summary>
    public static string GetPath(string relativePath)
    {
        var snapLocation = Environment.GetEnvironmentVariable("SNAP");
        return Path.Join(
            string.IsNullOrWhiteSpace(snapLocation) ? AppContext.BaseDirectory : snapLocation,
            relativePath);
    }
}
