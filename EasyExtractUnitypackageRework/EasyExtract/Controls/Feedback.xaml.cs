using System.Text;
using EasyExtract.Config;
using EasyExtract.Services;
using EasyExtract.Utilities.Logger;
using EasyExtract.Views;
using Newtonsoft.Json;

namespace EasyExtract.Controls;

public partial class Feedback
{
    public Feedback()
    {
        InitializeComponent();
    }

    private static string SenderName =>
        DiscordRpcManager.Instance.Client?.CurrentUser != null
            ? DiscordRpcManager.Instance.Client.CurrentUser.Username
            : "Anonymous";

    private async void Feedback_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Visible;

            if (ConfigHandler.Instance.Config.UwUModeActive) BetterUwUifyer.ApplyUwUModeToVisualTree(this);
            GetDiscordUsername();
            await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Feedback");
        }
        catch (Exception exception)
        {
            BetterLogger.Exception(exception, "Error in Feedback Loaded", "Feedback");
        }
    }

    private void GetDiscordUsername()
    {
        DiscordNameRequest.Text = DiscordRpcManager.Instance.Client?.CurrentUser != null
            ? $"Sending Request as {DiscordRpcManager.Instance.Client.CurrentUser.Username}"
            : "Sending Request as Anonymous";
    }

    private async void SubmitFeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FeedbackTextBox.Text))
            {
                await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Warning",
                    "Please enter your feedback.",
                    "OK");
                return;
            }

            if (FeedbackSelection.SelectedIndex.Equals(-1))
            {
                await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Warning",
                    "Please select your satisfaction level.",
                    "OK");
                return;
            }

            await SendFeedback();
            FeedbackTextBox.Text = string.Empty;
            FeedbackSelection.SelectedIndex = -1; // Reset the selection
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error in SubmitFeedbackButton_OnClick", "Feedback");
        }
    }

    private async Task SendFeedback()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            var feedbackData = new
            {
                user = SenderName,
                satisfaction = FeedbackSelection.Text,
                feedback = FeedbackTextBox.Text,
                appVersion = version.ToString()
            };
            var url = new StringBuilder()
                .Append(
                    "\u0068\u0074\u0074\u0070\u0073\u003a\u002f\u002f\u0068\u006f\u006f\u006b\u0065\u0072\u002e\u007a\u006b\u0077\u006f\u006c\u0066\u002e\u0063\u006f\u006d\u002f\u0065\u0061\u0073\u0079\u005f\u0065\u0078\u0074\u0072\u0061\u0063\u0074\u006f\u0072\u002f\u0066\u0065\u0065\u0064\u0062\u0061\u0063\u006b")
                .ToString();
            var jsonData = JsonConvert.SerializeObject(new
            {
                data = feedbackData
            });
            try
            {
                using HttpClient client = new();
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Success",
                        "Feedback submitted successfully.",
                        "OK");
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic errorResponse = (JsonConvert.DeserializeObject(responseContent) ?? null) ??
                                            throw new InvalidOperationException();
                    if (errorResponse != null)
                    {
                        string errorMessage = errorResponse.message;
                        string errorStatus = errorResponse.status;
                        await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), errorStatus.ToUpper(),
                            errorMessage, "OK");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                BetterLogger.Exception(ex, "Network error while submitting feedback", "Feedback");
                await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Error",
                    "Network error. Please check your internet connection and try again.", "OK");
            }
            catch (Exception ex)
            {
                BetterLogger.Exception(ex, "Unexpected error while submitting feedback", "Feedback");
                await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Error",
                    "An unexpected error occurred. Please check the logs for more information.", "OK");
            }
        }
    }
}