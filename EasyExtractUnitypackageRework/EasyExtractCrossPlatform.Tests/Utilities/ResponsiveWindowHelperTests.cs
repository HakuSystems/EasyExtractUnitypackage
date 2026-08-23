using Avalonia.Controls;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Tests.Support;
using EasyExtractCrossPlatform.Utilities;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Utilities;

public sealed class ResponsiveWindowHelperTests
{
    [Fact]
    public Task Enable_WhenWindowWidthChanges_UpdatesLayoutClasses()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var window = new Window
            {
                Width = 1700,
                Height = 900
            };
            using var subscription = ResponsiveWindowHelper.Enable(window);

            window.Show();
            Assert.Contains("wide", window.Classes);

            window.Width = 1000;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("cozy", window.Classes);
            Assert.DoesNotContain("wide", window.Classes);

            window.Width = 900;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("compact", window.Classes);
            Assert.DoesNotContain("cozy", window.Classes);

            window.Close();
        });
    }
}
