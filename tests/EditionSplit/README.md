# EditionSplit

Lekki test regresji sprawdzający rozdzielenie wydania na dwa programy. Nie uruchamia GUI, nie wysyła wejścia i nie odczytuje ustawień użytkownika.

```powershell
dotnet run --project .\tests\EditionSplit\EditionSplit.csproj -c Release
```

Test pilnuje między innymi, że Auto Clicker nie ładuje OCR/audio ani globalnego hooka myszy, Kopacz nie zawiera widoków PvP oraz że instalator udostępnia oba składniki.
