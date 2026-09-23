namespace core.spotify;

public static class ReleaseDate
{

    /// <summary>
    /// The year from a Spotify release date, which comes as "1988", "1988-05" or "1988-05-12"
    /// depending on how precisely Spotify knows it. Empty when there is no usable year.
    /// </summary>
    public static string Year(string releaseDate)
    {
        if (string.IsNullOrWhiteSpace(releaseDate))
        {
            return string.Empty;
        }

        var year = releaseDate.Trim().Split('-')[0];
        return year.Length == 4 && year.All(char.IsAsciiDigit) && year != "0000" ? year : string.Empty;
    }

}
