namespace EasyExtractCrossPlatform.ViewModels;

public sealed class CreditsViewModel : INotifyPropertyChanged
{
    private CreditProfileViewModel? _selectedProfile;

    public CreditsViewModel()
    {
        Profiles = new ObservableCollection<CreditProfileViewModel>(CreateProfiles());
        _selectedProfile = Profiles.FirstOrDefault();
    }

    public ObservableCollection<CreditProfileViewModel> Profiles { get; }

    public CreditProfileViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set => SetField(ref _selectedProfile, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return;

        storage = value;
        OnPropertyChanged(propertyName);
    }

    private static IEnumerable<CreditProfileViewModel> CreateProfiles()
    {
        yield return new CreditProfileViewModel(
            "lyze · HakuSystems",
            "@lyze",
            "Creator & maintainer",
            "Keeps EasyExtract shipping on every platform and fixes things the second they break.",
            new[]
            {
                "Vision keeper",
                "Release captain",
                "Systems designer"
            },
            new[]
            {
                "Started EasyExtract to solve a rough Unity workflow and still leads every release.",
                "Owns the extractor core, cross-platform search, and Discord status hook.",
                "Turns late-night Discord threads into real fixes, docs, and builds."
            },
            "#FF6B8D",
            "#FFC778",
            "#33FF6B8D",
            "🚀");

        yield return new CreditProfileViewModel(
            "Exploited",
            "@exploited",
            "Flow tester & chaos buddy",
            "\"EasyExtractUwUnitypackage\" legend who keeps the vibe light.",
            new[]
            {
                "UX spark",
                "Overlay hype",
                "Queue guide"
            },
            new[]
            {
                "Asked for a proper credits view instead of hidden shout-outs.",
                "Coined the UwU module idea during a 2022 chat and it still makes us grin.",
                "Breaks weird queue flows so we polish copy and animations."
            },
            "#6ED5FF",
            "#9C6BFF",
            "#266ED5FF",
            "✨");

        yield return new CreditProfileViewModel(
            "wavynews",
            "@wavynews",
            "Beta tester & DPI helper",
            "Shared the long write-up that made us fix window size, position, and scaling.",
            new[]
            {
                "Bug scout",
                "High-DPI helper",
                "Output-path fan"
            },
            new[]
            {
                "Showed how 140% scaling hid buttons, which led to saved window bounds.",
                "Asked for manual GUI scaling sliders so we keep working on accessibility.",
                "Requested a custom output path option and got it shipped."
            },
            "#58FFE0",
            "#3C9CFF",
            "#2658FFE0",
            "🛰️");

        yield return new CreditProfileViewModel(
            "GhostHugger",
            "@ghosthugger",
            "Web bug reporter",
            "Found the hosted extractor edge cases so others could finish their jobs.",
            new[]
            {
                "Website tester",
                "Browser sleuth",
                "Support scout"
            },
            new[]
            {
                "Hit the \"No destination folder\" wall on the site and shared repro steps.",
                "Confirmed Brave-specific issues so we could warn users and suggest Firefox/Chrome.",
                "Kept pinging us until the hosted UI had better copy and screenshots."
            },
            "#FF8F70",
            "#FF4F84",
            "#26FF8F70",
            "🕸️");

        yield return new CreditProfileViewModel(
            "Sparkle Games · Oğuzhan",
            "@sparklegames",
            "Asset fixer & beta tester",
            "Shared the extension-fixer idea and tons of sample data.",
            new[]
            {
                "Extension hunter",
                "Beta partner",
                "Freeze buster"
            },
            new[]
            {
                "Explained the broken extension bug after processing 1,600 packages.",
                "Sent ExtensionFixer + extensions.txt so we could improve our normalizer.",
                "Asked for backend-only extraction and optional previews to stop UI freezes."
            },
            "#FFB347",
            "#FFCC33",
            "#26FFB347",
            "🛠️");

        yield return new CreditProfileViewModel(
            "TheReaperTGM",
            "@thereapertgm",
            "Bug reporter",
            "Raised the extraction failure that sent us digging into mesh data.",
            new[]
            {
                "Mesh guard",
                "Error hunter",
                "Early user"
            },
            new[]
            {
                "Shared the July 2024 package that failed after other tools lost materials.",
                "Gave us logs that hardened the importer and error messages.",
                "Reminds us that good meshes and animations matter more than speed."
            },
            "#7B61FF",
            "#C46CFF",
            "#247B61FF",
            "🛡️");

        yield return new CreditProfileViewModel(
            "Jan-Fcloud",
            "@janfcloud",
            "Early collaborator",
            "Forked the app when it was tiny and helped build the base we use today.",
            new[]
            {
                "Fork pioneer",
                "Feature buddy",
                "Community glue"
            },
            new[]
            {
                "Shared an early fork full of fixes we merged.",
                "Paired on UX polish and pipeline choices back when EasyExtract was a rough prototype.",
                "Still drops ideas even when they live only in code or docs."
            },
            "#4CE5B6",
            "#4AA1FF",
            "#274CE5B6",
            "🌱");

        yield return new CreditProfileViewModel(
            "DigitalAzure",
            "@digitalazure",
            "Brand designer",
            "Made the EasyExtract logo and gave us the color story we lean on.",
            new[]
            {
                "Logo artist",
                "Palette guide",
                "UI mood-setter"
            },
            new[]
            {
                "Designed the EasyExtract logo and app icon you see on every splash and build.",
                "Shared palette and lighting notes that inspired the glass gradients.",
                "Still gives quick feedback whenever we tweak the hero visuals."
            },
            "#6AD1FF",
            "#9BF0FF",
            "#266AD1FF",
            "🎨");
    }

    public sealed class CreditProfileViewModel
    {
        public CreditProfileViewModel(
            string displayName,
            string handle,
            string role,
            string tagline,
            IReadOnlyList<string> highlights,
            IReadOnlyList<string> contributions,
            string accentStart,
            string accentEnd,
            string glowColor,
            string emoji)
        {
            DisplayName = displayName;
            Handle = handle;
            Role = role;
            Tagline = tagline;
            Highlights = highlights;
            Contributions = contributions;
            Emoji = emoji;
            HeroBrush = CreateHeroBrush(accentStart, accentEnd);
            AccentForegroundBrush = CreateSolidBrush(accentEnd);
            AccentBorderBrush = CreateSolidBrush(accentStart);
            ChipBackgroundBrush = CreateSolidBrush(glowColor);
            EmblemBackgroundBrush = CreateSolidBrush(glowColor, 0.45);
        }

        public string DisplayName { get; }

        public string Handle { get; }

        public string Role { get; }

        public string Tagline { get; }

        public string Emoji { get; }

        public IReadOnlyList<string> Highlights { get; }

        public IReadOnlyList<string> Contributions { get; }

        public IBrush HeroBrush { get; }

        public IBrush AccentForegroundBrush { get; }

        public IBrush AccentBorderBrush { get; }

        public IBrush ChipBackgroundBrush { get; }

        public IBrush EmblemBackgroundBrush { get; }

        private static IBrush CreateHeroBrush(string startHex, string endHex)
        {
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(ParseColor(startHex), 0),
                    new GradientStop(ParseColor(endHex), 1)
                }
            };
        }

        private static SolidColorBrush CreateSolidBrush(string hex, double? overrideOpacity = null)
        {
            var color = ParseColor(hex);
            if (overrideOpacity is { } opacity)
            {
                var alpha = (byte)(255 * opacity);
                color = Color.FromArgb(alpha, color.R, color.G, color.B);
            }

            return new SolidColorBrush(color);
        }

        private static Color ParseColor(string hex)
        {
            return Color.Parse(hex);
        }
    }
}