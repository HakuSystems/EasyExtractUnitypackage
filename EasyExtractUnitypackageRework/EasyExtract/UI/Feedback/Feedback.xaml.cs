using System.Windows;
using System.Windows.Controls;
using EasyExtract.Discord;

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

    private void SendFeedback()
    {
        var feedbackText =
            $"{SenderName} is {FeedbackSelection.Text} with the application. Feedback: {FeedbackTextBox.Text}";
        //todo: API CALL
        MessageBox.Show("Feedback submitted successfully!", "Success", MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}