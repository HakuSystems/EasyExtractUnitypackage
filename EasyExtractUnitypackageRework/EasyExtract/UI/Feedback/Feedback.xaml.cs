using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using EasyExtract.Discord;
using Newtonsoft.Json;

namespace EasyExtract.UI.Feedback;

public partial class Feedback : UserControl
{
    public Feedback()
    {
        InitializeComponent();
    }

    private string SenderName => DiscordRpcManager.Instance.client.CurrentUser != null
        ? DiscordRpcManager.Instance.client.CurrentUser.Username
        : "Anonymous";

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

    private void SubmitFeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FeedbackTextBox.Text))
        {
            MessageBox.Show("Please enter your feedback.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (FeedbackSelection.SelectedIndex.Equals(-1))
        {
            MessageBox.Show("Please select your satisfaction level.", "Warning", MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
        var jsonData = JsonConvert.SerializeObject(new { data = feedbackData });
        try
        {
            using HttpClient client = new();
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("Feedback submitted successfully!", "Success", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic errorResponse = JsonConvert.DeserializeObject(responseContent);
                string errorMessage = errorResponse.message;
                string errorStatus = errorResponse.status;
                MessageBox.Show(errorMessage, errorStatus, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (HttpRequestException)
        {
            MessageBox.Show("Network error. Please check your internet connection and try again.", "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}