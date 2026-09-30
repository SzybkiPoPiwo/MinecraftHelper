# Zatrzymywanie Kopacza podczas akcji

Uruchom na Windows z katalogu repozytorium:

```powershell
dotnet run --project tests/MiningStop/MiningStop.csproj -c Release
dotnet run --project tests/MouseHookLifecycle/MouseHookLifecycle.csproj -c Release
```

Testy sprawdzają zatrzymanie stanu Kopacza 5/3/3 i 6/3/3 dla każdego etapu
Auto EQ i komend, anulowanie pozostałych celów, CX, jedzenia i wznowienia,
odrzucanie starych wyników skanowania po STOP/restart oraz pierwszeństwo
obsługi STOP przed blokadą komend i reconnectu. Sprawdzają też pojedyncze
naciśnięcie bindu i ignorowanie klawiszy trzymanych syntetycznie przez automat.
Licznik komend obu Kopaczy jest sprawdzany pod kątem zatrzymania na cały czas
Auto EQ/CX/jedzenia i wznowienia z dokładnie zachowaną pozostałą wartością.
Test obejmuje również jasne statusy sesji EQ i poprawną polską odmianę liczby
pozostałych stosów oraz wykonanych przebiegów.

Testy nie uruchamiają GUI/Minecrafta, nie zapisują ustawień ani logów użytkownika
i nie wysyłają klawiszy do pulpitu. Test resetowania stanu zaczyna z puszczonymi
klawiszami; rzeczywiste puszczenie klawiszy w grze wymaga próby ręcznej.

## Próba ręczna

Użyj bindu, który nie koliduje ze sterowaniem gry (np. Insert/Delete), i powtórz
dla obu Kopaczy. Podczas każdej próby naciśnij raz ten sam bind, którym włączasz
kopanie:

- podczas powrotu A/S do punktu startowego przed EQ;
- podczas otwierania i skanowania EQ oraz wyrzucania stosów;
- podczas wpisywania CX/komendy kopania i jedzenia po EQ;
- podczas kontroli EQ, oczekiwania i analizy ekranu reconnectu.

Po STOP nie mogą być wykonywane pozostałe akcje ani wznowienie kopania.
Klawisze trzymane przez program (ruch, Shift, Ctrl, wyrzucanie, LPM/PPM) mają
zostać puszczone. Przytrzymanie bindu po STOP nie może ponownie uruchomić Kopacza.
Sprawdź też ponowny start po puszczeniu i ponownym naciśnięciu bindu.

STOP nie cofa przedmiotów już wyrzuconych ani komend już wysłanych. Nie wysyła
dodatkowego Escape/E: otwarte EQ lub chat można zamknąć ręcznie. Wynik trwającego
skanu może zostać obliczony w tle, ale po anulowaniu jest ignorowany.
