namespace EasyExtract.Core;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}