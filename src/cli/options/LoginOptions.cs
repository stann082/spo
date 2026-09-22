using CommandLine;

namespace cli.options;

[Verb("login", HelpText = "Log into your Spotify account through the browser (OAuth2).")]
public class LoginOptions
{

    public const string DefaultRedirectUri = "http://127.0.0.1:5000/callback";

    [Option("redirect-uri", Default = DefaultRedirectUri, HelpText = "Callback URL to listen on. Must also be registered as a redirect URI on your Spotify app. The port is taken from it.")]
    public string RedirectUri { get; set; }

    [Option("scopes", Separator = ',', HelpText = "Comma-separated scopes to request instead of the defaults (see --list-scopes).")]
    public IEnumerable<string> Scopes { get; set; }

    [Option("timeout", Default = 300, HelpText = "Seconds to wait for the browser login.")]
    public int TimeoutSeconds { get; set; }

    [Option("list-scopes", HelpText = "Print the scopes requested by default and exit.")]
    public bool ListScopes { get; set; }

}
