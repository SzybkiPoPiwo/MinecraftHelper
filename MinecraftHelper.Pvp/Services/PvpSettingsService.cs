using System;
using System.IO;
using System.Text.Json;
using MinecraftHelper.Models;

namespace MinecraftHelper.Services;

internal sealed class PvpSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string AppDataPath =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public string SettingsPath { get; } = Path.Combine(
        AppDataPath,
        "Minecraft Helper",
        "AutoClicker",
        "settings.json");

    private static string LegacySettingsPath { get; } = Path.Combine(
        AppDataPath,
        "MinecraftHelper",
        "Pvp",
        "settings.json");

    public bool SettingsFileExists =>
        File.Exists(SettingsPath) || File.Exists(LegacySettingsPath);

    public PvpSettings Load()
    {
        try
        {
            string sourcePath = File.Exists(SettingsPath)
                ? SettingsPath
                : LegacySettingsPath;
            if (!File.Exists(sourcePath))
                return new PvpSettings();

            PvpSettings settings = JsonSerializer.Deserialize<PvpSettings>(
                    File.ReadAllText(sourcePath),
                    JsonOptions)
                ?? new PvpSettings();
            if (!string.Equals(sourcePath, SettingsPath, StringComparison.OrdinalIgnoreCase))
                TryMigrateLegacySettings(settings);
            return settings;
        }
        catch
        {
            return new PvpSettings();
        }
    }

    public void Save(PvpSettings settings)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private void TryMigrateLegacySettings(PvpSettings settings)
    {
        try
        {
            if (!File.Exists(SettingsPath))
                Save(settings);
        }
        catch
        {
            // Starsze ustawienia nadal zostały poprawnie wczytane; zapis ponowi się przy zmianie opcji.
        }
    }
}

