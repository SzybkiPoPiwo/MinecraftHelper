> Projekt jest aktywnie rozwijany. Część funkcji oznaczonych jako Experimental może zawierać błędy.

# MinecraftHelper

MinecraftHelper to desktopowa aplikacja WPF (.NET 8) na Windows do konfiguracji i uruchamiania makr pod Minecraft.

<img width="3168" height="1344" alt="MinecraftHelper Preview" src="https://github.com/user-attachments/assets/1ee1fa6b-54a1-4432-8733-1936e8c83b55" />

---

## 1. Co to jest i dla kogo

MinecraftHelper to narzędzie dla graczy Minecraft:

- GUI do konfiguracji makr i bindów
- działanie runtime pod wybrane okno gry
- zapis ustawień bez ręcznej edycji plików JSON

Projekt jest open-source i rozwijany modułowo.

---

## 2. Co działa teraz (stan aktualny)

- PVP:
  - HOLD (`LPM + PPM`) z jednym bindem i osobnymi checkboxami LPM/PPM
  - `AUTO LPM` (bind + CPS)
  - `AUTO PPM` (bind + CPS)
  - `Jabłka z liści` (bind + komenda)
- Kopacz:
  - `Kopacz 5/3/3`
  - `Kopacz 6/3/3` (na wprost / do góry)
  - harmonogram komend z opóźnieniami
- BINDY:
  - wiele wierszy (własna nazwa, bind, komenda, enabled)
  - pojedyncze wykonanie komendy po naciśnięciu binda
- Experimental:
  - OCR F3 pod odczyt `E: x/x`
  - tryb niestandardowego obszaru OCR + bind zaznaczania + reset danych
- Ustawienia:
  - wybór procesu gry z listy uruchomionych okien
  - konfiguracja HUD overlay (włącz, animacje, monitor, pozycja)
  - import/export ustawień
- UI:
  - status bar na górze
  - kafelki statusu trybów
  - wskaźniki `Minecraft Focus` i `Settings Saved`
  - minimalizacja do traya

---

## 3. Jak działa interfejs (skrót)

### Górny panel

- `NAZWA OKNA`: aktualnie zapisany proces/okno gry
- kafelki statusów:
  - `LPM + PPM`
  - `AUTO LPM`
  - `AUTO PPM`
  - `KOPACZ 5/3/3`
  - `KOPACZ 6/3/3`
  - `Jabłka z liści`
  - `Pauza ekwipunku`
- pasek stanu:
  - `Minecraft Focus` - czy aplikacja widzi fokus wybranego okna gry
  - `Settings Saved` - czy ustawienia są już zapisane

### Zakładki

#### PVP

<img width="1584" height="1000" alt="image" src="https://github.com/user-attachments/assets/149a4656-dc0e-4d63-810c-8601da4e483e" />

- `LPM + PPM HOLD`:
  - 1 bind do aktywacji modułu
  - osobne checkboxy: `Lewy przycisk (LPM)` i `Prawy przycisk (PPM)`
  - osobne zakresy `Min CPS` / `Max CPS`
- `AUTOMATYCZNY LPM`:
  - checkbox włączający
  - bind
  - zakres CPS
- `AUTOMATYCZNY PPM`:
  - checkbox włączający
  - bind
  - zakres CPS
- `Jabłka z liści`:
  - checkbox włączający
  - bind
  - komenda (zapisywana z GUI)
  - opis działania: 70 cykli slotów + chat `T` + `ENTER` + `/repair`
- `Pauza gdy kursor widoczny`:
  - zatrzymuje klikanie, gdy jest widoczny kursor (ekwipunek/GUI)

#### Kopacz

<img width="1584" height="1000" alt="image" src="https://github.com/user-attachments/assets/aa8e0cd7-37f6-4a4c-afcc-225b3a0659d6" />

- `Kopacz 5/3/3`:
  - checkbox włączający
  - bind
  - lista komend (`+ Dodaj komendę`) z opóźnieniem w sekundach
- `Kopacz 6/3/3`:
  - checkbox włączający
  - bind
  - kierunek: `Na wprost` / `Do góry`
  - szerokość / długość (zależnie od kierunku)
  - lista komend
- panel informacyjny:
  - podpowiedzi optymalnych czasów
  - AFK facing dla trybu "Do góry"
  - konwerter minut -> sekundy

#### BINDY

<img width="1584" height="1000" alt="image" src="https://github.com/user-attachments/assets/a8f238cc-d068-4a2a-8242-9b7b1d704c31" />

- checkbox `Włącz moduł BINDY`
- każdy wiersz ma:
  - `Enabled`
  - `Nazwa`
  - `Klawisz` (zapis bindu)
  - `Komenda`
  - usuwanie wiersza
- jeden bind uruchamia jednorazowy flow:
  - otwarcie chatu (`T`)
  - wpisanie komendy
  - zatwierdzenie (`ENTER`)

#### Experimental

<img width="1584" height="1000" alt="image" src="https://github.com/user-attachments/assets/7dedebef-8485-4187-9f41-cec82212cebb" />

- checkbox `Wykrywanie graczy (E)`
- OCR oparty o linię F3 `E: x/x`
- `Niestandardowy obszar OCR`:
  - bind do uruchamiania zaznaczania
  - przycisk `Zaznacz obszar`
  - przycisk `Resetuj dane`
- live odczyt:
  - `Encje (E): x/x`

#### Ustawienia

<img width="1584" height="1000" alt="image" src="https://github.com/user-attachments/assets/96f6f217-5023-4761-ad70-a785f0935889" />

- `Program gry`:
  - lista aktywnych procesów z oknem
  - przycisk `Odśwież`
  - przycisk `Zapisz program`
- `Ustawienia HUD (overlay)`:
  - `Panel HUD`
  - `Animacje HUD`
  - wybór monitora
  - pozycja: prawy/lewy, górny/dolny róg
- `Import/Export ustawień`:
  - eksport do JSON
  - import z JSON

---

## 4. Zapis ustawień - jak działa

Aplikacja zapisuje ustawienia automatycznie.

- każda zmiana w GUI oznacza ustawienia jako "niezapisane"
- po krótkiej chwili bez kolejnych zmian (debounce ~1s) działa auto-zapis
- status w UI:
  - `Settings Saved: ✗ Nie` - zmiany czekają na zapis
  - `Settings Saved: ✓ Tak` - zapisane
  - `Błąd` - problem z zapisem
- ręczny przycisk `Zapisz program` zapisuje od razu wybór procesu gry

Domyślna lokalizacja pliku:

- `%AppData%\Minecraft Helper\settings.json`

---

## 5. Wymagania

### Dla użytkownika końcowego (instalator)

- Windows 10/11
- brak potrzeby instalowania .NET osobno, jeśli instalator jest zbudowany jako self-contained

### Dla dewelopera (uruchamianie z kodu)

- Windows 10/11
- .NET 8 SDK

---

## 6. Szybki start (użytkownik)

1. Zainstaluj aplikację z instalatora.
2. W zakładce `Ustawienia` wybierz `Program gry` i kliknij `Zapisz program`.
3. W zakładce `PVP` / `Kopacz` / `BINDY` ustaw bindy i opcje.
4. Sprawdź na górze:
   - `Minecraft Focus`
   - `Settings Saved`
5. Wróć do gry i uruchamiaj moduły bindami.

---

## 7. Uruchomienie lokalnie (z kodu)

```bash
git clone https://github.com/SzybkiPoPiwo/MinecraftHelper.git
cd MinecraftHelper
dotnet restore
dotnet build MinecraftHelper.slnx
dotnet run --project MinecraftHelper/MinecraftHelper.csproj
```

---

## 8. Budowanie instalatora (.exe)

W repo jest gotowy skrypt:

- `scripts/build-installer.ps1`

Wymagania:

- Inno Setup 6 (`ISCC.exe`)
- .NET 8 SDK

Przykład:

```powershell
cd D:\MinecraftHelper
Set-ExecutionPolicy -Scope Process Bypass
$env:ISCC_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
.\scripts\build-installer.ps1 -Version 1.0.0 -Rid win-x64 -SelfContained:$true -Clean
```

Wynik:

- publish: `artifacts/publish/win-x64`
- instalator: `artifacts/installer/MinecraftHelper-Setup-1.0.0.exe`

Uwagi:

- domyślnie build jest multi-file (stabilniejszy dla OCR/Tesseract)
- opcjonalny single-file: dodaj `-SingleFile`

---

## 9. Import / Export ustawień

- Export zapisuje aktualną konfigurację do wskazanego pliku `.json`.
- Import:
  - ładuje ustawienia z pliku `.json`
  - przechodzi walidację spójności
  - odświeża GUI
  - zapisuje jako aktywne ustawienia lokalne

---
## 11. Kontakt

- GitHub: https://github.com/SzybkiPoPiwo
- Discord: `twojstaryricardo`
