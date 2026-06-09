namespace SnipDock.Core.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", System.StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public string Theme { get; set; } = "Dark";
        public string AccentColor { get; set; } = "Purple";

        // Phase 4 Daily Efficiency Enhancements
        public bool FocusSearchOnOpen { get; set; } = true;
        public bool SelectSearchTextOnOpen { get; set; } = true;
        public bool HidePanelAfterCopy { get; set; } = false;
        public bool ClearSearchAfterCopy { get; set; } = false;

        // Phase 5 Pinned & Startup Preferences
        public bool IsStartupEnabled { get; set; } = false;
        public int DataSchemaVersion { get; set; } = 1;
    }
}
