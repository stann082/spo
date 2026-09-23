using core;
using core.config;
using core.spotify;
using Microsoft.Extensions.DependencyInjection;
using SpotifyAPI.Web;

namespace cli;

public static class Program
{

    #region Main Method

    /// <summary>
    /// The only place that turns failures into exit codes. Commands throw <see cref="SpoException"/>
    /// for anything the user can fix; nothing below here calls Environment.Exit.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var services = new ServiceCollection()
                .AddSingleton(ApplicationConfig.Load())
                .AddSingleton<ISpotifyClientFactory, SpotifyClientFactory>()
                .AddSingleton<ILoginService, LoginService>()
                .AddSingleton<App>()
                .BuildServiceProvider();

            return await services.GetRequiredService<App>().RunAsync(args);
        }
        catch (SpoException ex)
        {
            ConsoleWrapper.WriteError(ex.Message);
            return ex.ExitCode;
        }
        catch (APIException ex)
        {
            var status = ex.Response != null ? $" ({(int)ex.Response.StatusCode})" : string.Empty;
            ConsoleWrapper.WriteError($"Spotify API error{status}: {ex.Message}");
            return 1;
        }
    }

    #endregion

}
