using core.config;
using Microsoft.Data.Sqlite;

namespace core.monitor;

/// <summary>
/// Stores snapshots in a single SQLite file, by default alongside the CLI config in
/// %APPDATA%\spo\history.db.
/// </summary>
public class SqliteSnapshotStore : ISnapshotStore
{

    #region Constructors

    public SqliteSnapshotStore() : this(DefaultDatabasePath)
    {
    }

    public SqliteSnapshotStore(string databasePath)
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

    public static string DefaultDatabasePath => AppPaths.HistoryDatabase;

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
            CREATE TABLE IF NOT EXISTS snapshots (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                captured_at  TEXT NOT NULL,
                entity_type  TEXT NOT NULL,
                time_range   TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_snapshots_lookup
                ON snapshots (entity_type, time_range, captured_at DESC);

            CREATE TABLE IF NOT EXISTS entries (
                snapshot_id  INTEGER NOT NULL REFERENCES snapshots (id) ON DELETE CASCADE,
                "rank"       INTEGER NOT NULL,
                spotify_id   TEXT NOT NULL,
                name         TEXT NOT NULL,
                detail       TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (snapshot_id, "rank")
            );

            CREATE INDEX IF NOT EXISTS ix_entries_spotify ON entries (spotify_id);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TopSnapshot> GetLatestAsync(TopEntityType entityType, string timeRange, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);

        await using var header = connection.CreateCommand();
        header.CommandText = """
            SELECT id, captured_at
            FROM snapshots
            WHERE entity_type = $entityType AND time_range = $timeRange
            ORDER BY captured_at DESC, id DESC
            LIMIT 1;
            """;
        header.Parameters.AddWithValue("$entityType", Encode(entityType));
        header.Parameters.AddWithValue("$timeRange", timeRange);

        long snapshotId;
        DateTime capturedAt;

        await using (var reader = await header.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            snapshotId = reader.GetInt64(0);
            capturedAt = ParseTimestamp(reader.GetString(1));
        }

        return new TopSnapshot
        {
            Id = snapshotId,
            CapturedAt = capturedAt,
            EntityType = entityType,
            TimeRange = timeRange,
            Entries = await ReadEntriesAsync(connection, snapshotId, cancellationToken)
        };
    }

    public async Task SaveAsync(TopSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertSnapshot = connection.CreateCommand())
        {
            insertSnapshot.CommandText = """
                INSERT INTO snapshots (captured_at, entity_type, time_range)
                VALUES ($capturedAt, $entityType, $timeRange);
                SELECT last_insert_rowid();
                """;
            insertSnapshot.Parameters.AddWithValue("$capturedAt", FormatTimestamp(snapshot.CapturedAt));
            insertSnapshot.Parameters.AddWithValue("$entityType", Encode(snapshot.EntityType));
            insertSnapshot.Parameters.AddWithValue("$timeRange", snapshot.TimeRange);

            snapshot.Id = (long)await insertSnapshot.ExecuteScalarAsync(cancellationToken);
        }

        await using (var insertEntry = connection.CreateCommand())
        {
            insertEntry.CommandText = """
                INSERT INTO entries (snapshot_id, "rank", spotify_id, name, detail)
                VALUES ($snapshotId, $rank, $spotifyId, $name, $detail);
                """;

            var snapshotId = insertEntry.Parameters.Add("$snapshotId", SqliteType.Integer);
            var rank = insertEntry.Parameters.Add("$rank", SqliteType.Integer);
            var spotifyId = insertEntry.Parameters.Add("$spotifyId", SqliteType.Text);
            var name = insertEntry.Parameters.Add("$name", SqliteType.Text);
            var detail = insertEntry.Parameters.Add("$detail", SqliteType.Text);

            snapshotId.Value = snapshot.Id;

            foreach (var entry in snapshot.Entries)
            {
                rank.Value = entry.Rank;
                spotifyId.Value = entry.SpotifyId;
                name.Value = entry.Name;
                detail.Value = entry.Detail ?? string.Empty;
                await insertEntry.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM snapshots WHERE captured_at < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", FormatTimestamp(olderThanUtc));

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Helper Methods

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<List<TopEntry>> ReadEntriesAsync(SqliteConnection connection, long snapshotId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "rank", spotify_id, name, detail
            FROM entries
            WHERE snapshot_id = $snapshotId
            ORDER BY "rank";
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        var entries = new List<TopEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new TopEntry(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return entries;
    }

    private static string Encode(TopEntityType entityType)
    {
        return entityType == TopEntityType.Artist ? "artist" : "track";
    }

    // Sortable, culture-independent and comparable as text, which is what the ORDER BY relies on.
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
