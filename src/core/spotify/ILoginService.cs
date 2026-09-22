using SpotifyAPI.Web;

namespace core.spotify;

public interface ILoginService
{

    /// <summary>
    /// Runs the OAuth authorization-code flow: listens on <paramref name="redirectUri"/>, hands the
    /// authorize URL to <paramref name="openBrowser"/>, exchanges the code, and saves the tokens
    /// and account to config. Throws <see cref="SpoException"/> on denial, timeout or bad state.
    /// </summary>
    Task<PrivateUser> LoginAsync(
        Uri redirectUri,
        IReadOnlyCollection<string> scopes,
        TimeSpan timeout,
        Action<Uri> openBrowser,
        CancellationToken cancellationToken = default);

}
