# Minecraft Helper Auto Clicker

Lekka, niezależna aplikacja zawierająca wyłącznie funkcje używane poza kopaniem:

- AUTO LPM i AUTO PPM: przełączanie bindem, działanie podczas trzymania, tryb kombinacji oraz trwałe uzbrojenie osobnym bindem, w którym clicker pracuje tylko podczas fizycznego trzymania LPM albo PPM. Wspólny bind działa dla LPM w trybie trzymania/kombinacji i PPM w trybie kombinacji; trwałe uzbrojenie LPM i PPM wymaga różnych klawiszy,
- szybkie akcje PVP: osobne bindy Auto bloku, śnieżki, perły i wędki, własne sloty hotbara oraz regulowany czas trzymania PPM dla wędki,
- ścinanie jabłek: osobny bind, slot nożyc i liści, losowane zakresy czasu przełączania, cięcia i kładzenia, PPM 40 CPS oraz automatyczne `/repair` co ustawioną liczbę cykli,
- BINDY z własnymi nazwami, klawiszami i komendami,
- Auto zbroja z ręczną kalibracją pól,
- AutoWater z kalibracją pustego wiadra i wiadra z wodą na hotbarze przy otwartym czacie (nie EQ),
- opcjonalny, mały overlay aktywnych funkcji z jednolitym rozmiarem wszystkich komunikatów.

Projekt nie uruchamia Kopacza, Auto EQ kopacza, OCR, Auto Reconnect ani nasłuchu dźwięku. Kliknięcia wykonuje lekki harmonogram uruchamiany tylko wtedy, gdy makro faktycznie pracuje. W trybach zależnych od fizycznego LPM/PPM minimalny tracker działa tylko podczas trzymania przycisku, ignoruje wejście aplikacji i nie przetwarza ruchów myszy. Ustawienia są zapisywane niezależnie w:

`%APPDATA%\Minecraft Helper\AutoClicker\settings.json`

Szybka akcja działa tylko podczas aktywnego AUTO LPM; samo AUTO PPM jej nie uruchamia. Jedno naciśnięcie odpowiedniego binda wybiera skonfigurowany slot, wykonuje jedno użycie PPM i przywraca ustawiony slot miecza. AUTO LPM oraz DAB działają podczas całej sekwencji bez przerwy, natomiast AUTO PPM jest chwilowo pauzowane, aby nie dublowało użycia przedmiotu. Auto blok wybiera ustawiony slot bloku i stawia jeden blok dokładnie tak jak akcje śnieżki i perły, a dla wędki można ustawić czas trzymania PPM od 20 do 1000 ms. Utrata fokusu, otwarte GUI lub wyłączenie AUTO LPM przerywają sekwencję i zwalniają PPM.

Auto blok, śnieżka i perła używają pojedynczego pełnego kliknięcia PPM, bez przytrzymywania przycisku między etapami. Akcja aktualizuje wyłącznie lewy kanał harmonogramu i nie zmienia jego cyklu, jeśli ustawienia LPM pozostają takie same. Włączenie, wykonanie i zakończenie akcji nie resetują CPS ani terminu następnego kliknięcia. W trybach trzymania puszczenie wymaganego bindu lub LPM nadal zatrzymuje klikanie.

Zakładka `Jabłka` uruchamia niezależną pętlę jednym bindem: wybiera nożyce, przytrzymuje LPM, przełącza się na liście, wysyła PPM z szybkością 40 CPS i wraca na nożyce. Każdy z trzech etapów ma własny zakres `od–do` w milisekundach. Domyślnie po 40 pełnych cyklach, już na slocie nożyc, program wpisuje `/repair`. Ten sam bind zatrzymuje automat natychmiast; utrata fokusu lub nieoczekiwane otwarcie GUI także przerywa działanie i zwalnia trzymany LPM.

## AutoWater — nowa kalibracja

Stary pojedynczy wzorzec nie wystarcza. Wymagane są dwa wzorce tego samego fragmentu ikony w tym samym slocie hotbara:

1. Włącz moduł, ustaw bind i wybierz slot wody oraz powrotu.
2. Użyj `Kalibruj puste`: umieść puste wiadro w wybranym slocie **hotbara**, otwórz **czat, nie EQ**, wskaż charakterystyczny fragment ikony nad polem czatu i naciśnij lewy `Ctrl`.
3. Użyj `Kalibruj wodę`: umieść wiadro z wodą w tym samym slocie, wróć do gry, otwórz czat i naciśnij lewy `Ctrl`. Druga próbka jest pobierana z zapisanego punktu — nie trzeba ponownie celować kursorem.
4. Przez `Testuj` sprawdź oba wiadra oraz inny przedmiot. Test odlicza pełne dwie sekundy dopiero w aktywnym Minecrafcie przy zamkniętym GUI; utrata fokusu albo otwarcie GUI resetują odliczanie.

Przed PPM moduł odczytuje świeżą ikonę; pierwsze rozpoznanie pustego wiadra wstrzymuje klikanie na czas potwierdzania i ustawionego oczekiwania. `Zebranie po` liczy się od tego pierwszego odczytu. Zebranie wymaga potwierdzenia wzorca wody, nie tylko zniknięcia pustej ikony. Niepewne odczyty nie uprawniają do PPM. Pozostałe bindy nie ingerują w trwającą sekwencję.

Kalibracja, test i start AutoWater zatrzymują clickery oraz DAB. Po zakończeniu włącz potrzebne makro ponownie. Ponowne naciśnięcie bindu anuluje aktywną sekwencję; kalibrację lub test anulujesz przez wyłączenie modułu. `Resetuj` usuwa oba wzorce.

To rozpoznawanie ekranowe, nie gwarancja zaakceptowania akcji przez serwer. Najpierw wykonaj [próbę ręczną AutoWater](../tests/AutoWater/README.md) w bezpiecznym świecie. Celuj w blok i nie zmieniaj ręcznie slotu; po zmianie slotu wody, skali GUI, paczki lub rozmiaru okna powtórz kalibrację.

Zakresy ustawień, dobór czasów i ograniczenia opisano w [pełnej instrukcji AutoWater](../README.md#autowater). Poprawki są częścią wersji `2.0.2`, dostępnej we wspólnym instalatorze z wyborem Auto Clickera i Kopacza. Po aktualizacji bindy i czasy pozostają zachowane; oba wzorce wiader trzeba skalibrować ponownie.

## Ustawienia i aktualizacje

Jeżeli istnieją ustawienia wcześniejszego prototypu z brancha `projekt-2-apki`, program wczyta je ze starej lokalizacji i przy najbliższym zapisie zapisze konfigurację także w nowym pliku.

Przy pierwszym uruchomieniu oraz po zmianie wersji Auto Clicker pokazuje krótkie informacje o konfiguracji i listę nowości. Przy każdym starcie sprawdza też najnowsze publiczne wydanie na GitHubie; znalezioną aktualizację tylko zgłasza i podaje oficjalny link, bez automatycznego pobierania lub instalowania pliku.

## Uruchomienie ze źródeł

Uruchom PowerShell w głównym katalogu repozytorium, obok `MinecraftHelper.slnx`. Wymagania SDK: [instrukcja budowania](../docs/BUILDING.md#wymagane-narzędzia).

```powershell
dotnet run --project ".\MinecraftHelper.Pvp\MinecraftHelper.Pvp.csproj" -c Release
```

Po uruchomieniu wybierz właściwe okno gry z listy i kliknij `Zapisz`. Funkcje wejścia działają tylko wtedy, gdy zapisane okno Minecrafta ma fokus.

Program ma interfejs okienkowy, nie konsolowy. Jeśli już działa i nie widzisz okna, sprawdź ikonę w zasobniku obok zegara. Przycisk `X` i minimalizacja ukrywają okno; całkowite zamknięcie jest dostępne w menu ikony zasobnika.


