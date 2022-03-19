using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasyExtractUnitypackageRework.Theme.MessageBox
{
    /// <summary>
    /// Interaction logic for EasyMessageBox.xaml
    /// </summary>
    public partial class EasyMessageBox : Window
    {
        public EasyMessageBox(string Message, MessageType Type, MessageButtons Buttons)
        {
            InitializeComponent();
            txtMessage.Text = Message;
            switch (Type)
            {
                case MessageType.Info:
                    txtTitle.Text = "Information";
                    break;
                case MessageType.Confirmation:
                    txtTitle.Text = "Confirm";
                    break;
                case MessageType.Success:
                    {
                        txtTitle.Text = "Success";
                    }
                    break;
                case MessageType.Warning:
                    txtTitle.Text = "Warning";
                    break;
                case MessageType.Error:
                    {
                        txtTitle.Text = "Error";
                    }
                    break;
                case MessageType.EasterEgg:
                    {
                        txtTitle.Text = "EasterEgg";
                    }
                    break;
            }
            switch (Buttons)
            {
                case MessageButtons.OkCancel:
                    BtnYes.Visibility = Visibility.Collapsed; BtnNo.Visibility = Visibility.Collapsed;
                    break;
                case MessageButtons.YesNo:
                    BtnOk.Visibility = Visibility.Collapsed; BtnCancel.Visibility = Visibility.Collapsed;
                    break;
                case MessageButtons.Ok:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnYes.Visibility = Visibility.Collapsed; BtnNo.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
