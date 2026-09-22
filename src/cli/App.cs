using cli.commands;
using cli.options;
using CommandLine;
using core.config;
using core.spotify;

namespace cli;

public class App
{

    #region Constructors

    public App(ApplicationConfig config, ISpotifyClientFactory clientFactory, ILoginService loginService)
    {
        _config = config;
        _clientFactory = clientFactory;
        _loginService = loginService;
    }

    #endregion

    #region Variables

    private readonly ApplicationConfig _config;
    private readonly ISpotifyClientFactory _clientFactory;
    private readonly ILoginService _loginService;

    #endregion

    #region Public Methods

    public Task<int> RunAsync(IEnumerable<string> args)
    {
        return Parser.Default.ParseArguments<ConfigOptions,
                LoginOptions,
                LogoutOptions,
                TopOptions>(args)
            .MapResult(
                (ConfigOptions opts) => ConfigCommand.ExecuteAsync(opts, _config),
                (LoginOptions opts) => LoginCommand.ExecuteAsync(opts, _loginService),
                (LogoutOptions _) => LogoutCommand.ExecuteAsync(_config),
                (TopOptions opts) => TopCommand.ExecuteAsync(opts, _clientFactory),
                _ => Task.FromResult(2));
    }

    #endregion

}
