using cli.options;
using core;
using core.spotify;
using SpotifyAPI.Web.Auth;

namespace cli.commands;

public static class LoginCommand
{

    #region Public Methods

    public static async Task<int> ExecuteAsync(LoginOptions options, ILoginService loginService)
    {
        if (options.ListScopes)
        {
            Console.WriteLine(string.Join(Environment.NewLine, SpotifyScopes.Default));
            return 0;
        }

        if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var redirectUri))
        {
            throw new SpoException($"'{options.RedirectUri}' is not a valid redirect URI.");
        }

        if (options.TimeoutSeconds < 1)
        {
            throw new SpoException("--timeout must be at least 1 second.");
        }

        var requested = options.Scopes?.ToList() ?? [];
        var scopes = requested.Count > 0 ? requested : SpotifyScopes.Default.ToList();

        var me = await loginService.LoginAsync(
            redirectUri,
            scopes,
            TimeSpan.FromSeconds(options.TimeoutSeconds),
            authorizeUri =>
            {
                BrowserUtil.Open(authorizeUri);
                Console.WriteLine("If no browser opened, visit this URL:");
                ConsoleWrapper.WriteLine(authorizeUri.ToString(), ConsoleColor.Cyan);
            });

        ConsoleWrapper.WriteSuccess($"Logged in as {me.DisplayName} ({me.Id})");
        return 0;
    }

    #endregion

}
