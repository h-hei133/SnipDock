using System;
using System.Windows;
using System.Windows.Media;
using Serilog;
using Application = System.Windows.Application;

namespace SnipDock.App.Services
{
    public class ThemeService
    {
        public void ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName)) return;

            try
            {
                var dicts = Application.Current.Resources.MergedDictionaries;
                ResourceDictionary? oldDict = null;
                
                // Find existing theme dictionary by checking for key presence
                foreach (var d in dicts)
                {
                    if (d.Contains("ThemeBackgroundBrush"))
                    {
                        oldDict = d;
                        break;
                    }
                }

                var uri = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase)
                    ? "Resources/Themes/LightTheme.xaml"
                    : "Resources/Themes/DarkTheme.xaml";

                var newDict = new ResourceDictionary
                {
                    Source = new Uri(uri, UriKind.RelativeOrAbsolute)
                };

                if (oldDict != null)
                {
                    dicts.Remove(oldDict);
                }
                
                // Insert at beginning so control overrides in ThemeResources work
                dicts.Insert(0, newDict);
                Log.Information("Theme applied: {Theme}", themeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply theme: {Theme}", themeName);
            }
        }

        public void ApplyAccentColor(string colorName, string themeName)
        {
            if (string.IsNullOrEmpty(colorName)) return;

            try
            {
                string accentHex = "#7c3aed";
                string hoverHex = "#6d28d9";
                string pressedHex = "#5b21b6";
                string textHex = "#a78bfa";
                string selectedBgHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#f3e8ff" : "#251a3c";

                switch (colorName.ToLower())
                {
                    case "blue":
                        accentHex = "#3b82f6";
                        hoverHex = "#2563eb";
                        pressedHex = "#1d4ed8";
                        textHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#2563eb" : "#93c5fd";
                        selectedBgHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#dbeafe" : "#1a253c";
                        break;
                    case "green":
                        accentHex = "#10b981";
                        hoverHex = "#059669";
                        pressedHex = "#047857";
                        textHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#059669" : "#6ee7b7";
                        selectedBgHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#d1fae5" : "#132b20";
                        break;
                    case "orange":
                        accentHex = "#f97316";
                        hoverHex = "#ea580c";
                        pressedHex = "#c2410c";
                        textHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#ea580c" : "#fdba74";
                        selectedBgHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#ffedd5" : "#3c251a";
                        break;
                    case "pink":
                        accentHex = "#ec4899";
                        hoverHex = "#db2777";
                        pressedHex = "#be185d";
                        textHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#db2777" : "#fbcfe8";
                        selectedBgHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#fce7f3" : "#3c1a2d";
                        break;
                    case "purple":
                    default:
                        accentHex = "#7c3aed";
                        hoverHex = "#6d28d9";
                        pressedHex = "#5b21b6";
                        textHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#6d28d9" : "#a78bfa";
                        selectedBgHex = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "#f3e8ff" : "#251a3c";
                        break;
                }

                var res = Application.Current.Resources;
                
                SetSolidColorBrush(res, "AccentColorBrush", accentHex);
                SetSolidColorBrush(res, "AccentColorBrushMouseOver", hoverHex);
                SetSolidColorBrush(res, "AccentColorBrushPressed", pressedHex);
                SetSolidColorBrush(res, "AccentColorTextBrush", textHex);
                SetSolidColorBrush(res, "AccentColorSelectedBgBrush", selectedBgHex);

                // Apply floating ball linear gradient dynamically matching the accent color
                var colorStart = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accentHex);
                var colorEnd = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hoverHex);
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1)
                };
                gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x90, colorStart.R, colorStart.G, colorStart.B), 0));
                gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xE0, colorEnd.R, colorEnd.G, colorEnd.B), 1));
                gradient.Freeze();
                res["FloatingBallBgBrush"] = gradient;

                Log.Information("Accent color applied: {Color}", colorName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply accent color: {Color}", colorName);
            }
        }

        private void SetSolidColorBrush(ResourceDictionary res, string key, string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze(); // Freeze for high performance and thread safety in WPF
            res[key] = brush;
        }
    }
}
