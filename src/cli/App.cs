using cli.commands;
using cli.options;
using CommandLine;
using core.config;
using core.plays;
using core.spotify;

namespace cli;

public class App
{

    #region Constructors

    public App(ApplicationConfig config, ISpotifyClientFactory clientFactory, ILoginService loginService, IPlayStore playStore)
    {
        _config = config;
        _clientFactory = clientFactory;
        _loginService = loginService;
        _playStore = playStore;
    }

    #endregion

    #region Variables

    private readonly ApplicationConfig _config;
    private readonly ISpotifyClientFactory _clientFactory;
    private readonly ILoginService _loginService;
    private readonly IPlayStore _playStore;

    #endregion

    #region Public Methods

    public Task<int> RunAsync(IEnumerable<string> args)
    {
        return Parser.Default.ParseArguments<ConfigOptions,
                LoginOptions,
                LogoutOptions,
                PlaylistsOptions,
                PlaysOptions,
                TopOptions,
                TracksOptions>(args)
            .MapResult(
                (ConfigOptions opts) => ConfigCommand.ExecuteAsync(opts, _config),
                (LoginOptions opts) => LoginCommand.ExecuteAsync(opts, _loginService),
                (LogoutOptions _) => LogoutCommand.ExecuteAsync(_config),
                (PlaylistsOptions opts) => PlaylistsCommand.ExecuteAsync(opts, _clientFactory),
                (PlaysOptions opts) => PlaysCommand.ExecuteAsync(opts, _playStore),
                (TopOptions opts) => TopCommand.ExecuteAsync(opts, _clientFactory),
                (TracksOptions opts) => TracksCommand.ExecuteAsync(opts, _clientFactory),
                errors => Task.FromResult(errors.IsHelp() || errors.IsVersion() ? 0 : 2));
    }

    #endregion

}
