namespace core.config;

/// <summary>
/// Where spo keeps its state. Everything lives under %APPDATA%\spo; the legacy spoticli folder is
/// only ever read, by <see cref="LegacyImport"/>.
/// </summary>
public static class AppPaths
{

    public const string AppName = "spo";
    public const string LegacyAppName = "spoticli";

    public const string ConfigFileName = "config.json";
    public const string HistoryFileName = "history.db";

    private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static string Root => Path.Combine(AppData, AppName);

    public static string LegacyRoot => Path.Combine(AppData, LegacyAppName);

    public static string ConfigFile => Path.Combine(Root, ConfigFileName);

    public static string HistoryDatabase => Path.Combine(Root, HistoryFileName);

    public static string LogsDirectory => Path.Combine(Root, "logs");

}
