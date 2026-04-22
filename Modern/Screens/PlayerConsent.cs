using System;
using Keen.Game2.Client.UI.Library.Dialogs.ThreeOptionsDialog;
using Pulsar.Shared;
using Pulsar.Shared.Config;
using Pulsar.Shared.Stats;

namespace Pulsar.Modern.Screens;

public static class PlayerConsent
{
    public static void ShowDialog(Action continuation = null)
    {
        var definition = ScreenTools.GetDefaultYesNoCancelDialog();
        definition.Title = ScreenTools.GetKeyFromString("Consent");
        definition.Content = ScreenTools.GetKeyFromString(
            "Would you like to rate plugins and inform developers?\r\n"
                + "\r\n"
                + "\r\n"
                + "YES: Pulsar will send the list of enabled plugins to our server\r\n"
                + "each time the game starts. Your Steam ID is sent only in hashed form,\r\n"
                + "which makes it hard to identify you. Plugin usage statistics is kept\r\n"
                + "for up to 90 days. Votes on plugins are preserved indefinitely.\r\n"
                + "Server log files and database backups may be kept up to 90 days.\r\n"
                + "Location of data storage: European Union\r\n"
                + "\r\n"
                + "\r\n"
                + "NO:   None of your data will be sent to nor stored\r\n"
                + "on our statistics server.\r\n"
                + "Pulsar will still connect to download the statistics shown.\r\n"
        );

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new ThreeOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        StoreConsent(true, continuation);
                    },
                    DefaultAction = () =>
                    {
                        ShowWithdrawConsentDialog(continuation);
                    },
                }
            );
    }

    public static bool ConsentRequested =>
        !string.IsNullOrEmpty(ConfigManager.Instance.Core.DataHandlingConsentDate);

    public static bool ConsentGiven => ConfigManager.Instance.Core.DataHandlingConsent;

    private static void ShowWithdrawConsentDialog(Action continuation = null)
    {
        var definition = ScreenTools.GetDefaultYesNoCancelDialog();
        definition.Title = ScreenTools.GetKeyFromString("Confirm consent withdrawal");
        definition.Content = ScreenTools.GetKeyFromString(
            "Are you sure to withdraw your consent to data handling?\r\n\r\n"
                + "Doing so would irrecoverably remove all your votes\r\n"
                + "and usage data from our statistics server."
        );

        ScreenTools
            .GetSharedUIComponent()
            .ShowDialog(
                new ThreeOptionsDialogViewModel(definition)
                {
                    ConfirmAction = () =>
                    {
                        StoreConsent(false, continuation);
                    },
                }
            );
    }

    private static void StoreConsent(bool consent, Action continuation)
    {
        if (ConsentRequested && consent == ConsentGiven)
        {
            continuation?.Invoke();
            return;
        }

        if (!StatsClient.Consent(consent))
        {
            LogFile.Error("Failed to register player consent on statistics server");
            return;
        }

        var config = ConfigManager.Instance.Core;
        config.DataHandlingConsentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        config.DataHandlingConsent = consent;
        config.Save();

        if (consent)
        {
            ProfilesConfig profiles = ConfigManager.Instance.Profiles;
            StatsClient.Track([.. profiles.Current.GetPluginIDs(false)]);
        }

        ConfigManager.Instance.UpdatePlayerStats();

        continuation?.Invoke();
    }
}
