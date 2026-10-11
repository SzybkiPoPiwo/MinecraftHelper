using System;
using System.IO;
using System.Text.Json;
using MinecraftHelper.Models;

namespace MinecraftHelper.Services
{
    public class SettingsService
    {
        private const string RootSettingsFolderName = "Minecraft Helper";
        private const string ProductSettingsFolderName = "Kopacz";
        private const string SettingsFileName = "settings.json";
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public string SettingsDirectoryPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                RootSettingsFolderName,
                ProductSettingsFolderName);

        public string SettingsFilePath => Path.Combine(SettingsDirectoryPath, SettingsFileName);

        private string LegacySettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            RootSettingsFolderName,
            SettingsFileName);

        public bool SettingsFileExists =>
            File.Exists(SettingsFilePath) || File.Exists(LegacySettingsFilePath);

        private void EnsureSettingsDirectoryExists()
        {
            Directory.CreateDirectory(SettingsDirectoryPath);
        }

        public void Save(AppSettings settings)
        {
            EnsureSettingsDirectoryExists();
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            WriteAllTextAtomically(SettingsFilePath, json);
        }

        public AppSettings Load()
        {
            try
            {
                string sourcePath = File.Exists(SettingsFilePath)
                    ? SettingsFilePath
                    : LegacySettingsFilePath;
                if (File.Exists(sourcePath))
                {
                    string json = File.ReadAllText(sourcePath);
                    AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    if (!string.Equals(sourcePath, SettingsFilePath, StringComparison.OrdinalIgnoreCase))
                        TryMigrateLegacySettings(settings);
                    return settings;
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

        private void TryMigrateLegacySettings(AppSettings settings)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    Save(settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nie udało się przenieść ustawień Kopacza: {ex.Message}");
            }
        }

        public void ExportToFile(AppSettings settings, string filePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(settings, SerializerOptions);
                WriteAllTextAtomically(filePath, json);
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

        private static void WriteAllTextAtomically(string filePath, string contents)
        {
            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory))
                directory = Directory.GetCurrentDirectory();

            string tempPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempPath, contents);
                File.Move(tempPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
