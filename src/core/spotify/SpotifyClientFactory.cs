using core.config;
using SpotifyAPI.Web;

namespace core.spotify;

public class SpotifyClientFactory : ISpotifyClientFactory
{

    #region Constructors

    public SpotifyClientFactory(ApplicationConfig config)
    {
        _config = config;
    }

    #endregion

    #region Variables

    private readonly ApplicationConfig _config;

    #endregion

    #region Public Methods

    public ISpotifyClient CreateUserClient()
    {
        RequireCredentials(_config);

        if (!_config.IsLoggedIn)
        {
            throw new SpoException("Not logged in. Run `spo login` first.");
        }

        var token = _config.SpotifyToken;
        var authenticator = new AuthorizationCodeAuthenticator(
            _config.SpotifyApp.ClientId,
            _config.SpotifyApp.ClientSecret,
            new AuthorizationCodeTokenResponse
            {
                AccessToken = token.AccessToken ?? string.Empty,
                RefreshToken = token.RefreshToken,
                TokenType = token.TokenType ?? "Bearer",
                // Missing values make the token read as expired, which just forces a refresh.
                CreatedAt = token.CreatedAt ?? DateTime.MinValue,
                ExpiresIn = token.ExpiresIn ?? 0
            });

        // The authenticator refreshes in memory; persisting it here keeps config.json current, so
        // the next run starts with a live token instead of refreshing again.
        authenticator.TokenRefreshed += (_, refreshed) =>
        {
            _config.SpotifyToken.Apply(refreshed);
            _config.Save();
        };

        var clientConfig = SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator(authenticator)
            .WithRetryHandler(new SimpleRetryHandler());

        return new SpotifyClient(clientConfig);
    }

    public static void RequireCredentials(ApplicationConfig config)
    {
        if (!config.HasCredentials)
        {
            throw new SpoException("Spotify app credentials are not set. Run `spo config --client-id <id> --client-secret <secret>` first.");
        }
    }

    #endregion

}
