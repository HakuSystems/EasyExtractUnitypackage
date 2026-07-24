using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Controls;

/// <summary>
///     The error dialog shows the crash details in a read-only TextBox. If the user can silently
///     edit that box, the details they copy and send back are no longer the details we logged.
/// </summary>
public sealed class ReadOnlyTextBoxTests
{
    private const string Details = "System.InvalidOperationException: boom";

    [Theory]
    [InlineData(Key.Back)]
    [InlineData(Key.Delete)]
    public Task ReadOnlyTextBox_IgnoresDeletionKeys(Key key)
    {
        return HeadlessUi.RunAsync(() =>
        {
            var textBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                Text = Details
            };

            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = textBox
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();
            textBox.Focus();
            textBox.CaretIndex = Details.Length / 2;
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

            Assert.Equal(Details, textBox.Text);
        });
    }
}
