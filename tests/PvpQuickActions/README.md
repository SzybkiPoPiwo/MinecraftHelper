# Ciągłość AUTO LPM podczas akcji PVP

```powershell
dotnet run --project tests/PvpQuickActions/PvpQuickActions.csproj -c Release
```

Test łączy produkcyjną maszynę stanów akcji PVP i harmonogram clickera z
izolowanym hostem kontrolek oraz licznikiem zastępującym wejście Windows.
Nie otwiera okien, nie wysyła klawiszy/kliknięć ani nie czyta ustawień użytkownika.
Etapy wykonuje według sztucznego zegara; natywne timery są odłączone.

Sprawdza wszystkie cztery akcje w każdym trybie LPM: aktywny kanał, niezmienioną
generację i zaplanowany termin kliknięcia od wyboru przedmiotu po powrót na miecz.
Sprawdza pauzę AUTO PPM, pojedyncze kliknięcia standardowych przedmiotów,
ustawiony czas trzymania PPM wędki, błędy wejścia, blokadę kursorem GUI i STOP.

Pomiar CPS oraz odbiór kliknięć przez klienta Minecrafta wymagają osobnego testu.
