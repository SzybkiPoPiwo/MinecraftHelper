# Samodzielne sprawdzenie i budowanie Minecraft Helper

Ten poradnik pozwala zbudować aplikację bez uruchamiania gotowego instalatora. Wszystkie polecenia wykonuj w PowerShellu uruchomionym w katalogu głównym repozytorium.

## Co znajduje się w repozytorium

- `MinecraftHelper/` — kod aplikacji WPF w C#,
- `MinecraftHelper/Assets/` — ikona i zasoby programu,
- `Installer/` — definicja instalatora Inno Setup,
- `scripts/` — skrypt publikowania i budowania instalatora,
- `MinecraftHelper_AutoEQ_CobbleX_1.8.8/` — paczka zasobów do Auto EQ i CobbleX,
- `tools/` — generator paczki zasobów.

## Wymagane narzędzia

Do zbudowania samej aplikacji potrzebujesz:

1. Windows 10 lub Windows 11.
2. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
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
dotnet restore .\MinecraftHelper\MinecraftHelper.csproj
dotnet list .\MinecraftHelper\MinecraftHelper.csproj package
dotnet list .\MinecraftHelper\MinecraftHelper.csproj package --vulnerable --include-transitive
```

Ostatnie polecenie sprawdza znane podatności pakietów NuGet i wymaga połączenia z internetem.

## 3. Kompilacja bez instalatora

```powershell
dotnet build .\MinecraftHelper\MinecraftHelper.csproj -c Release
```

Gotowa aplikacja znajdzie się w:

```text
MinecraftHelper\bin\Release\net8.0-windows\MinecraftHelper.exe
```

Możesz ją uruchomić bez instalowania:

```powershell
.\MinecraftHelper\bin\Release\net8.0-windows\MinecraftHelper.exe
```

Możliwe jest też uruchomienie bezpośrednio przez SDK:

```powershell
dotnet run --project .\MinecraftHelper\MinecraftHelper.csproj
```

## 4. Samodzielna publikacja wersji przenośnej

Poniższe polecenie tworzy aplikację `win-x64` razem z wymaganym środowiskiem .NET 8:

```powershell
dotnet publish .\MinecraftHelper\MinecraftHelper.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\artifacts\publish\win-x64
```

Uruchamiany plik będzie dostępny tutaj:

```text
artifacts\publish\win-x64\MinecraftHelper.exe
```

## 5. Zbudowanie własnego instalatora

Zainstaluj Inno Setup 6, zamknij działające kopie Minecraft Helper i uruchom:

```powershell
.\scripts\build-installer.ps1 -Version 1.1.0 -Rid win-x64 -SelfContained $true -Clean
```

Jeżeli PowerShell blokuje lokalny skrypt, możesz zezwolić na jego wykonanie tylko w bieżącym oknie terminala:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Następnie ponownie uruchom skrypt budowania. Wynik znajdzie się w:

```text
artifacts\installer\MinecraftHelper-Setup-1.1.0.exe
```

Parametr `-Clean` usuwa wyłącznie wcześniejsze wyniki z `artifacts/publish/<RID>` i `artifacts/installer`, po czym buduje je od początku.

## 6. Sprawdzenie wyniku

Oblicz sumę SHA-256 własnego pliku:

```powershell
Get-FileHash .\artifacts\installer\MinecraftHelper-Setup-1.1.0.exe -Algorithm SHA256
```

Przed udostępnieniem programu warto również:

```powershell
git status --short
git diff --check
dotnet build .\MinecraftHelper\MinecraftHelper.csproj -c Release
```

Sprawdź ręcznie uruchamianie aplikacji, zapis ustawień, bindy oraz zamykanie programu. Sam fakt, że kod się kompiluje, nie potwierdza jeszcze poprawności wszystkich funkcji.

## Korzystanie z asystenta AI

W Visual Studio Code możesz poprosić asystenta AI o wykonanie instrukcji krok po kroku, na przykład:

> Przeczytaj `docs/BUILDING.md`, sprawdź wymagane narzędzia i pomóż mi zbudować aplikację bez uruchamiania instalatora. Wyjaśnij każde polecenie przed jego wykonaniem.

Nie zatwierdzaj automatycznie poleceń usuwających pliki ani wyłączających zabezpieczenia systemu. Asystent AI może pomóc w analizie, ale nie zastępuje samodzielnego przeglądu kodu lub audytu bezpieczeństwa.
