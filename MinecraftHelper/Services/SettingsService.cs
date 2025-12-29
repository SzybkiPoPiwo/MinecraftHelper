using System;
using System.IO;
using System.Text.Json;
using MinecraftHelper.Models;

namespace MinecraftHelper.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "MinecraftHelper");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _settingsPath = Path.Combine(dir, "settings.json");
        }

        public AppSettings Load()
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
    }
}
