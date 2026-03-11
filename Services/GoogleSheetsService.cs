using CalendarDemo.Models;
using System.Globalization;

namespace CalendarDemo.Services;

public class GoogleSheetsService
{
    private readonly HttpClient _http;
    private readonly string _sheetId;

    public GoogleSheetsService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _sheetId = "1XOgZtvlO7GTkTSIXtB5mlDw9lslzwWF_rByQVWuIXOM";
    }

    // Fogli da leggere con il nome della regione
    private static readonly Dictionary<string, string> Sheets = new()
    {
        { "0",          "LOMBARDIA" },
        { "32467924",   "PIEMONTE-LIGURIA" },
        { "1719809067", "LAZIO" },
        { "1166058384", "VENETO-FRIULI" },
        { "380669227",  "EMILIA ROMAGNA-MARCHE" },
        { "838374118",  "TOSCANA-UMBRIA" },
    };

    // Mappa tipologia → colore
    private static readonly Dictionary<string, string> ColoriTipologia = new(StringComparer.OrdinalIgnoreCase)
    {
        { "A", "#2D292D" }, // Skate the Vibes
        { "B", "#E55D27" }, // Shaka
        { "C", "#FCB445" }, // Open Day
        { "D", "#C9BBD0" }, // One Day
        { "E", "#50AFA8" }, // Time Out
        { "F", "#C82C2F" }, // Secret Santa on the Wave
        { "G", "#A0A0A0" }, // Clean Up
        { "H", "#A0A0A0" }, // Giorni Bloccati
    };

    // Mappa mesi italiani → numero
    private static readonly Dictionary<string, int> Mesi = new(StringComparer.OrdinalIgnoreCase)
    {
        { "GEN", 1 }, { "FEB", 2 }, { "MAR", 3 }, { "APR", 4 },
        { "MAG", 5 }, { "GIU", 6 }, { "LUG", 7 }, { "AGO", 8 },
        { "SET", 9 }, { "OTT", 10 }, { "NOV", 11 }, { "DIC", 12 }
    };



    public async Task<List<CalendarEvent>> GetAllEventsAsync()
    {
        var allEvents = new List<CalendarEvent>();
        int id = 1;

        foreach (var (sheetGid, regione) in Sheets)
        {
            try
            {
                var url = $"https://docs.google.com/spreadsheets/d/{_sheetId}/export?format=csv&gid={sheetGid}";
                var csv = await _http.GetStringAsync(url);
                var events = ParseSheet(csv, regione, ref id);
                allEvents.AddRange(events);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore lettura foglio {regione}: {ex.Message}");
            }
        }

        return allEvents;
    }

    private List<CalendarEvent> ParseSheet(string csv, string regione, ref int id)
    {
        var events = new List<CalendarEvent>();
        var lines = csv.Split('\n').Select(l => l.Trim()).ToArray();

        // Le prime 3 righe sono intestazioni, partiamo dalla riga 4
        string currentMese = "";

        for (int i = 3; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            if (cols.Count < 8) continue;

            // Colonna 0: MESE (può essere vuoto se è lo stesso del precedente)
            if (!string.IsNullOrWhiteSpace(cols[0]))
                currentMese = cols[0].Trim().ToUpper().Substring(0, 3);

            // Colonna 1: GIORNO
            var giornoRaw = cols[1].Trim();

            // Colonna 4 o 5: TIPOLOGIA (varia per foglio)
            var tipologiaRaw = cols.Count > 5 ? cols[4].Trim() : "";
            if (string.IsNullOrWhiteSpace(tipologiaRaw) && cols.Count > 5)
                tipologiaRaw = cols[5].Trim();

            // Colonna DESCRIZIONE EVENTO e LUOGO
            var descrizione = cols.Count > 7 ? cols[6].Trim() : "";
            var luogo = cols.Count > 8 ? cols[7].Trim() : "";

            // Salta righe senza dati utili
            if (string.IsNullOrWhiteSpace(giornoRaw) || string.IsNullOrWhiteSpace(currentMese))
                continue;

            // Costruisci la data
            if (!int.TryParse(giornoRaw, out int giorno)) continue;
            if (!Mesi.TryGetValue(currentMese, out int mese)) continue;

            var data = new DateTime(2026, mese, giorno);

            // Estrai codice tipologia (prima lettera)
            var codiceTipologia = tipologiaRaw.Length > 0
                ? tipologiaRaw.Substring(0, 1).ToUpper()
                : "A";

            // Colore dalla tipologia
            var colore = ColoriTipologia.TryGetValue(codiceTipologia, out var c) ? c : "#A0A0A0";

            // Nome leggibile tipologia
            var nomeTipologia = tipologiaRaw.Contains("-")
                ? tipologiaRaw.Substring(tipologiaRaw.IndexOf('-') + 1).Trim()
                : tipologiaRaw;

            events.Add(new CalendarEvent
            {
                Id = id++,
                Title = string.IsNullOrWhiteSpace(descrizione) ? nomeTipologia : descrizione,
                Start = data.ToString("yyyy-MM-dd"),
                Color = colore,
                Description = nomeTipologia,
                Location = luogo,
                Tipologia = codiceTipologia,
                Regione = regione,
                AllDay = true
            });
        }

        return events;
    }

    // Parser CSV che gestisce le virgole dentro le virgolette
    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = "";
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(current); current = ""; }
            else { current += c; }
        }

        result.Add(current);
        return result;
    }
}