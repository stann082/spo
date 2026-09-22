using core.config;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace core.spotify;

public class LoginService : ILoginService
{

    #region Constructors

    public LoginService(ApplicationConfig config)
    {
        _config = config;
    }

    #endregion

    #region Variables

    private readonly ApplicationConfig _config;

    #endregion

    #region Public Methods

    public async Task<PrivateUser> LoginAsync(
        Uri redirectUri,
        IReadOnlyCollection<string> scopes,
        TimeSpan timeout,
        Action<Uri> openBrowser,
        CancellationToken cancellationToken = default)
    {
        SpotifyClientFactory.RequireCredentials(_config);

        var state = Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<AuthorizationCodeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The handlers only hand the result over; the token exchange happens below, where an
        // exception reaches the caller instead of vanishing inside an event handler.
        using var server = new EmbedIOAuthServer(redirectUri, redirectUri.Port);
        server.AuthorizationCodeReceived += (_, response) =>
        {
            received.TrySetResult(response);
            return Task.CompletedTask;
        };
        server.ErrorReceived += (_, error, _) =>
        {
            received.TrySetException(new SpoException($"Spotify did not authorize the login: {error}"));
            return Task.CompletedTask;
        };

        await server.Start();
        try
        {
            var loginRequest = new LoginRequest(redirectUri, _config.SpotifyApp.ClientId, LoginRequest.ResponseType.Code)
            {
                Scope = scopes.ToList(),
                State = state
            };
            openBrowser(loginRequest.ToUri());

            AuthorizationCodeResponse response;
            try
            {
                response = await received.Task.WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new SpoException($"Timed out after {timeout.TotalSeconds:0}s waiting for the browser login.");
            }

            if (response.State != state)
            {
                throw new SpoException("The login response carried the wrong state value. Try `spo login` again.");
            }

            var token = await new OAuthClient().RequestToken(
                new AuthorizationCodeTokenRequest(
                    _config.SpotifyApp.ClientId,
                    _config.SpotifyApp.ClientSecret,
                    response.Code,
                    redirectUri),
                cancellationToken);

            var me = await new SpotifyClient(token.AccessToken).UserProfile.Current(cancellationToken);

            _config.SpotifyToken.Apply(token);
            _config.Account.Id = me.Id;
            _config.Account.DisplayName = me.DisplayName;
            _config.Account.Uri = me.Uri;
            _config.Save();

            return me;
        }
        finally
        {
            await server.Stop();
        }
    }

    #endregion

}
