using System.Text.RegularExpressions;
using System.Windows.Media;

namespace EasyExtract.Services;

public static class BetterUwUifyer
{
    private const string SpecialTokenPlaceholder = "~~EASY_EXTRACT~~";

    private static readonly Dictionary<string, string> WordReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        { "small", "smol" },
        { "really", "weawwy" },
        { "love", "wuv" },
        { "hello", "hewwo" },
        { "hi", "h-hi" }
    };

    private static readonly string[] Emojis = { "(ꈍᴗꈍ)", "^•ﻌ•^", "(◕‿◕)", "(✿◠‿◠)" };
    private static readonly string[] Interjections = { "*boops your nose*", "*screams*", "(・`ω´・)" };

    public static Settings _settings { get; set; } = new();
    public static Random _random { get; set; } = new();


    public static string UwUify(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        input = Regex.Replace(input, @"\bEasyExtractUnitypackage\b", SpecialTokenPlaceholder, RegexOptions.IgnoreCase);
        var output = input.ToLower();
        output = ReplaceWords(output);
        output = Regex.Replace(output, @"\bn([aeiou])", "ny$1", RegexOptions.IgnoreCase);
        output = Regex.Replace(output, "[rl]", "w", RegexOptions.IgnoreCase);
        output = DuplicatePunctuation(output);
        output = Regex.Replace(output, @"\b(\w+)\b", match =>
            match.Groups[1].Value.Length > 1 && _random.NextDouble() < _settings.StutterChance
                ? match.Groups[1].Value[0] + "-" + match.Groups[1].Value
                : match.Groups[1].Value);
        output = Regex.Replace(output, @"([.,!?])", match =>
            _random.NextDouble() < _settings.EmojiInsertionChance
                ? match.Value + " " + GetRandomEmoji()
                : match.Value);
        output = Regex.Replace(output, @"([.!?])\s+", match =>
            _random.NextDouble() < _settings.InterjectionInsertionChance
                ? match.Value + GetRandomInterjection() + " "
                : match.Value);
        output = output.Replace(SpecialTokenPlaceholder.ToLower(), "EasyExtractUwUnitypackage");
        return output;
    }

    private static string ReplaceWords(string input)
    {
        foreach (var kvp in WordReplacements)
        {
            var pattern = @"\b" + Regex.Escape(kvp.Key) + @"\b";
            input = Regex.Replace(input, pattern, kvp.Value, RegexOptions.IgnoreCase);
        }

        return input;
    }

    private static string DuplicatePunctuation(string input)
    {
        return Regex.Replace(input, @"([.,!?])", match =>
            _random.NextDouble() < _settings.DuplicatePunctuationChance
                ? new string(match.Value[0], _settings.DuplicatePunctuationAmount + 1)
                : match.Value);
    }

    private static string GetRandomEmoji()
    {
        return Emojis[_random.Next(Emojis.Length)];
    }

    private static string GetRandomInterjection()
    {
        return Interjections[_random.Next(Interjections.Length)];
    }

    public static void ApplyUwUModeToVisualTree(DependencyObject? parent)
    {
        if (parent == null)
            return;
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            switch (child)
            {
                case TextBlock tb:
                    tb.Text = UwUify(tb.Text);
                    break;
                case ContentControl cc when cc.Content is string cs:
                    cc.Content = UwUify(cs);
                    break;
                case HeaderedContentControl hcc when hcc.Header is string hs:
                    hcc.Header = UwUify(hs);
                    break;
            }

            if (child is FrameworkElement fe && fe.ToolTip is string tooltip)
                fe.ToolTip = UwUify(tooltip);
            ApplyUwUModeToVisualTree(child);
        }
    }

    public class Settings
    {
        public readonly int DuplicatePunctuationAmount = 2;
        public readonly float DuplicatePunctuationChance = 0.4f;
        public readonly float EmojiInsertionChance = 0.3f;
        public readonly float InterjectionInsertionChance = 0.2f;
        public readonly float StutterChance = 0.2f;
    }
}