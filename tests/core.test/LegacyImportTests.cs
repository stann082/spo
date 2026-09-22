using core.config;

namespace core.test;

public class LegacyImportTests
{

    #region Setup

    private string _base;
    private string _legacyRoot;
    private string _root;

    [SetUp]
    public void Setup()
    {
        _base = Path.Combine(Path.GetTempPath(), $"spo-import-test-{Guid.NewGuid():N}");
        _legacyRoot = Path.Combine(_base, "spoticli");
        _root = Path.Combine(_base, "spo");
        Directory.CreateDirectory(_legacyRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    #endregion

    #region Tests

    [Test]
    public void Run_CopiesConfigAndHistoryWhenSpoHasNoConfig()
    {
        File.WriteAllText(Path.Combine(_legacyRoot, "config.json"), "{\"legacy\":true}");
        File.WriteAllText(Path.Combine(_legacyRoot, "history.db"), "db");

        var result = LegacyImport.Run(_legacyRoot, _root);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigImported, Is.True);
            Assert.That(result.HistoryImported, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(_root, "config.json")), Is.EqualTo("{\"legacy\":true}"));
            // The legacy app must keep working, so its files stay where they were.
            Assert.That(File.Exists(Path.Combine(_legacyRoot, "config.json")), Is.True);
        });
    }

    [Test]
    public void Run_DoesNothingOnceSpoHasItsOwnConfig()
    {
        File.WriteAllText(Path.Combine(_legacyRoot, "config.json"), "legacy");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "config.json"), "mine");

        var result = LegacyImport.Run(_legacyRoot, _root);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigImported, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(_root, "config.json")), Is.EqualTo("mine"));
        });
    }

    [Test]
    public void Run_WithoutALegacyConfig_CreatesNothing()
    {
        var result = LegacyImport.Run(_legacyRoot, _root);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigImported, Is.False);
            Assert.That(Directory.Exists(_root), Is.False);
        });
    }

    [Test]
    public void Run_WithoutLegacyHistory_ImportsTheConfigOnly()
    {
        File.WriteAllText(Path.Combine(_legacyRoot, "config.json"), "{}");

        var result = LegacyImport.Run(_legacyRoot, _root);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConfigImported, Is.True);
            Assert.That(result.HistoryImported, Is.False);
            Assert.That(result.Describe(), Does.Contain("login from"));
        });
    }

    #endregion

}
