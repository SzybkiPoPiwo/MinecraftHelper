# Izolacja Kopacza od modułów clickera

Uruchom na Windows z katalogu repozytorium:

```powershell
dotnet run --project tests/MinerIsolation/MinerIsolation.csproj -c Release
```

Test zastępuje dawny `MouseHookLifecycle`: Kopacz nie powinien już posiadać
globalnego hooka myszy, więc zamiast testowania jego rejestracji sprawdzamy brak
możliwości rejestracji hooka w całym skompilowanym module. Pilnujemy również braku
starego harmonogramu clickera, diagnostyki i wątku myszy, zachowania szybkiej
obsługi aktywnych etapów kopania/EQ/reconnectu oraz zgodności starszych ustawień.

Nie otwiera GUI ani Minecrafta, nie wysyła wejścia, nie zakłada hooków i nie
odczytuje ani nie zapisuje ustawień użytkownika. Osobny `PvpMouseTracker` testuje
tracker fizycznych przycisków nadal potrzebny clickerowi w trybach kombinacji.
