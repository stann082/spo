using core.config;
using SpotifyAPI.Web;

namespace core.test;

public class ApplicationConfigTests
{

    #region Setup

    private string _directory;
    private string _configPath;

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"spo-config-test-{Guid.NewGuid():N}");
        _configPath = Path.Combine(_directory, "config.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    #endregion

    #region Tests

    [Test]
    public void Load_WhenTheFileIsMissing_ReturnsAnEmptyConfigWithoutWritingOne()
    {
        var config = ApplicationConfig.Load(_configPath);

        Assert.Multiple(() =>
        {
            Assert.That(config.HasCredentials, Is.False);
            Assert.That(config.IsLoggedIn, Is.False);
            Assert.That(File.Exists(_configPath), Is.False);
        });
    }

    [Test]
    public void Save_ThenLoad_RoundTripsEverySection()
    {
        var config = ApplicationConfig.Load(_configPath);
        config.SpotifyApp.ClientId = "id";
        config.SpotifyApp.ClientSecret = "secret";
        config.SpotifyToken.RefreshToken = "refresh";
        config.Account.DisplayName = "Me";
        config.Monitor.TimeRanges = ["short", "long"];
        config.Save();

        var loaded = ApplicationConfig.Load(_configPath);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.HasCredentials, Is.True);
            Assert.That(loaded.IsLoggedIn, Is.True);
            Assert.That(loaded.Account.DisplayName, Is.EqualTo("Me"));
            Assert.That(loaded.Monitor.TimeRanges, Is.EqualTo(new[] { "short", "long" }));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        });
    }

    [Test]
    public void ClearLogin_KeepsTheAppCredentials()
    {
        var config = ApplicationConfig.Load(_configPath);
        config.SpotifyApp.ClientId = "id";
        config.SpotifyApp.ClientSecret = "secret";
        config.SpotifyToken.RefreshToken = "refresh";
        config.Account.Id = "me";

        config.ClearLogin();

        Assert.Multiple(() =>
        {
            Assert.That(config.IsLoggedIn, Is.False);
            Assert.That(config.Account.Id, Is.Null);
            Assert.That(config.HasCredentials, Is.True);
        });
    }

    [Test]
    public void Apply_WhenARefreshOmitsTheRefreshToken_KeepsTheStoredOne()
    {
        var token = new SpotifyTokenConfig { RefreshToken = "original" };

        token.Apply(new AuthorizationCodeTokenResponse { AccessToken = "new-access", RefreshToken = null, ExpiresIn = 3600 });

        Assert.Multiple(() =>
        {
            Assert.That(token.AccessToken, Is.EqualTo("new-access"));
            Assert.That(token.RefreshToken, Is.EqualTo("original"));
        });
    }

    #endregion

}
