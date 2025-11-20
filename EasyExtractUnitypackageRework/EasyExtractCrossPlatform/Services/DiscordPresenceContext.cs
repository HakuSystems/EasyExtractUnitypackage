namespace EasyExtractCrossPlatform.Services;

public readonly record struct DiscordPresenceContext(
    string State,
    string Details,
    string? CurrentPackage,
    string? NextPackage,
    int QueueCount,
    bool IsBusy)
{
    public static DiscordPresenceContext Disabled() =>
        new("Rich Presence disabled", "Discord integration disabled by user", null, null, 0, false);
}
