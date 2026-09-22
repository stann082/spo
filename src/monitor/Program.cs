using CommandLine;
using core;
using core.config;
using core.monitor;
using core.spotify;
using Microsoft.Extensions.DependencyInjection;
using monitor;
using Serilog;

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
    var import = LegacyImport.Run();
    if (import.ConfigImported)
    {
        Log.Information("{Message}", import.Describe());
    }

    var config = ApplicationConfig.Load();

    var services = new ServiceCollection()
        .AddSingleton(config)
        .AddSingleton<ISpotifyClientFactory, SpotifyClientFactory>()
        .AddSingleton<ISnapshotStore>(_ => new SqliteSnapshotStore())
        .AddSingleton<ITopMonitorService, TopMonitorService>()
        .AddSingleton<INotifier, ToastNotifier>()
        .BuildServiceProvider();

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
