using CalendarDemo.Models;
using System.Globalization;

namespace CalendarDemo.Services;

public record SheetConfig(string Gid, string Regione, int ColMese, int ColGiorno, int ColTipologia, int ColDescrizione, int ColLuogo);

public class GoogleSheetsService
{
    private readonly HttpClient _http;
    private readonly string _sheetId;

    public GoogleSheetsService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _sheetId = "1XOgZtvlO7GTkTSIXtB5mlDw9lslzwWF_rByQVWuIXOM";
    }

    private static readonly List<SheetConfig> Sheets = new()
    {
        new SheetConfig("0",          "LOMBARDIA",             0, 1, 4, 6, 7),
        new SheetConfig("32467924",   "PIEMONTE-LIGURIA",      0, 1, 5, 7, 8),
        new SheetConfig("1719809067", "LAZIO",                 0, 1, 5, 7, 8),
        new SheetConfig("1166058384", "VENETO-FRIULI",         0, 1, 5, 7, 8),
        new SheetConfig("380669227",  "EMILIA ROMAGNA-MARCHE", 0, 1, 5, 7, 8),
        new SheetConfig("838374118",  "TOSCANA-UMBRIA",        0, 1, 5, 7, 8),
    };

    private static readonly Dictionary<string, string> ColoriTipologia = new(StringComparer.OrdinalIgnoreCase)
    {
        { "A", "#2D292D" }, { "B", "#E55D27" }, { "C", "#FCB445" },
        { "D", "#C9BBD0" }, { "E", "#50AFA8" }, { "F", "#C82C2F" },
        { "G", "#A0A0A0" }, { "H", "#A0A0A0" },
    };

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

        foreach (var config in Sheets)
        {
            try
            {
                var url = $"https://docs.google.com/spreadsheets/d/{_sheetId}/export?format=csv&gid={config.Gid}";
                var csv = await _http.GetStringAsync(url);

                // --- INIZIO TEST GREZZO LOMBARDIA ---
                if (config.Regione == "LOMBARDIA")
                {
                    Console.WriteLine("\n=== INIZIO LETTURA REALE LOMBARDIA ===");
                    var rawLines = csv.Split('\n').Take(6).ToArray(); // Prende le prime 6 righe
                    for (int i = 0; i < rawLines.Length; i++)
                    {
                        Console.WriteLine($"RIGA {i}: {rawLines[i].Trim()}");
                    }
                    Console.WriteLine("=== FINE LETTURA REALE LOMBARDIA ===\n");
                }
                // --- FINE TEST ---

                var events = ParseSheet(csv, config, ref id);
                allEvents.AddRange(events);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore lettura foglio {config.Regione}: {ex.Message}");
            }
        }

        return allEvents;
    }

    private List<CalendarEvent> ParseSheet(string csv, SheetConfig config, ref int id)
    {
        var events = new List<CalendarEvent>();
        var lines = csv.Split('\n').Select(l => l.Trim()).ToArray();

        string currentMese = "";

        // Partiamo dalla riga 4 (indice 3)
        for (int i = 3; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            if (cols.Count <= config.ColGiorno) continue;

            // --- LETTURA MESE E GIORNO ---
            var meseRaw = cols.Count > config.ColMese ? cols[config.ColMese].Trim() : "";
            if (!string.IsNullOrWhiteSpace(meseRaw))
                currentMese = meseRaw.ToUpper().Length >= 3 ? meseRaw.ToUpper().Substring(0, 3) : meseRaw.ToUpper();

            var giornoRaw = cols[config.ColGiorno].Trim(' ', '"');

            if (string.IsNullOrWhiteSpace(giornoRaw) || string.IsNullOrWhiteSpace(currentMese))
                continue;

            // --- GESTIONE DATE: ESTRAZIONE NUMERO ---
            DateTime data = DateTime.MinValue;

            // Questo trucco magico estrae SOLO I NUMERI dalla scritta (es: da "giovedì 15" tira fuori "15")
            var matchNumero = System.Text.RegularExpressions.Regex.Match(giornoRaw, @"\d+");

            // 1. Caso principale: siamo riusciti a estrarre un numero di giorno
            if (matchNumero.Success && int.TryParse(matchNumero.Value, out int giorno) && Mesi.TryGetValue(currentMese, out int mese))
            {
                // Se per caso Google Sheets dovesse sparare un giorno seriale strano (tipo 45672)
                if (giorno > 30000)
                {
                    data = DateTime.FromOADate(giorno);
                }
                else
                {
                    data = new DateTime(2026, mese, giorno);
                }
            }
            // 2. Piano B: forse è già una data intera (es "2026-01-15")
            else if (DateTime.TryParse(giornoRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                data = parsedDate;
            }
            else
            {
                // Riga incomprensibile, saltiamo
                continue;
            }

            // --- LETTURA ALTRE COLONNE ---
            var tipologiaRaw = cols.Count > config.ColTipologia ? cols[config.ColTipologia].Trim() : "";
            var descrizione = cols.Count > config.ColDescrizione ? cols[config.ColDescrizione].Trim() : "";
            var luogo = cols.Count > config.ColLuogo ? cols[config.ColLuogo].Trim() : "";

            if (descrizione.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                descrizione.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            {
                descrizione = "";
            }

            var codiceTipologia = tipologiaRaw.Length > 0 ? tipologiaRaw.Substring(0, 1).ToUpper() : "A";
            var colore = ColoriTipologia.TryGetValue(codiceTipologia, out var c) ? c : "#A0A0A0";

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
                Regione = config.Regione,
                AllDay = true
            });
        }

        return events;
    }

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