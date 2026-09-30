# Test przechwytywania myszy na żądanie

Uruchom na Windows z katalogu repozytorium:

```powershell
dotnet run --project tests/MouseHookLifecycle/MouseHookLifecycle.csproj -c Release
```

Test sprawdza brak globalnego przechwytywania po starcie i w spoczynku,
rejestrację przed pracą makra, zachowanie rejestracji podczas aktywnych makr,
ignorowanie syntetycznych naciśnięć/puszczeń LPM i PPM, wielokrotne
włączanie/wyłączanie oraz sprzątanie wątku przy zamykaniu.

Test na krótko rejestruje prawdziwy hook Windows, ale nie wysyła ruchów,
kliknięć ani klawiszy. Nie otwiera GUI i nie czyta ani nie zapisuje ustawień
użytkownika. Tworzy niezinicjalizowany obiekt `MainWindow` wyłącznie dla
izolowanych metod obsługi myszy; diagnostyka plikowa jest wyłączona.

Test ręczny wersji diagnostycznej:

1. Zamknij dotychczasowy Helper przez **Zakończ aplikację** w zasobniku.
2. Uruchom nowy build i porównaj płynność Minecrafta przy wyłączonych makrach,
   zarówno z widocznym, jak i zminimalizowanym GUI.
3. Sprawdź AUTO LPM/PPM: zwykły bind, trzymanie bindu oraz bind + przycisk myszy.
   Sprawdź uruchomienie, puszczenie przycisku i ponowne uruchomienie.
4. Po zatrzymaniu makr sprawdź ponownie płynność gry.

W `macro-diagnostics.log` wpis `MOUSE_HOOK enabled=0` oznacza odłączenie
przechwytywania, a `MOUSE_HOOK enabled=1` jego ponowną rejestrację. Te testy
nie zastępują pomiaru płynności gry na komputerze użytkownika.
