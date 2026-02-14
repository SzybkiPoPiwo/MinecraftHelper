using System;
using System.IO;
using System.Text.Json;
using MinecraftHelper.Models;

namespace MinecraftHelper.Services
{
    public class SettingsService
    {
        private const string SettingsFolderName = "Minecraft Helper";
        private const string SettingsFileName = "settings.json";

        public string SettingsDirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SettingsFolderName);

        public string SettingsFilePath => Path.Combine(SettingsDirectoryPath, SettingsFileName);

        private void EnsureSettingsDirectoryExists()
        {
            Directory.CreateDirectory(SettingsDirectoryPath);
        }

        public void Save(AppSettings settings)
        {
            EnsureSettingsDirectoryExists();
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }

                // First run: create a default settings file in AppData.
                AppSettings defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd odczytu: {ex.Message}");
            }
            return new AppSettings();
        }

        public void ExportToFile(AppSettings settings, string filePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Błąd eksportu ustawień.", ex);
            }
        }

        public AppSettings ImportFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Plik nie znaleziony");

                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Błąd importu ustawień.", ex);
            }
        }
    }
}
