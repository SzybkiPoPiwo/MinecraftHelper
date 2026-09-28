# Eksperymenty techniczne

## Sterowanie BlazingPackiem na drugim pulpicie Windows

Status: wycofane z aplikacji po teście zgodności (27.09.2026).

### Cel

Sprawdzenie, czy Minecraft 1.8.8 uruchomiony przez BlazingPack może odbierać
polecenia z Minecraft Helpera, gdy znajduje się na innym wirtualnym pulpicie
Windows i nie jest aktywnym oknem.

### Sprawdzony wariant

- Polecenia klawiatury i myszy były wysyłane przez `PostMessage` bezpośrednio
  do uchwytu okna procesu `java.exe`.
- Testy obejmowały LPM, klawisze A, S, D, otwarcie ekwipunku klawiszem E oraz
  wpisanie komendy na czacie.
- Samo wysłanie `WM_KEYDOWN`/`WM_KEYUP` nie powodowało reakcji gry, dopóki
  użytkownik nie kliknął okna Minecrafta.
- Po wysłaniu `WM_SETFOCUS` ruch i otwieranie ekwipunku zaczęły działać również
  na drugim pulpicie.

### Wynik

BlazingPack/LWJGL po otrzymaniu fokusu przejmuje mysz, centruje lub ogranicza
kursor i może zakłócać pracę w aktywnym programie. Próby zwalniania `ClipCursor`
i przechwycenia myszy oraz przywracania pozycji kursora ograniczały objaw, ale
nie zapewniły bezpiecznej, niewidocznej pracy w tle.

Wirtualne pulpity Windows nie są osobnymi sesjami wejścia. Współdzielą aktywny
fokus, klawiaturę i mysz, dlatego ten wariant nie powinien zostać włączony do
produkcyjnego Kopacza.

### Zachowane wnioski na przyszłość

- Wiadomości należy kierować do aktualnego głównego okna właściwego procesu
  `java.exe`, a nie polegać wyłącznie na zapisanym wcześniej PID.
- Helper i gra muszą działać z tym samym poziomem uprawnień.
- `WM_SETFOCUS` umożliwił reakcję na przyciski na drugim pulpicie, lecz wywołał
  globalne skutki uboczne dla kursora.
- Pełna izolacja wymaga osobnej sesji wejścia, np. maszyny wirtualnej, drugiego
  komputera albo rozwiązania oficjalnie obsługiwanego wewnątrz klienta.

## Auto reconnect i powrót do kopania

Status: aktywny prototyp w zakładce `Experimental` (27.09.2026).

Mechanizm działa wyłącznie przy widocznym, aktywnym oknie Minecrafta i nie
otwiera już samodzielnie EQ w osobnym interwale. Kontrola została podpięta pod
zaplanowany cykl Auto EQ. Jeżeli podczas tego cyklu gra trzykrotnie nie otworzy
ekwipunku albo nie uda się rozpoznać jego ekranu, uruchamiana jest maszyna
stanów reconnectu: rozpoznanie ekranu, powrót do listy serwerów, Direct Connect,
komenda home i końcowe potwierdzenie EQ przed wznowieniem kopania.

Auto EQ sprawdza również osobny znacznik diamentowego kilofa w głównym EQ i na
hotbarze. Jego brak nie uruchamia Direct Connect. To osobny flow bezpieczeństwa:
program zamyka EQ, wykonuje home zgodnie z wybranym profilem (z GUI albo bez)
i kończy tylko ten kanał Kopacza, który rozpoczął skan.

Konfiguracja korzysta z osobnych profili serwerów. Profil zapisuje nazwę,
adres Direct Connect, pełną komendę home, czasy oraz limity prób. Jeśli home
nie otwiera GUI, automat wysyła wyłącznie zapisaną komendę, np.
`/home kopalnia`. Jeśli serwer pokazuje GUI, profil przechowuje czas
oczekiwania, liczbę rzędów i kolumn oraz wybrane pole. Edytor pozwala dodawać
kolumny z prawej strony i rzędy od dołu, więc układ nie jest ograniczony do
3 × 9. Po zakończeniu ustawionego czasu teleportacji automat czeka dodatkową
sekundę przed otwarciem kontrolnego EQ.

Po wykryciu ekranu rozłączenia automat odczekuje 7 sekund przed użyciem
`Back to Server List` albo `Reconnect`. BlazingPack potrafi blokować przycisk
powrotu przez około 5 sekund i ogranicza kolejne połączenie przez około
6 sekund; dodatkowa sekunda zapasu zapobiega klikaniu na granicy timeoutu.

Rozpoznawane etykiety `[MH]` są identyczne w `en_US.lang` i `pl_PL.lang`.
Oryginalna przyczyna rozłączenia pozostaje widoczna. Wykrycie bana albo
nieznanego ekranu zatrzymuje procedurę bez dalszego klikania.

Zalecana kolejność testów:

1. `Rozpoznaj ekran` na każdym ekranie rozłączenia, liście serwerów i Direct
   Connect.
2. `Sprawdź EQ` w grze z aktywną paczką, diamentowym kilofem i `GUI Scale: Large`.
3. `Test /home` z wybranym profilem i właściwym polem jego układu GUI.
4. `Pełny reconnect` dopiero po zaliczeniu wcześniejszych etapów.

