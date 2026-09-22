using System.Runtime.Versioning;
using System.Security;
using core.monitor;
using Microsoft.Win32;
using Serilog;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace monitor;

/// <summary>
/// Raises a Windows toast through the WinRT notification APIs.
///
/// An unpackaged desktop app can only post toasts under an AppUserModelID that Windows knows
/// about, so the notifier registers its own AUMID under HKCU on first use. That registration is
/// per-user, idempotent, and is what makes the toast land in Action Center with a proper name
/// instead of being dropped silently.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public class ToastNotifier : INotifier
{

    #region Constants

    public const string AppUserModelId = "spo.TopMonitor";

    private const string DisplayName = "spo";
    private const int MaxBodyLines = 3;

    #endregion

    #region Public Methods

    public Task NotifyAsync(MonitorRunResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureRegistered();

            var document = new XmlDocument();
            document.LoadXml(BuildToastXml(result));

            ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(new ToastNotification(document));
            Log.Information("Toast notification raised");
        }
        catch (Exception ex)
        {
            // A failed notification must not fail the run - the snapshot is already saved and the
            // full report is in the log.
            Log.Error(ex, "Could not raise the toast notification");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes the per-user AUMID registration Windows needs before it will accept toasts from an
    /// unpackaged app. Safe to call repeatedly.
    /// </summary>
    public static void EnsureRegistered()
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{AppUserModelId}");
        key?.SetValue("DisplayName", DisplayName, RegistryValueKind.String);
    }

    /// <summary>Removes the AUMID registration, used by the uninstall path.</summary>
    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\AppUserModelId\{AppUserModelId}", throwOnMissingSubKey: false);
    }

    #endregion

    #region Helper Methods

    private static string BuildToastXml(MonitorRunResult result)
    {
        string title = MonitorReport.BuildToastTitle(result);
        var body = MonitorReport
            .BuildToastBody(result, MaxBodyLines)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // ToastGeneric allows a title plus three body lines; anything longer belongs in the log.
        var texts = body
            .Take(MaxBodyLines)
            .Select(line => $"<text>{SecurityElement.Escape(line)}</text>");

        return $"""
            <toast activationType="foreground">
              <visual>
                <binding template="ToastGeneric">
                  <text>{SecurityElement.Escape(title)}</text>
                  {string.Join(Environment.NewLine, texts)}
                </binding>
              </visual>
            </toast>
            """;
    }

    #endregion

}
