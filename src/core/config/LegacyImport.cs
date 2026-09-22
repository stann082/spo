namespace core.config;

/// <summary>
/// Carries the spoticli login and monitor history over to spo, once. It only runs while spo has no
/// config of its own, only copies (the legacy app keeps working untouched), and never overwrites.
/// </summary>
public static class LegacyImport
{

    #region Public Methods

    public static LegacyImportResult Run()
    {
        return Run(AppPaths.LegacyRoot, AppPaths.Root);
    }

    public static LegacyImportResult Run(string legacyRoot, string root)
    {
        var configPath = Path.Combine(root, AppPaths.ConfigFileName);
        var legacyConfigPath = Path.Combine(legacyRoot, AppPaths.ConfigFileName);

        if (File.Exists(configPath) || !File.Exists(legacyConfigPath))
        {
            return LegacyImportResult.None;
        }

        Directory.CreateDirectory(root);
        File.Copy(legacyConfigPath, configPath);

        bool historyImported = false;
        var historyPath = Path.Combine(root, AppPaths.HistoryFileName);
        var legacyHistoryPath = Path.Combine(legacyRoot, AppPaths.HistoryFileName);
        if (File.Exists(legacyHistoryPath) && !File.Exists(historyPath))
        {
            File.Copy(legacyHistoryPath, historyPath);
            historyImported = true;
        }

        return new LegacyImportResult(true, historyImported, legacyRoot);
    }

    #endregion

}

public record LegacyImportResult(bool ConfigImported, bool HistoryImported, string LegacyRoot)
{

    public static readonly LegacyImportResult None = new(false, false, null);

    public string Describe()
    {
        var what = HistoryImported ? "login and monitor history" : "login";
        return $"Imported your spoticli {what} from {LegacyRoot}. The legacy app is unchanged.";
    }

}
