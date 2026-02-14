# MinecraftHelper

MinecraftHelper to desktopowa aplikacja WPF (.NET 8) na Windows do konfigurowania i uruchamiania makr pod Minecraft.

## Aktualny stan projektu

Aktualna wersja zawiera działające GUI, zapis ustawien oraz logike runtime dla trybow PVP/Kopacz/Jablka z lisci.

## Co dziala teraz

- Tryby PVP:
  - `LPM + PPM` (HOLD): aktywacja bindem, LPM jako toggle klikania, PPM podczas przytrzymania.
  - `AUTO LPM`: automatyczne klikanie po aktywacji bindem.
  - `AUTO PPM`: automatyczne klikanie po aktywacji bindem.
- Tryb `Jablka z lisci`:
  - bind start/stop,
  - cykl slot 1 + slot 2,
  - opcjonalna komenda wysylana cyklicznie (konfigurowalna w GUI).
- Tryby `Kopacz`:
  - `Kopacz 5/3/3` z lista komend i opoznieniami,
  - `Kopacz 6/3/3` (`Na wprost` / `Do gory`) z konfiguracja szerokosci/dlugosci,
  - wykonywanie komend czatowych w czasie pracy makra.
- Warunki i bezpieczenstwo runtime:
  - dzialanie tylko przy fokusie wybranego okna gry (`TargetWindowTitle`),
  - opcjonalna pauza makr przy widocznym kursorze (ekwipunek/GUI).
- Ustawienia:
  - auto-zapis ustawien,
  - import/export JSON,
  - historia tytulow okna gry (max 5).
- UI:
  - status bar,
  - kafelki stanu trybow,
  - ciemny motyw.

## Wymagania

- Windows 10/11
- .NET 8 SDK

## Uruchomienie lokalnie

```bash
git clone https://github.com/SzybkiPoPiwo/MinecraftHelper.git
cd MinecraftHelper
dotnet restore
dotnet build MinecraftHelper.slnx
dotnet run --project MinecraftHelper/MinecraftHelper.csproj
```

## Konfiguracja i pliki ustawien

Domyslny plik ustawien tworzony jest automatycznie tutaj:

`%AppData%\Minecraft Helper\settings.json`

Plik `settings.json` w root repo jest ignorowany i nie powinien byc commitowany.

## Uwagi

- Aplikacja symuluje klawisze i klikniecia myszy na poziomie systemowym (WinAPI).
- Uzywaj zgodnie z regulaminem serwera, launchera i gry.

## Roadmap (skrot)

- dalsza stabilizacja runtime i podzial logiki na mniejsze serwisy,
- rozbudowa panelu bindow,
- rozbudowa monitoringu statusow i logow.

## Kontakt

- GitHub: https://github.com/SzybkiPoPiwo
- Discord: `twojstaryricardo`
