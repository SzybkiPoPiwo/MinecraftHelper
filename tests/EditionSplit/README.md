# EditionSplit

Lekki test regresji sprawdzający rozdzielenie wydania na dwa programy. Nie uruchamia GUI, nie wysyła wejścia i nie odczytuje ustawień użytkownika.

```powershell
dotnet run --project .\tests\EditionSplit\EditionSplit.csproj -c Release
```

Test pilnuje między innymi, że Auto Clicker nie ładuje OCR/audio, a jego minimalny tracker fizycznych przycisków nie przetwarza ruchów myszy. Kopacz nie zawiera widoków PvP ani globalnego hooka myszy, starego harmonogramu clickera i diagnostyki. Sprawdzane są też oba składniki instalatora.
