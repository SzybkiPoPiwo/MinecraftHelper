> 🚧 **Apka w trakcie budowy** — aktualnie gotowy jest głównie **GUI + zapisywanie ustawień** (układ, zakładki, statusy, historia tytułów, import/export). Logika makr (realne klikanie/kopanie) będzie dopinana w kolejnych commitach.

# MinecraftHelper

MinecraftHelper to prosta aplikacja‑**makro z GUI** do Minecrafta na Windows. Ustawiasz bindy, CPS i opcje kopania w oknie programu — bez ręcznego grzebania w configach.

<img width="3168" height="1344" alt="Gemini_Generated_Image_5j6nz75j6nz75j6n" src="https://github.com/user-attachments/assets/1ee1fa6b-54a1-4432-8733-1936e8c83b55" />

---

## O projekcie

Ten projekt miał być na początku po prostu **podstawowym macrem pod PVP**, zrobionym prywatnie dla siebie. Nie planowałem wrzucać go nigdzie dalej ani rozwijać „pod publikę”.

Z czasem jednak kilka osób zaczęło pytać o program, podrzucać pomysły i prosić o kolejne funkcje — i tak ten projekt zaczął się rozkręcać. W końcu wylądował tutaj na GitHubie jako otwarty pomysł do dalszego rozwoju.

Cel jest prosty: **dać graczom to, czego chcą** — program od gracza dla graczy.  
Kod jest **open‑source**, czysty i przejrzysty: bez ratów, bez wirusów, bez ukrytych „niespodzianek”.

---

## Co już działa

- Pełny **szkielet GUI** (PVP — w tym „Jabłka z liści” / Kopacz / Ustawienia).
- **Auto‑zapis** ustawień (sygnalizacja “Settings Saved” na górze).
- **Import/Export** ustawień do pliku JSON.
- **Historia tytułów okna gry** (max 5) + szybkie podstawianie z listy.
- Ciemny, spójny motyw kontrolek (dark style).

> Uwaga: tryby makr są spięte w GUI, ale docelowa logika wykonywania akcji będzie dopinana etapami.

---

## Górny panel statusu

U góry aplikacji widać:
- Pasek statusu (np. informacja, że ustawienia są zapisane).
- Aktualnie ustawiony tytuł okna gry (po tym apka rozpoznaje, w które okno ma działać).
- Kafelki trybów, które pokazują czy dana opcja jest włączona/wyłączona i jaki ma status (np. CPS/wyłączony).
- Pasek „Minecraft Focus / Settings Saved” — szybka informacja czy okno gry ma fokus oraz czy ustawienia są zapisane.

---

## Zakładki

### PVP

- Tryb **LPM + PPM HOLD** działa na zasadzie jak klasyczne macro uruchamiane bindem — naciskasz przypisany klawisz i tryb się aktywuje.  
  Różnica jest taka, że samo „klikanie” dzieje się dopiero wtedy, gdy trzymasz przycisk myszy:
  - Przytrzymujesz **LPM** → macro zaczyna bić (auto‑klika) według ustawionego CPS.
  - Puszczasz **LPM** → macro przestaje.
  - Przytrzymujesz **PPM** → macro klika PPM według ustawionego CPS.
  - Puszczasz **PPM** → macro przestaje.

- Tryb **AUTO LPM**:
  - bind + zakres CPS.

- Tryb **AUTO PPM**:
  - bind + zakres CPS.

- Tryb **Jabłka z liści** (w tej samej zakładce PVP):
  - bind uruchomienia,
  - osobna konfiguracja w GUI (checkbox + pole klawisza + przycisk zapisu).

<img width="1386" height="893" alt="7xfV42v9qF" src="https://github.com/user-attachments/assets/f21f6645-2863-4fdf-b472-7377b91b68f5" />

### Kopacz

- **Kopacz 5/3/3**
  - kopie **tylko do przodu**,
  - bind startu,
  - lista komend wykonywanych z opóźnieniami (sekundy).

- **Kopacz 6/3/3**
  - wybór kierunku: „na wprost” lub „do góry”,
  - ustawienia szerokości/długości (zależnie od trybu),
  - lista komend wykonywanych z opóźnieniami.

- Panel „Przelicznik” (minuty → sekundy) ułatwia ustawianie timingów.

<img width="1386" height="893" alt="oaHfhc26Yt" src="https://github.com/user-attachments/assets/d9da49b1-8888-4566-ad84-c2be2cc8ef8f" />

### Ustawienia

- Tytuł okna gry (Minecraft) — po tym apka sprawdza, czy ma działać w aktualnym oknie.
- Historia ostatnich tytułów okna gry (max 5) — szybkie przełączanie między różnymi instancjami Minecrafta (np. inne okno / inny launcher).
- Import/Export ustawień do JSON.

<img width="1386" height="893" alt="aG7Q73Ki11" src="https://github.com/user-attachments/assets/02872f66-91d9-456f-b77b-09165e8950ef" />

---

## Szybki start

1. Otwórz zakładkę **Ustawienia** i wpisz tytuł okna gry (np. `Minecraft`).
2. Skonfiguruj opcje w zakładkach **PVP** (w tym „Jabłka z liści”) i/lub **Kopacz**.
3. Sprawdź na górze, czy `Minecraft Focus` pokazuje, że okno gry jest aktywne.
4. Zapisz konfigurację (albo zrób Export do pliku).

---

## Do zrobienia (TODO)

| # | Zadanie | Status | Notatki |
|---:|---|---|---|
| 1 | Macro (core) | ⏳ | Start/stop, CPS, tick/timing, bezpieczeństwo. |
| 2 | Warunek działania | ⏳ | Działać tylko gdy `Minecraft Focus = Tak`. |
| 3 | PVP: logika klikania | ⏳ | Realne kliki LPM/PPM + zakres CPS. |
| 4 | Kopacz: logika | ⏳ | Sekwencje ruchów/komend zgodnie z ustawieniami. |
| 5 | Jabłka z liści: logika | ⏳ | Działanie trybu zgodnie z ustawieniami. |
| 6 | Lista aktywnych trybów | ⏳ | W GUI pokazać aktywne makra i ich parametry. |
| 7 | Wyrzucanie itemów podczas kopania | ⏳ | Opcjonalne, konfigurowalne. |
| 8 | Auto join + wznowienie kopania | ⏳ | Wykrycie rozłączenia i automatyczny powrót. |
| 9 | Zakładka „Bindy” | ⏳ | Jedno miejsce do zarządzania wszystkimi bindami. |
| 10 | Logi z serwera podczas kopania | ⏳ | Podgląd/zbieranie logów na żywo. |
| 11 | itd. | ⏳ | Kolejne pomysły z czasem. |

---

## Masz pomysł?

Jeśli masz pomysł co można jeszcze dodać albo jaką logikę zastosować, napisz do mnie na Discordzie: **twojstaryricardo**.  
Chętnie zbieram propozycje od graczy i wrzucam je do TODO / kolejnych aktualizacji.

---

## Pobranie (klonowanie)

>git clone https://github.com/SzybkiPoPiwo/MinecraftHelper.git
