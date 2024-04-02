using System.Windows.Controls;
using System.Windows.Input;

namespace EasyExtract.UserControls;

public partial class Settings : UserControl
{
    public Settings()
    {
        InitializeComponent();
    }


    private void UserExExpander_OnMouseEnter(object sender, MouseEventArgs e)
    {
        UserExExpander.IsExpanded = true;
    }

    private void UserExExpander_OnMouseLeave(object sender, MouseEventArgs e)
    {
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Tick += (o, args) =>
        {
            UserExExpander.IsExpanded = false;
            timer.Stop();
        };
        timer.Interval = new System.TimeSpan(0, 0, 1);
        timer.Start();
    }

    private void NotifUpExpander_OnMouseEnter(object sender, MouseEventArgs e)
    {
        NotifUpExpander.IsExpanded = true;
    }

    private void NotifUpExpander_OnMouseLeave(object sender, MouseEventArgs e)
    {
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Tick += (o, args) =>
        {
            NotifUpExpander.IsExpanded = false;
            timer.Stop();
        };
        timer.Interval = new System.TimeSpan(0, 0, 1);
        timer.Start();
    }

    private void IntigExpander_OnMouseEnter(object sender, MouseEventArgs e)
    {
        IntigExpander.IsExpanded = true;
    }

    private void IntigExpander_OnMouseLeave(object sender, MouseEventArgs e)
    {
        var timer = new System.Windows.Threading.DispatcherTimer(); 
        timer.Tick += (o, args) =>
        {
            IntigExpander.IsExpanded = false;
            timer.Stop();
        };
        timer.Interval = new System.TimeSpan(0, 0, 1);  
        timer.Start();
    }

    private void PathExpander_OnMouseEnter(object sender, MouseEventArgs e)
    {
        PathExpander.IsExpanded = true;
    }

    private void PathExpander_OnMouseLeave(object sender, MouseEventArgs e)
    {
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Tick += (o, args) =>
        {
            PathExpander.IsExpanded = false;
            timer.Stop();
        };
        timer.Interval = new System.TimeSpan(0, 0, 1);
        timer.Start();
    }
}