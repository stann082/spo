using CommandLine;

namespace cli.options;

[Verb("config", HelpText = "Set or show the Spotify app credentials. With no options, shows the current config.")]
public class ConfigOptions
{

    [Option("client-id", SetName = "explicit", HelpText = "Client id of your Spotify app.")]
    public string ClientId { get; set; }

    [Option("client-secret", SetName = "explicit", HelpText = "Client secret of your Spotify app.")]
    public string ClientSecret { get; set; }

    [Option("from-env", SetName = "env", HelpText = "Read the client id and secret from SPOTIFY_CLIENT_ID and SPOTIFY_CLIENT_SECRET.")]
    public bool FromEnv { get; set; }

}
