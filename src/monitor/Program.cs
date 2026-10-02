using CommandLine;
using core;
using core.config;
using core.monitor;
using core.plays;
using core.spotify;
using Microsoft.Extensions.DependencyInjection;
using monitor;
using Serilog;
using SpotifyAPI.Web;

var parsed = Parser.Default.ParseArguments<MonitorOptions>(args);
if (parsed.Errors.Any())
{
    return 2;
}

var options = parsed.Value;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppPaths.LogsDirectory, "monitor-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var config = ApplicationConfig.Load();

    var services = new ServiceCollection()
        .AddSingleton(config)
        .AddSingleton<ISpotifyClientFactory, SpotifyClientFactory>()
        .AddSingleton<ISnapshotStore>(_ => new SqliteSnapshotStore())
        .AddSingleton<ITopMonitorService, TopMonitorService>()
        .AddSingleton<IPlayStore>(_ => new SqlitePlayStore())
        .AddSingleton<PlayRecorder>()
        .AddSingleton<INotifier, ToastNotifier>()
        .BuildServiceProvider();

    if (options.PlaysOnly)
    {
        // Runs every half hour, so it says one line and leaves.
        LogPlays(await services.GetRequiredService<PlayRecorder>().RecordAsync(!options.DryRun));
        return 0;
    }

    if (config.Monitor.RecordPlays)
    {
        try
        {
            LogPlays(await services.GetRequiredService<PlayRecorder>().RecordAsync(!options.DryRun));
        }
        catch (Exception ex) when (ex is APIException or HttpRequestException)
        {
            // The top lists are the point of the daily run; the next half-hourly run picks the plays up.
            Log.Warning("Play log skipped: {Message}", ex.Message);
        }
    }

    Log.Information(
        "Monitor run started (ranges: {Ranges}, limit: {Limit}, dry run: {DryRun})",
        string.Join(", ", config.Monitor.TimeRanges),
        config.Monitor.Limit,
        options.DryRun);

    var result = await services.GetRequiredService<ITopMonitorService>().RunAsync(!options.DryRun);

    foreach (var line in MonitorReport.BuildLines(result))
    {
        Log.Information("{Line}", line);
    }

    if (result.PrunedSnapshots > 0)
    {
        Log.Information("Pruned {Count} snapshots past the retention window", result.PrunedSnapshots);
    }

    if (options.NoNotify)
    {
        Log.Information("Notification skipped (--no-notify)");
    }
    else if (!result.IsWorthNotifying && !options.NotifyAlways)
    {
        // Nothing moved. A daily toast saying so would only train you to ignore them.
        Log.Information("Notification skipped: nothing changed");
    }
    else
    {
        await services.GetRequiredService<INotifier>().NotifyAsync(result);
    }

    Log.Information("Monitor run completed");
    return 0;
}
catch (SpoException ex)
{
    // Something the user has to fix (no login, bad config) - the message says what, a stack trace would not help.
    Log.Error("Monitor run failed: {Message}", ex.Message);
    return ex.ExitCode;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Monitor run failed");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void LogPlays(PlayLogResult plays)
{
    Log.Information(
        plays.DryRun ? "Play log: {Added} new play(s) would be recorded ({Fetched} fetched)" : "Play log: {Added} new play(s) recorded ({Fetched} fetched)",
        plays.Added,
        plays.Fetched);

    if (plays.MayHaveMissedPlays)
    {
        Log.Warning("Play log: every fetched play was new, so some plays since the last run were probably lost. Spotify only shows the last 50.");
    }
}
