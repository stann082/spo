using core.config;
using Microsoft.Data.Sqlite;

namespace core.plays;

/// <summary>
/// Keeps the play log in the same SQLite file as the monitor's snapshots, by default
/// %APPDATA%\spo\history.db. Plays are never pruned: the counts are only worth something if
/// they keep adding up.
/// </summary>
public class SqlitePlayStore : IPlayStore
{

    #region Constructors

    public SqlitePlayStore() : this(AppPaths.HistoryDatabase)
    {
    }

    public SqlitePlayStore(string databasePath)
    {
        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    #endregion

    #region Variables

    private readonly string _connectionString;

    #endregion

    #region Properties

    public string DatabasePath { get; }

    #endregion

    #region Public Methods

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS plays (
                played_at  TEXT NOT NULL,
                track_id   TEXT NOT NULL,
                name       TEXT NOT NULL,
                artists    TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (played_at, track_id)
            );

            CREATE INDEX IF NOT EXISTS ix_plays_track ON plays (track_id);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestPlayedAtAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(played_at) FROM plays;";

        return await command.ExecuteScalarAsync(cancellationToken) is string value ? ParseTimestamp(value) : null;
    }

    public async Task<int> SaveAsync(IReadOnlyList<PlayRecord> plays, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plays);

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var insert = connection.CreateCommand();

        // The same play comes back on every poll until fifty newer ones push it out.
        insert.CommandText = """
            INSERT OR IGNORE INTO plays (played_at, track_id, name, artists)
            VALUES ($playedAt, $trackId, $name, $artists);
            """;

        var playedAt = insert.Parameters.Add("$playedAt", SqliteType.Text);
        var trackId = insert.Parameters.Add("$trackId", SqliteType.Text);
        var name = insert.Parameters.Add("$name", SqliteType.Text);
        var artists = insert.Parameters.Add("$artists", SqliteType.Text);

        int added = 0;
        foreach (var play in plays)
        {
            playedAt.Value = FormatTimestamp(play.PlayedAt);
            trackId.Value = play.TrackId;
            name.Value = play.Name;
            artists.Value = play.Artists ?? string.Empty;
            added += await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return added;
    }

    public async Task<IReadOnlyList<PlayCount>> GetCountsAsync(int limit, string query = null, DateTime? sinceUtc = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // instr rather than LIKE, so a "%" or "_" in the search text means just that.
        command.CommandText = """
            SELECT track_id, name, artists, COUNT(*) AS plays, MIN(played_at), MAX(played_at) AS last_played
            FROM plays
            WHERE ($since IS NULL OR played_at >= $since)
              AND ($query IS NULL OR instr(lower(name), lower($query)) > 0 OR instr(lower(artists), lower($query)) > 0)
            GROUP BY track_id
            ORDER BY plays DESC, last_played DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$since", sinceUtc.HasValue ? FormatTimestamp(sinceUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$query", string.IsNullOrWhiteSpace(query) ? DBNull.Value : query.Trim());
        command.Parameters.AddWithValue("$limit", limit);

        var counts = new List<PlayCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts.Add(new PlayCount(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                ParseTimestamp(reader.GetString(4)),
                ParseTimestamp(reader.GetString(5))));
        }

        return counts;
    }

    public async Task<PlayLogSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), COUNT(DISTINCT track_id), MIN(played_at) FROM plays;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new PlayLogSummary(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : ParseTimestamp(reader.GetString(2)));
    }

    #endregion

    #region Helper Methods

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    // Sortable and comparable as text, which is what MAX, MIN and the "since" filter rely on.
    private static string FormatTimestamp(DateTime value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
    }

    private static DateTime ParseTimestamp(string value)
    {
        return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
    }

    #endregion

}
