# Samodzielne sprawdzenie i budowanie Minecraft Helper

Ten poradnik pozwala zbudować aplikację bez uruchamiania gotowego instalatora. Wszystkie polecenia wykonuj w PowerShellu uruchomionym w katalogu głównym repozytorium.

## Co znajduje się w repozytorium

- `MinecraftHelper/` — kod aplikacji `Minecraft Helper Kopacz`,
- `MinecraftHelper.Pvp/` — kod lekkiej aplikacji `Minecraft Helper Auto Clicker`,
- `MinecraftHelper/Assets/` — ikona i zasoby programu,
- `Installer/` — definicja instalatora Inno Setup,
- `scripts/` — skrypty testowania, publikowania i budowania instalatora,
- `MinecraftHelper_AutoEQ_CobbleX_1.8.8.zip` — gotowa paczka zasobów do Auto EQ i CobbleX,
- `tests/` — testy regresji obu aplikacji,
- `tools/` — generator paczki zasobów.

## Wymagane narzędzia

Do zbudowania samej aplikacji potrzebujesz:

1. Windows 10 lub Windows 11.
2. [.NET SDK](https://dotnet.microsoft.com/download/dotnet) obsługujący rozwiązania `.slnx` (sprawdzone na SDK `10.0.402`). Obie aplikacje nadal mają docelowe środowisko `.NET 8`.
3. Edytora, np. [Visual Studio Code](https://code.visualstudio.com/) z rozszerzeniem C# Dev Kit, Visual Studio 2022 z pakietem `.NET desktop development` albo Ridera.
4. [Git](https://git-scm.com/download/win) — opcjonalnie, jeśli zamiast `git clone` pobierzesz repozytorium jako ZIP.

Dodatkowe narzędzia:

- [Inno Setup 6](https://jrsoftware.org/isdl.php) — tylko do utworzenia instalatora,
- [Node.js](https://nodejs.org/) — tylko do ponownego wygenerowania paczki zasobów.

Pobieraj narzędzia z ich oficjalnych stron, nie z przypadkowych serwisów z programami.

## 1. Pobranie źródeł

Za pomocą Git:

```powershell
git clone https://github.com/SzybkiPoPiwo/MinecraftHelper.git
cd MinecraftHelper
```

Możesz również pobrać ZIP z GitHuba, rozpakować go i otworzyć PowerShell w rozpakowanym katalogu.

## 2. Sprawdzenie środowiska i zależności

```powershell
dotnet --version
dotnet restore .\MinecraftHelper.slnx
dotnet list .\MinecraftHelper\MinecraftHelper.csproj package
dotnet list .\MinecraftHelper.Pvp\MinecraftHelper.Pvp.csproj package
dotnet list .\MinecraftHelper\MinecraftHelper.csproj package --vulnerable --include-transitive
dotnet list .\MinecraftHelper.Pvp\MinecraftHelper.Pvp.csproj package --vulnerable --include-transitive
```

Dwa ostatnie polecenia sprawdzają znane podatności pakietów NuGet i wymagają połączenia z internetem.

## 3. Kompilacja bez instalatora

```powershell
dotnet build .\MinecraftHelper.slnx -c Release
```

Gotowe aplikacje znajdą się w:

```text
MinecraftHelper.Pvp\bin\Release\net8.0-windows\MinecraftHelper.AutoClicker.exe
MinecraftHelper\bin\Release\net8.0-windows\MinecraftHelper.Miner.exe
```

Możesz je uruchomić bez instalowania:

```powershell
.\MinecraftHelper.Pvp\bin\Release\net8.0-windows\MinecraftHelper.AutoClicker.exe
.\MinecraftHelper\bin\Release\net8.0-windows\MinecraftHelper.Miner.exe
```

Możliwe jest też uruchomienie bezpośrednio przez SDK:

```powershell
dotnet run --project .\MinecraftHelper.Pvp\MinecraftHelper.Pvp.csproj -c Release
dotnet run --project .\MinecraftHelper\MinecraftHelper.csproj -c Release
```

## 4. Samodzielna publikacja wersji przenośnej

Poniższe polecenia tworzą obie aplikacje `win-x64` razem z wymaganym środowiskiem .NET 8:

```powershell
dotnet publish .\MinecraftHelper.Pvp\MinecraftHelper.Pvp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\artifacts\publish\autoclicker\win-x64

dotnet publish .\MinecraftHelper\MinecraftHelper.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\artifacts\publish\miner\win-x64
```

Uruchamiane pliki będą dostępne tutaj:

```text
artifacts\publish\autoclicker\win-x64\MinecraftHelper.AutoClicker.exe
artifacts\publish\miner\win-x64\MinecraftHelper.Miner.exe
```

Publikacja Kopacza `win-x64` pomija nieużywane biblioteki OCR `x86/`. Folder `x64/` i `tessdata/` muszą pozostać przy aplikacji — nie usuwaj ich z wersji przenośnej. Do publikacji użyj nowego katalogu albo skryptu instalatora, który usuwa pozostałości `x86/` po starszym publikowaniu.

## 5. Zbudowanie własnego instalatora

Zainstaluj Inno Setup 6, zamknij działające kopie Minecraft Helper i uruchom:

```powershell
.\scripts\build-installer.ps1 -Version 2.0.2 -Rid win-x64 -SelfContained $true -Clean
```

Jeżeli PowerShell blokuje lokalny skrypt, możesz zezwolić na jego wykonanie tylko w bieżącym oknie terminala:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Następnie ponownie uruchom skrypt budowania. Wynik znajdzie się w:

```text
artifacts\installer\MinecraftHelper-Setup-2.0.2.exe
```

Jeden instalator zawiera dwa niezależne składniki. Użytkownik może wybrać Auto Clicker, Kopacza albo oba programy.

Parametr `-Clean` usuwa wyłącznie wcześniejsze wyniki z `artifacts/publish` i `artifacts/installer`, po czym buduje je od początku.

## 6. Sprawdzenie wyniku

Oblicz sumę SHA-256 własnego pliku:

```powershell
Get-FileHash .\artifacts\installer\MinecraftHelper-Setup-2.0.2.exe -Algorithm SHA256
```

Przed udostępnieniem programu warto również:

```powershell
git status --short
git diff --check
dotnet build .\MinecraftHelper.slnx -c Release
.\scripts\test.ps1
```

Sprawdź ręcznie uruchamianie obu aplikacji, wybór procesu Minecrafta, zapis ustawień, bindy, overlay oraz zamykanie programów. W instalatorze wykonaj po jednej próbie instalacji każdego składnika osobno i obu razem. Sam fakt, że kod się kompiluje, nie potwierdza jeszcze poprawności wszystkich funkcji.

Skrypt testów zatrzyma się na pierwszym błędzie. Parametr `-NoRestore` pomija przywracanie pakietów, jeśli wszystkie projekty testowe zostały już zbudowane. Testy nie wysyłają wejścia ani nie modyfikują ustawień użytkownika; `PvpMouseTracker` na krótko rejestruje prawdziwy hook Windows. `MinerIsolation` sprawdza brak hooków i starych usług clickera w skompilowanym Kopaczu oraz migrację ustawień. `AutoWater` testuje rzeczywisty kod sekwencji z symulowanymi pikselami/wejściem: dwa wzorce, opóźnienia ikony, oczekiwanie przed zebraniem, fokus, kalibrację hotbara i zgodność ustawień; używa też tekstur wiader z dołączonego ZIP-a.

## 7. Opcjonalne odtworzenie paczki zasobów

Gotowy ZIP w repozytorium jest wystarczający do działania programu. Generator wymaga Node.js oraz rozpakowanej paczki 1.8.8. Domyślny lokalny katalog `Old_Default_1.8.8` nie jest dołączony do GitHuba.

Możesz użyć dołączonej paczki jako źródła i ponownie wygenerować znaczniki:

```powershell
Expand-Archive .\MinecraftHelper_AutoEQ_CobbleX_1.8.8.zip -DestinationPath .\.build-check\texture-source
node .\tools\build-discard-texture-pack.js .\.build-check\texture-source
```

Katalog źródłowy powinien bezpośrednio zawierać `pack.mcmeta` i `assets`. Użyj osobnego, nowego katalogu do rozpakowania. Generator zapisuje wynik do `MinecraftHelper_AutoEQ_CobbleX_1.8.8/`, nadpisując pliki o tych samych nazwach. Nie aktualizuje automatycznie ZIP-a. Pomoc: `node .\tools\build-discard-texture-pack.js --help`.

## Korzystanie z asystenta AI

W Visual Studio Code możesz poprosić asystenta AI o wykonanie instrukcji krok po kroku, na przykład:

> Przeczytaj `docs/BUILDING.md`, sprawdź wymagane narzędzia i pomóż mi zbudować aplikację bez uruchamiania instalatora. Wyjaśnij każde polecenie przed jego wykonaniem.

Nie zatwierdzaj automatycznie poleceń usuwających pliki ani wyłączających zabezpieczenia systemu. Asystent AI może pomóc w analizie, ale nie zastępuje samodzielnego przeglądu kodu lub audytu bezpieczeństwa.
