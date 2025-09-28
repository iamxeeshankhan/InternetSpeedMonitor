using System;
using System.IO;
using System.Text.Json;

namespace InternetSpeedMonitor
{
    /// <summary>
    /// Serializable user preferences for the overlay and app behavior.
    /// </summary>
    public class UserSettings
	{
		public bool OverlayEnabled { get; set; } = true;
		public string TextColor { get; set; } = "White";
		public string BackgroundOption { get; set; } = "No Background"; // or color name
		public string Position { get; set; } = "Top Right";
		public string FontFamily { get; set; } = "Segoe UI";
		public double FontSize { get; set; } = 14;
		public string FontVariant { get; set; } = "Bold";
	}

    /// <summary>
    /// Loads and saves <see cref="UserSettings"/> to AppData as JSON.
    /// </summary>
    public static class SettingsService
	{
		private static string GetSettingsPath()
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var dir = Path.Combine(appData, "InternetSpeedMonitor");
			Directory.CreateDirectory(dir);
			return Path.Combine(dir, "settings.json");
		}

		public static UserSettings Load()
		{
			try
			{
				var path = GetSettingsPath();
				if (!File.Exists(path)) return new UserSettings();
				var json = File.ReadAllText(path);
				var settings = JsonSerializer.Deserialize<UserSettings>(json);
				return settings ?? new UserSettings();
			}
			catch
			{
				return new UserSettings();
			}
		}

		public static void Save(UserSettings settings)
		{
			try
			{
				var path = GetSettingsPath();
				var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(path, json);
			}
			catch
			{
				// ignore persistence errors
			}
		}
	}
}


