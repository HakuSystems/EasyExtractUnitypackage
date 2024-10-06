using System.Text;
using EasyExtract.Services;
using EasyExtract.Services.Discord;
using Newtonsoft.Json;

namespace EasyExtract.UI.Feedback;

public partial class Feedback : UserControl
{
    public Feedback()
    {
        InitializeComponent();
    }

    private string SenderName
    {
        get => DiscordRpcManager.Instance.client.CurrentUser != null
            ? DiscordRpcManager.Instance.client.CurrentUser.Username
            : "Anonymous";
    }

    private void Feedback_OnLoaded(object sender, RoutedEventArgs e)
    {
        GetDiscordUsername();
        UpdateDiscordPresence();
    }

    private async void UpdateDiscordPresence()
    {
        await DiscordRpcManager.Instance.UpdatePresenceAsync("Feedback");
    }

    private void GetDiscordUsername()
    {
        DiscordNameRequest.Text = DiscordRpcManager.Instance.client.CurrentUser != null
            ? $"Sending Request as {DiscordRpcManager.Instance.client.CurrentUser.Username}"
            : "Sending Request as Anonymous";
    }

    private async void SubmitFeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FeedbackTextBox.Text))
        {
            await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Warning", "Please enter your feedback.", "OK");
            return;
        }

        if (FeedbackSelection.SelectedIndex.Equals(-1))
        {
            await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Warning", "Please select your satisfaction level.",
                "OK");
            return;
        }

        SendFeedback();
        FeedbackTextBox.Text = string.Empty;
        FeedbackSelection.SelectedIndex = -1; // Reset the selection
    }

    private async void SendFeedback()
    {
        var feedbackData = new
        {
            user = SenderName,
            satisfaction = FeedbackSelection.Text,
            feedback = FeedbackTextBox.Text,
            appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString()
        };
        var url = "https://hooker.zkwolf.com/easy_extractor/feedback";
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
                await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Success", "Feedback submitted successfully.", "OK");
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic errorResponse = JsonConvert.DeserializeObject(responseContent);
                string errorMessage = errorResponse.message;
                string errorStatus = errorResponse.status;
                await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), errorStatus.ToUpper(), errorMessage, "OK");
            }
        }
        catch (HttpRequestException)
        {
            await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Error",
                "Network error. Please check your internet connection and try again.", "OK");
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorDialogAsync(Window.GetWindow(this), "Error",
                "An unexpected error occurred. Please check the logs for more information.", "OK");
        }
    }
}