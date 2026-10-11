# Minecraft Helper Auto Clicker

Lekka, niezależna aplikacja zawierająca wyłącznie funkcje używane poza kopaniem:

- AUTO LPM i AUTO PPM: przełączanie bindem, działanie podczas trzymania oraz tryb kombinacji z fizycznym przyciskiem myszy. Wspólny bind działa dla LPM w trybie trzymania/kombinacji i PPM w trybie kombinacji; trzymanie PPM wymaga osobnego klawisza,
- szybkie akcje PVP: osobne bindy Auto bloku, śnieżki, perły i wędki, własne sloty hotbara oraz regulowany czas trzymania PPM dla wędki,
- BINDY z własnymi nazwami, klawiszami i komendami,
- Auto zbroja z ręczną kalibracją pól,
- AutoWater z ręczną kalibracją ikony wiadra,
- opcjonalny, mały overlay aktywnych funkcji z jednolitym rozmiarem wszystkich komunikatów.

Projekt nie uruchamia Kopacza, Auto EQ kopacza, OCR, Auto Reconnect ani nasłuchu dźwięku. Kliknięcia wykonuje lekki harmonogram uruchamiany tylko wtedy, gdy makro faktycznie pracuje. W trybie kombinacji uruchamiany jest dodatkowo minimalny tracker fizycznego LPM/PPM, który ignoruje wejście aplikacji i nie przetwarza ruchów myszy. Ustawienia są zapisywane niezależnie w:

`%APPDATA%\Minecraft Helper\AutoClicker\settings.json`

Szybka akcja działa tylko podczas aktywnego AUTO LPM; samo AUTO PPM jej nie uruchamia. Jedno naciśnięcie odpowiedniego binda wybiera skonfigurowany slot, wykonuje jedno użycie PPM i przywraca ustawiony slot miecza. AUTO LPM oraz DAB działają podczas całej sekwencji bez przerwy, natomiast AUTO PPM jest chwilowo pauzowane, aby nie dublowało użycia przedmiotu. Auto blok wybiera ustawiony slot bloku i stawia jeden blok dokładnie tak jak akcje śnieżki i perły, a dla wędki można ustawić czas trzymania PPM od 20 do 1000 ms. Utrata fokusu, otwarte GUI lub wyłączenie AUTO LPM przerywają sekwencję i zwalniają PPM.

Jeżeli istnieją ustawienia wcześniejszego prototypu z brancha `projekt-2-apki`, program wczyta je ze starej lokalizacji i przy najbliższym zapisie zapisze konfigurację także w nowym pliku.

Przy pierwszym uruchomieniu oraz po zmianie wersji Auto Clicker pokazuje krótkie informacje o konfiguracji i listę nowości. Przy każdym starcie sprawdza też najnowsze publiczne wydanie na GitHubie; znalezioną aktualizację tylko zgłasza i podaje oficjalny link, bez automatycznego pobierania lub instalowania pliku.

## Uruchomienie wersji testowej

```powershell
dotnet run --project .\MinecraftHelper.Pvp\MinecraftHelper.Pvp.csproj -c Release
```

Po uruchomieniu wybierz właściwe okno gry z listy i kliknij `Zapisz`. Funkcje wejścia działają tylko wtedy, gdy zapisane okno Minecrafta ma fokus.


