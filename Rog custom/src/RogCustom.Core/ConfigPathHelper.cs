namespace RogCustom.Core;

/// <summary>
/// Resolves config directory: portable (well-known file beside exe) or LocalApplicationData.
/// Prefer LocalApplicationData over ApplicationData to avoid roaming config/logs between machines.
/// </summary>
public static class ConfigPathHelper
{
    /// <summary>
    /// Well-known config file name. If this file exists next to the executable, config is portable.
    /// </summary>
    public const string PortableMarkerFileName = "RogCustom.portable";

    /// <summary>
    /// Gets the directory for config and data. Uses executable directory if portable marker exists;
    /// otherwise uses LocalApplicationData\RogCustom (or similar).
    /// </summary>
    /// <param name="getExecutableDirectory">Function that returns the directory containing the executable (injected for testability).</param>
    /// <param name="getLocalAppData">Function that returns LocalApplicationData path (injected for testability).</param>
    public static string GetConfigDirectory(
        Func<string> getExecutableDirectory,
        Func<string> getLocalAppData)
    {
        var exeDir = getExecutableDirectory();
        var portablePath = Path.Combine(exeDir, PortableMarkerFileName);
        if (File.Exists(portablePath))
            return exeDir;

        var localAppData = getLocalAppData();
        var appFolder = Path.Combine(localAppData, "RogCustom");
        return appFolder;
    }

    /// <summary>
    /// Default implementation using AppContext.BaseDirectory and Environment.GetFolderPath.
    /// </summary>
    public static string GetConfigDirectory()
    {
        return GetConfigDirectory(
            () => Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory,
            () => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }
}
