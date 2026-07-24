using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Controls;

/// <summary>
///     Regression coverage for the crash reported from V2.9.7: pressing Delete or Backspace in a
///     search bar after the text had been cleared programmatically killed the whole app with
///     "ArgumentOutOfRangeException (Parameter 'length')" out of TextBox.OnKeyDown. The search bar
///     template did not bind PART_TextPresenter, so the presenter kept the previous, longer string
///     and TextBox computed a delete range past the end of Text.
/// </summary>
public sealed class SearchBarTextBoxTests
{
    [Theory]
    [InlineData(Key.Delete)]
    [InlineData(Key.Back)]
    public Task KeyPress_AfterProgrammaticClear_DoesNotThrow(Key key)
    {
        return HeadlessUi.RunAsync(() =>
        {
            var (window, textBox) = CreateSearchBar();

            window.KeyTextInput("shader");
            Assert.Equal("shader", textBox.Text);

            // This is what the clear button and the Esc shortcut do via the view model.
            textBox.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

            Assert.Equal(string.Empty, textBox.Text);
        });
    }

    [Fact]
    public Task ProgrammaticTextChange_IsReflectedByThePresenter()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var (window, textBox) = CreateSearchBar();

            window.KeyTextInput("unitypackage");

            textBox.Text = "xr";
            Dispatcher.UIThread.RunJobs();

            var presenter = textBox.GetVisualDescendants().OfType<TextPresenter>().Single();
            Assert.Equal("xr", presenter.Text);
        });
    }

    [Fact]
    public Task Backspace_AfterShorteningTextChange_DeletesFromTheCurrentText()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var (window, textBox) = CreateSearchBar();

            window.KeyTextInput("particle system");

            textBox.Text = "part";
            Dispatcher.UIThread.RunJobs();
            textBox.CaretIndex = 4;
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);

            Assert.Equal("par", textBox.Text);
        });
    }

    [Fact]
    public Task PlaceholderText_IsShownWhileTheBoxIsEmpty()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var (window, textBox) = CreateSearchBar();
            textBox.PlaceholderText = "Search packages...";
            Dispatcher.UIThread.RunJobs();

            var placeholder = textBox.GetVisualDescendants()
                .OfType<ContentControl>()
                .Single(control => control.Name == "PART_Watermark");

            Assert.Equal("Search packages...", placeholder.Content);
            Assert.True(placeholder.IsVisible);

            window.KeyTextInput("shader");
            Dispatcher.UIThread.RunJobs();

            Assert.False(placeholder.IsVisible);
        });
    }

    private static (Window Window, TextBox TextBox) CreateSearchBar()
    {
        var textBox = new TextBox
        {
            Classes = { "search-bar" }
        };

        var window = new Window
        {
            Width = 400,
            Height = 80,
            Content = textBox
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        textBox.Focus();
        Dispatcher.UIThread.RunJobs();

        return (window, textBox);
    }
}
