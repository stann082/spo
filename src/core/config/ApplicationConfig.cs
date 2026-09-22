using System.Text;
using Newtonsoft.Json;

namespace core.config;

public class ApplicationConfig
{

    #region Properties

    /// <summary>The file this config was loaded from and will be saved to.</summary>
    [JsonIgnore]
    public string FilePath { get; private set; }

    public AccountConfig Account { get; } = new AccountConfig();
    public MonitorConfig Monitor { get; } = new MonitorConfig();
    public SpotifyAppConfig SpotifyApp { get; } = new SpotifyAppConfig();
    public SpotifyTokenConfig SpotifyToken { get; } = new SpotifyTokenConfig();

    [JsonIgnore]
    public bool HasCredentials => !string.IsNullOrEmpty(SpotifyApp.ClientId) && !string.IsNullOrEmpty(SpotifyApp.ClientSecret);

    [JsonIgnore]
    public bool IsLoggedIn => !string.IsNullOrEmpty(SpotifyToken.RefreshToken);

    #endregion

    #region Public Methods

    public static ApplicationConfig Load()
    {
        return Load(AppPaths.ConfigFile);
    }

    /// <summary>
    /// Reads the config, or returns an empty one if the file does not exist yet. Loading never
    /// writes; the file only appears once something is worth saving.
    /// </summary>
    public static ApplicationConfig Load(string filePath)
    {
        var config = File.Exists(filePath)
            ? JsonConvert.DeserializeObject<ApplicationConfig>(File.ReadAllText(filePath)) ?? new ApplicationConfig()
            : new ApplicationConfig();

        config.FilePath = filePath;
        return config;
    }

    /// <summary>
    /// Writes to a temp file and swaps it in, so the CLI and the monitor refreshing a token at the
    /// same moment cannot leave a half-written config behind.
    /// </summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(this, Formatting.Indented), Encoding.UTF8);
        File.Move(tempPath, FilePath, overwrite: true);
    }

    public void ClearLogin()
    {
        SpotifyToken.Clear();
        Account.Id = null;
        Account.DisplayName = null;
        Account.Uri = null;
    }

    #endregion

}
