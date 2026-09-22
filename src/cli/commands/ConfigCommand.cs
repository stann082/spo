using cli.options;
using core;
using core.config;

namespace cli.commands;

public static class ConfigCommand
{

    #region Public Methods

    public static Task<int> ExecuteAsync(ConfigOptions options, ApplicationConfig config)
    {
        string clientId = options.ClientId;
        string clientSecret = options.ClientSecret;

        if (options.FromEnv)
        {
            clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
            clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new SpoException("Both SPOTIFY_CLIENT_ID and SPOTIFY_CLIENT_SECRET must be set.");
            }
        }

        if (string.IsNullOrEmpty(clientId) && string.IsNullOrEmpty(clientSecret))
        {
            Show(config);
            return Task.FromResult(0);
        }

        if (!string.IsNullOrEmpty(clientId))
        {
            config.SpotifyApp.ClientId = clientId;
        }

        if (!string.IsNullOrEmpty(clientSecret))
        {
            config.SpotifyApp.ClientSecret = clientSecret;
        }

        config.Save();
        ConsoleWrapper.WriteSuccess($"Saved to {config.FilePath}");
        return Task.FromResult(0);
    }

    #endregion

    #region Helper Methods

    private static void Show(ApplicationConfig config)
    {
        Console.WriteLine($"Config file:    {config.FilePath}");
        Console.WriteLine($"Client id:      {config.SpotifyApp.ClientId ?? "(not set)"}");
        Console.WriteLine($"Client secret:  {Mask(config.SpotifyApp.ClientSecret)}");
        Console.WriteLine($"Logged in as:   {(config.IsLoggedIn ? $"{config.Account.DisplayName} ({config.Account.Id})" : "(not logged in)")}");
        Console.WriteLine($"Monitor ranges: {string.Join(", ", config.Monitor.TimeRanges)} (top {config.Monitor.Limit}, keep {config.Monitor.RetentionDays} days)");
    }

    private static string Mask(string secret)
    {
        return string.IsNullOrEmpty(secret) ? "(not set)" : $"****{secret[^Math.Min(4, secret.Length)..]}";
    }

    #endregion

}
