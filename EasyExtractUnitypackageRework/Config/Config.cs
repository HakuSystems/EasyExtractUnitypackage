using System;
using System.IO;
using System.Windows;
using EasyExtractUnitypackageRework.Models;
using Newtonsoft.Json;

namespace EasyExtractUnitypackageRework.Config;

public class Config
{
    public static string Version { get; set; }
    public static int TotalUnitypackgesExtracted { get; set; }
    public static int TotalFilesExtracted { get; set; }

    public static string GoFrame { get; set; }
    public static bool UseDefaultTempPath { get; set; }
    public static string lastTargetPath { get; set; }
    public static bool HeartERPEasterEgg { get; set; }
    public static bool UwUifyer { get; set; }

    public static void InitializeConfig()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var specificFolder = Path.Combine(folder, "EasyExtractUnitypackageRework");
        var configPath = Path.Combine(specificFolder, "config.json");
        if (!File.Exists(configPath)) return;
        var config = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(configPath));
        UpdateData(config);
    }

    public static void UpdateConfig()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var specificFolder = Path.Combine(folder, "EasyExtractUnitypackageRework");
        Directory.CreateDirectory(specificFolder);
        var configPath = Path.Combine(specificFolder, "config.json");
        ConfigData config;
        //check if config file exists
        if (!File.Exists(configPath))
        {
            //create config file
            config = new ConfigData();

            WriteDefaults(config);
            UpdateData(config);

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }
        else
        {
            //Write to config file
            var json = File.ReadAllText(configPath);
            config = JsonConvert.DeserializeObject<ConfigData>(json);

            WriteToConfig(config);

            var json2 = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json2);
        }
    }

    private static void UpdateData(ConfigData config) //when Config file Updates the values
    {
        Version = config.EasyConfigContent.Version;
        TotalUnitypackgesExtracted = config.EasyConfigContent.TotalUnitypackgesExtracted;
        TotalFilesExtracted = config.EasyConfigContent.TotalFilesExtracted;
        GoFrame = config.EasyConfigContent.GoFrame;
        UseDefaultTempPath = config.EasyConfigContent.UseDefaultTempPath;
        lastTargetPath = config.EasyConfigContent.lastTargetPath;
        HeartERPEasterEgg = config.EasyConfigContent.HeartERPEasterEgg;
        UwUifyer = config.EasyConfigContent.UwUifyer;
    }

    private static void WriteToConfig(ConfigData config)
    {
        config.EasyConfigContent.Version = Version;
        config.EasyConfigContent.TotalUnitypackgesExtracted = TotalUnitypackgesExtracted;
        config.EasyConfigContent.TotalFilesExtracted = TotalFilesExtracted;
        config.EasyConfigContent.GoFrame = GoFrame;
        config.EasyConfigContent.UseDefaultTempPath = UseDefaultTempPath;
        config.EasyConfigContent.lastTargetPath = lastTargetPath;
        config.EasyConfigContent.HeartERPEasterEgg = HeartERPEasterEgg;
        config.EasyConfigContent.UwUifyer = UwUifyer;
    }

    private static void WriteDefaults(ConfigData config) //When Config file was created
    {
        config.EasyConfigContent.Version = Application.ResourceAssembly.GetName().Version.ToString();
        config.EasyConfigContent.TotalFilesExtracted = 0;
        config.EasyConfigContent.TotalUnitypackgesExtracted = 0;
        config.EasyConfigContent.GoFrame = null;
        config.EasyConfigContent.UseDefaultTempPath = true;
        config.EasyConfigContent.lastTargetPath = null;
        config.EasyConfigContent.HeartERPEasterEgg = false;
        config.EasyConfigContent.UwUifyer = false;
    }
}