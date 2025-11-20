using System;
using System.Diagnostics;
using DiscordRPC;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class DiscordRpcService
{
    private RichPresence BuildPresence(AppSettings settings, DiscordPresenceContext context)
    {
        if (context.IsBusy != _lastPresenceWasBusy)
        {
            _timestamps = new Timestamps(DateTime.UtcNow);
            _lastPresenceWasBusy = context.IsBusy;
        }

        var timestamps = context.IsBusy
            ? _timestamps ??= new Timestamps(DateTime.UtcNow)
            : null;

        return new RichPresence
        {
            Details = TrimToLimit(DetailsText, DiscordStringLimit),
            State = TrimToLimit(BuildStateText(context), DiscordStringLimit),
            Timestamps = timestamps,
            Assets = new Assets
            {
                LargeImageKey = LargeImageKey,
                LargeImageText = BuildLargeImageText(settings, context),
                SmallImageKey = SmallImageKey,
                SmallImageText = BuildSmallImageText(settings, context)
            }
        };
    }

    private static string BuildLargeImageText(AppSettings settings, DiscordPresenceContext context)
    {
        var packages = Math.Max(0, settings.TotalExtracted);
        var files = Math.Max(0, settings.TotalFilesExtracted);
        var baseCounts = $"U: {packages} | F: {files}";

        if (context.IsBusy)
        {
            var current = string.IsNullOrWhiteSpace(context.CurrentPackage)
                ? "Extracting assets"
                : $"Extracting {context.CurrentPackage}";
            var queueSuffix = context.QueueCount > 0 ? $" | {context.QueueCount} left" : string.Empty;
            return TrimToLimit($"{current}{queueSuffix} | {baseCounts}", DiscordStringLimit);
        }

        if (context.QueueCount > 0)
        {
            var nextSuffix = string.IsNullOrWhiteSpace(context.NextPackage)
                ? string.Empty
                : $" | Next: {context.NextPackage}";
            return TrimToLimit($"{baseCounts}{nextSuffix}", DiscordStringLimit);
        }

        return TrimToLimit(baseCounts, DiscordStringLimit);
    }

    private static string BuildSmallImageText(AppSettings settings, DiscordPresenceContext context)
    {
        var version = VersionProvider.GetApplicationVersion();
        var tier = string.IsNullOrWhiteSpace(settings.LicenseTier)
            ? "Free"
            : settings.LicenseTier.Trim();

        var baseText = string.IsNullOrWhiteSpace(version)
            ? "EasyExtract"
            : $"EasyExtract v{version}";

        var queueCount = Math.Max(0, context.QueueCount);
        var queueSuffix = queueCount > 0
            ? $" | {queueCount} in queue"
            : string.Empty;

        var composed = $"{baseText} - {tier} tier{queueSuffix}";
        return TrimToLimit(composed, DiscordStringLimit);
    }

    private static string BuildStateText(DiscordPresenceContext context)
    {
        if (context.IsBusy)
        {
            if (!string.IsNullOrWhiteSpace(context.CurrentPackage))
                return $"Extracting {context.CurrentPackage}";

            return "Extracting assets";
        }

        var normalized = string.IsNullOrWhiteSpace(context.State) ? "Dashboard" : context.State.Trim();
        return $"Viewing {normalized} Page";
    }

    private static string TrimToLimit(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string CreatePresenceSignature(RichPresence presence, DiscordPresenceContext context)
    {
        var assets = presence.Assets;
        return
            $"{presence.Details}|{presence.State}|{assets?.LargeImageText}|{assets?.SmallImageText}|{context.QueueCount}|{context.CurrentPackage}|{context.NextPackage}|{context.IsBusy}";
    }

    private void ApplyPresenceLocked(AppSettings settings, DiscordPresenceContext context, bool force = false)
    {
        if (_client is null || _client.IsDisposed)
            return;

        try
        {
            if (!_client.IsInitialized)
                return;

            var presence = BuildPresence(settings, context);
            var signature = CreatePresenceSignature(presence, context);

            if (!force && string.Equals(_lastPresenceSignature, signature, StringComparison.Ordinal))
                return;

            _client.SetPresence(presence);
            _lastPresenceSignature = signature;
            EnsureKeepAliveTimer();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscordRPC] Failed to set presence: {ex}");
            Log($"Failed to set presence: {ex}");
        }
    }
}
