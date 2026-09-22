using SpotifyAPI.Web;

namespace core.spotify;

public interface ISpotifyClientFactory
{

    /// <summary>
    /// A client acting as the logged-in user. Throws <see cref="SpoException"/> when there are no
    /// app credentials or no login yet.
    /// </summary>
    ISpotifyClient CreateUserClient();

}
