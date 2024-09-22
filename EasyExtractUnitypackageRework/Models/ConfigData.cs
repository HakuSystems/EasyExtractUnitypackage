namespace EasyExtractUnitypackageRework.Models;

public class ConfigData
{
    public ConfigData()
    {
        EasyConfigContent = new EasyConfigContentData();
    }

    public EasyConfigContentData EasyConfigContent { get; set; }

    public class EasyConfigContentData
    {
        public string Version { get; set; }
        public int TotalUnitypackgesExtracted { get; set; }
        public int TotalFilesExtracted { get; set; }
        public string GoFrame { get; set; }
        public bool UseDefaultTempPath { get; set; }
        public string lastTargetPath { get; set; }
        public bool HeartERPEasterEgg { get; set; }
        public bool UwUifyer { get; set; }

        public bool WindowsNotification { get; set; }
    }
}