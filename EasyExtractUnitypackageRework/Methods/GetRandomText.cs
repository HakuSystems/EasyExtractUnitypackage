using System;
using System.Net;

namespace EasyExtractUnitypackageRework.Methods;

public class GetRandomText
{
    private const string RandomTextContent = "https://nanosdk.net/EasyExtractUnitypackage/SplashText.txt";

    public static string RandomText()
    {
        var randomizer = new Random();
        using var client = new WebClient();
        var webData = client.DownloadString(RandomTextContent);
        var lines = webData.Split('\n');
        var randomLine = lines[randomizer.Next(0, lines.Length - 1)];
        client.Dispose();
        return randomLine;
    }
}