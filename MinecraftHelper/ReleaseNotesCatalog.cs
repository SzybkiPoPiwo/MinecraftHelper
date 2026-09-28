using System;
using System.Collections.Generic;
using System.Linq;

namespace MinecraftHelper
{
    internal sealed record AppReleaseNotes(
        string Version,
        string Title,
        IReadOnlyList<string> Changes);

    internal static class ReleaseNotesCatalog
    {
        // Przy kolejnej wersji dodaj nowy wpis na początku listy.
        private static readonly AppReleaseNotes[] Releases =
        {
            new AppReleaseNotes(
                "1.1.2",
                "Auto Reconnect, rozbudowany Kopacz i nowe logi",
                new[]
                {
                    "Dodano konfigurowalne profile Auto Reconnect z adresem Direct Connect, komendą powrotu, opcjonalnym GUI /home, wyborem pola oraz czasami oczekiwania.",
                    "Po odzyskaniu połączenia program wznawia ten sam tryb Kopacza 5/3/3 lub 6/3/3, który działał przed rozłączeniem.",
                    "Kontrola kilofa działa podczas zaplanowanego Auto EQ i może uruchomić oddzielnie skonfigurowany powrót do home po wykryciu problemu.",
                    "Rozbudowano Auto EQ o niezależny tryb wyrzucania całej zawartości, wybór typów i slotów oraz opcjonalne jedzenie mięsa ze slotu 2.",
                    "Przebudowano logi kopania: jedno uruchomienie Kopacza tworzy nadrzędną sesję, a kolejne skany EQ i zdarzenia automatyzacji są dostępne w rozwijanych szczegółach.",
                    "Logi zapisują tryb wyrzucania, wyniki przedmiotów i stosów, CobbleX, jedzenie oraz przebieg Auto Reconnect; raporty CSV i TXT zawierają nowe dane sesji.",
                    "Uporządkowano sekcję Kopacza, dodano czytelne obramowania kart i zakładek, zmniejszono domyślne okno oraz wyśrodkowano je przy uruchomieniu.",
                    "Dodano blokadę uruchomienia drugiej kopii Minecraft Helper i poprawiono obsługę wejścia, aby działające GUI nie powodowało przycięć podczas gwałtownych ruchów myszy.",
                    "AUTO LPM i AUTO PPM nie przechwytują bindów przy widocznym kursorze Minecrafta; ograniczenie nie zatrzymuje pozostałych modułów."
                }),
            new AppReleaseNotes(
                "1.1.1",
                "Logi kopania i dokładniejsze Auto EQ",
                new[]
                {
                    "Dodano trwałe logi kopania z liczbą utworzonych CobbleXów oraz wyrzuconych sztuk i stosów.",
                    "Dodano wyszukiwanie w historii oraz eksport aktualnie widocznych wpisów do raportu CSV lub TXT.",
                    "Poprawiono liczenie wyrzuconych przedmiotów — wynik jest potwierdzany ponownym skanem ekwipunku i uwzględnia liczebność stosów.",
                    "Auto EQ odsuwa kursor poza ekwipunek i wykonuje do trzech przebiegów, aby tooltip nie zasłaniał przedmiotów.",
                    "Ujednolicono wygląd komunikatu usuwania historii z ciemnym motywem aplikacji.",
                    "Przycisk minimalizacji pozostawia aplikację na pasku zadań, a zamknięcie przyciskiem X przenosi ją do zasobnika i wyświetla powiadomienie.",
                    "Każde otwarcie EQ tworzy osobną, rozwijaną sesję logu z kolorowym statusem i listą wyrzuconych przedmiotów.",
                    "AUTO PPM oraz HOLD PPM nie mogą zostać uruchomione, gdy Minecraft pokazuje kursor ekwipunku, chatu lub innego GUI.",
                    "HUD pokazuje czas pracy, historię skanów EQ i wyrzucania podczas kopania oraz czas bieżącej sesji łowienia.",
                    "Poprawiono precyzję AUTO LPM, AUTO PPM i HOLD PPM — ustawione 20 CPS nie traci już szybkości przez opóźnienia timera.",
                    "Poprawiono rozpoznawanie kursora w trybie pełnoekranowym — przezroczysty kursor rozgrywki nie blokuje makra, a otwarte GUI nadal je zatrzymuje."
                }),
            new AppReleaseNotes(
                "1.1.0",
                "Auto EQ, CobbleX i poprawki interfejsu",
                new[]
                {
                    "Dodano Auto EQ z wyborem typów przedmiotów i slotów 1–27 oraz wyrzucaniem całych stosów przez lewy Ctrl + Q.",
                    "Dodano wykrywanie pełnych stacków cobblestone i automatyczne wykonywanie komendy CobbleX.",
                    "Rozszerzono HUD o stan Auto EQ, licznik cobblestone i wynik ostatniego skanu.",
                    "Poprawiono stabilność clickerów, obsługę wejścia oraz niekontrolowane przesunięcia kursora.",
                    "Uproszczono zakładki PVP i Experimental oraz dodano animowane, rozwijane sekcje z pomocą pod przyciskami ?.",
                    "Dodano komunikat pierwszego uruchomienia i informacje o zmianach pomiędzy wersjami."
                })
        };

        public static string CurrentVersion
        {
            get
            {
                Version? version = typeof(ReleaseNotesCatalog).Assembly.GetName().Version;
                if (version == null)
                    return "1.1.2";

                return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
            }
        }

        public static IReadOnlyList<AppReleaseNotes> GetNotesForStartup(
            string? previousVersion,
            string currentVersion,
            bool isFirstRun)
        {
            Version? previous = ParseVersion(previousVersion);
            Version? current = ParseVersion(currentVersion);

            IEnumerable<AppReleaseNotes> notes = Releases;
            if (current != null)
            {
                notes = notes.Where(note =>
                {
                    Version? noteVersion = ParseVersion(note.Version);
                    return noteVersion != null && noteVersion <= current;
                });
            }

            if (!isFirstRun && previous != null && current != null && previous < current)
            {
                notes = notes.Where(note =>
                {
                    Version? noteVersion = ParseVersion(note.Version);
                    return noteVersion != null && noteVersion > previous;
                });
            }
            else
            {
                notes = notes.Where(note =>
                    string.Equals(note.Version, currentVersion, StringComparison.OrdinalIgnoreCase));
            }

            List<AppReleaseNotes> result = notes
                .OrderByDescending(note => ParseVersion(note.Version))
                .ToList();

            if (result.Count == 0)
            {
                result.Add(new AppReleaseNotes(
                    currentVersion,
                    "Zmiany w aplikacji",
                    new[]
                    {
                        "Ta wersja zawiera zmiany programu. Zapoznaj się z opisami funkcji dostępnymi pod przyciskami ?."
                    }));
            }

            return result;
        }

        public static int CompareVersions(string? left, string? right)
        {
            Version? leftVersion = ParseVersion(left);
            Version? rightVersion = ParseVersion(right);

            if (leftVersion == null && rightVersion == null)
                return 0;
            if (leftVersion == null)
                return -1;
            if (rightVersion == null)
                return 1;

            return leftVersion.CompareTo(rightVersion);
        }

        private static Version? ParseVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string normalized = value.Trim();
            int suffixIndex = normalized.IndexOfAny(new[] { '+', '-' });
            if (suffixIndex >= 0)
                normalized = normalized[..suffixIndex];

            return Version.TryParse(normalized, out Version? version) ? version : null;
        }
    }
}
