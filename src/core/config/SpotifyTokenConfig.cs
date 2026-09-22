using SpotifyAPI.Web;

namespace core.config;

public class SpotifyTokenConfig
{

    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public int? ExpiresIn { get; set; }
    public string TokenType { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Copies a token response in. Spotify often leaves the refresh token out of a refresh
    /// response, in which case the one already stored stays valid and is kept.
    /// </summary>
    public void Apply(AuthorizationCodeTokenResponse response)
    {
        AccessToken = response.AccessToken;
        ExpiresIn = response.ExpiresIn;
        TokenType = response.TokenType;
        CreatedAt = response.CreatedAt;

        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            RefreshToken = response.RefreshToken;
        }
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        ExpiresIn = null;
        TokenType = null;
        CreatedAt = null;
    }

}
