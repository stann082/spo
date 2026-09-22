using core.config;

namespace cli.commands;

public static class LogoutCommand
{

    #region Public Methods

    public static Task<int> ExecuteAsync(ApplicationConfig config)
    {
        config.ClearLogin();
        config.Save();
        ConsoleWrapper.WriteSuccess("Logged out.");
        return Task.FromResult(0);
    }

    #endregion

}
