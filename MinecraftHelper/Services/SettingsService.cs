using System;
using System.IO;
using System.Text.Json;
using MinecraftHelper.Models;

namespace MinecraftHelper.Services
{
    public class SettingsService
    {
        private const string SettingsFile = "settings.json";

        public void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd zapisu: {ex.Message}");
            }
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
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
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd eksportu: {ex.Message}");
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
                throw new Exception($"Błąd importu: {ex.Message}");
            }
        }
    }
}
