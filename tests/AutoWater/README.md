# AutoWater — testy regresji

Uruchom na Windows z katalogu repozytorium:

```powershell
dotnet run --project tests/AutoWater/AutoWater.csproj -c Release
```

Projekt linkuje prawdziwy `MainWindow.AutoWater.cs` i modele ustawień. Zastępuje
tylko otoczenie okna, przechwytywanie pikseli i natywne wejście. Nie otwiera GUI,
nie przechwytuje ekranu, nie wysyła klawiszy/kliknięć i nie odczytuje AppData.

Sprawdza między innymi:

- opóźnione odświeżanie ikony, różne odstępy PPM i kroki zegara;
- świeży odczyt przed PPM oraz zatrzymanie prób po pierwszym rozpoznaniu pustego
  wiadra, potwierdzenie bez klikania i pełny czas oczekiwania przed zebraniem;
- osobne wzorce pustego wiadra i wody, odrzucanie obcych i niepewnych ikon;
- wybór/powrót na slot, błędy wejścia, timeout i limit ponownych prób zebrania;
- przerwanie w każdym etapie po utracie fokusu albo otwarciu GUI;
- kalibrację hotbara nad czatem, odrzucenie punktu z EQ i drugą próbkę z tego
  samego punktu, bez konieczności ponownego celowania kursorem;
- czekanie na fokus i zamknięcie GUI, pełne dwie sekundy testu i ignorowanie Ctrl
  trzymanego już przy przejściu do Minecrafta;
- zapis/odczyt obu wzorców oraz bezpieczne odczytanie starszych konfiguracji.

To testy logiki, nie skuteczności water clutcha. Ekranowe rozpoznawanie nie daje
gwarancji przy dowolnym opóźnieniu klienta/serwera. Jeśli ikona zmienia się wolniej
niż ustawiony odstęp prób, dodatkowe kliknięcie przed jej rozpoznaniem nadal jest
możliwe. Żaden lokalny test nie potwierdza, że serwer zaakceptował postawienie wody.

## Próba ręczna

1. Wybierz Minecrafta i slot wody. Użyj standardowego hotbara i klawiszy `1–9`.
2. Włóż puste wiadro w wybrany slot, kliknij „Kalibruj puste”, wróć do gry,
   otwórz czat (nie EQ), najedź na odróżniający fragment ikony nad polem czatu
   i naciśnij lewy Ctrl. Próbka 9×9 px musi być w całości widoczna.
3. Kliknij „Kalibruj wodę”. W tym samym slocie umieść wiadro z wodą, wróć do
   gry, otwórz czat i użyj Ctrl. Drugi wzorzec jest odczytywany ze stałego punktu.
4. Testuj osobno pusty slot/przedmiot, puste wiadro i wodę przy zamkniętym GUI.
   Tylko woda powinna dawać „WIADRO Z WODĄ”, obcy przedmiot — „NIEZNANY”.
5. W bezpiecznym świecie/kreatywnym sprawdź bind, czas zebrania, anulowanie,
   otwarcie EQ i Alt+Tab. Dopiero później wykonuj próby na docelowym serwerze.

Celuj w blok, nie przewijaj ręcznie slotów podczas sekwencji. Po zmianie GUI Scale,
rozmiaru okna, paczki lub slotu wody skalibruj oba wzorce ponownie. Przy małej
skali GUI może brakować miejsca na próbkę nad czatem — użyj Medium/Large.
