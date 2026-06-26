using CalendarDemo.Models;
using System.Globalization;

namespace CalendarDemo.Services;

public record SheetConfig(
    string Gid,
    string Regione,
    int ColMese,
    int ColGiorno,
    int ColTipologia,
    int ColDescrizione,
    int ColLuogo,
    int? ColEventbrite = null
);

public class GoogleSheetsService
{
    private readonly HttpClient _http;
    private readonly string _sheetId;

    public GoogleSheetsService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _sheetId = "1dHEzF9bbvsSFD07b4-XJA9Yw7ViCXu76F3razowZVA8";
    }

    private static readonly List<SheetConfig> Sheets = new()
    {
        new SheetConfig("0",          "LOMBARDIA",             0, 1, 4, 6, 7, 18),
        new SheetConfig("32467924",   "PIEMONTE-LIGURIA",      0, 1, 5, 7, 8, 18),
        new SheetConfig("1719809067", "LAZIO",                 0, 1, 5, 7, 8, 18),
        new SheetConfig("1166058384", "VENETO-FRIULI",         0, 1, 5, 7, 8, 18),
        new SheetConfig("380669227",  "EMILIA ROMAGNA-MARCHE", 0, 1, 5, 7, 8, 18),
        new SheetConfig("838374118",  "TOSCANA-UMBRIA",        0, 1, 5, 7, 8, 18),
    };

    // Foglio nuovo: colonne A-G => MESE, GIORNO, TIPOLOGIA, DONE, DESCRIZIONE EVENTO, LUOGO, LINK EVENTBRITE.
    // Lo leggiamo per nome foglio, così non serve conoscere/aggiornare il gid.
    private static readonly SheetConfig InterregionaleSheet =
        new SheetConfig("INTERREGIONALE", "INTERREGIONALE", 0, 1, 2, 4, 5, 6);

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

                var events = ParseSheet(csv, config, ref id);
                allEvents.AddRange(events);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore lettura foglio {config.Regione}: {ex.Message}");
            }
        }

        var interregionaleEvents = new List<CalendarEvent>();

        try
        {
            var sheetName = Uri.EscapeDataString(InterregionaleSheet.Gid);
            var url = $"https://docs.google.com/spreadsheets/d/{_sheetId}/gviz/tq?tqx=out:csv&sheet={sheetName}";
            var csv = await _http.GetStringAsync(url);

            interregionaleEvents = ParseSheet(csv, InterregionaleSheet, ref id);
            allEvents.AddRange(interregionaleEvents);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore lettura foglio INTERREGIONALE: {ex.Message}");
        }

        // Se il foglio INTERREGIONALE viene letto correttamente, gli eventi H/GIORNI BLOCCATI
        // presenti nei fogli regionali vengono nascosti per evitare doppioni.
        if (interregionaleEvents.Count > 0)
        {
            allEvents = RemoveRegionalBlockedEvents(allEvents);
        }

        return allEvents;
    }

    private List<CalendarEvent> ParseSheet(string csv, SheetConfig config, ref int id)
    {
        var events = new List<CalendarEvent>();
        var lines = csv.Split('\n').Select(l => l.Trim()).ToArray();
        string currentMese = "";
        int currentMeseNum = 0;

        for (int i = 3; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);
            if (cols.Count <= config.ColGiorno) continue;

            // --- LOGICA MESE ---
            var meseRaw = cols.Count > config.ColMese ? cols[config.ColMese].Trim().ToUpper() : "";
            if (!string.IsNullOrWhiteSpace(meseRaw))
            {
                string meseTesto = new string(meseRaw.Where(char.IsLetter).ToArray());
                if (meseTesto.Length >= 3)
                {
                    string abbreviato = meseTesto.Substring(0, 3);
                    if (Mesi.TryGetValue(abbreviato, out int mFound))
                    {
                        currentMese = abbreviato;
                        currentMeseNum = mFound;
                        if (config.Regione == "PIEMONTE-LIGURIA")
                            Console.WriteLine($"[PIEMONTE] Rilevato mese: {currentMese} alla riga {i}");
                    }
                }
            }

            // --- LOGICA GIORNO ---
            var giornoRaw = cols[config.ColGiorno].Trim(' ', '"', '\t');

            // DEBUG ESTREMO: Se siamo in Piemonte ad Aprile, stampiamo TUTTA la riga
            if (config.Regione == "PIEMONTE-LIGURIA" && currentMeseNum == 4)
            {
                Console.WriteLine($"[DEBUG PIEMONTE APR] Riga {i} | Contenuto Colonna GG: '{giornoRaw}' | Intera riga: {lines[i]}");
            }

            if (string.IsNullOrWhiteSpace(giornoRaw) || currentMeseNum == 0) continue;

            var matchNumero = System.Text.RegularExpressions.Regex.Match(giornoRaw, @"\d+");
            if (matchNumero.Success && int.TryParse(matchNumero.Value, out int giorno))
            {
                try
                {
                    var data = new DateTime(2026, currentMeseNum, giorno);
                    var tipologiaRaw = cols.Count > config.ColTipologia ? cols[config.ColTipologia].Trim() : "";
                    var descrizione = cols.Count > config.ColDescrizione ? cols[config.ColDescrizione].Trim() : "";
                    var luogo = cols.Count > config.ColLuogo ? cols[config.ColLuogo].Trim() : "";

                    var eventbriteLink = config.ColEventbrite.HasValue && cols.Count > config.ColEventbrite.Value
                        ? cols[config.ColEventbrite.Value].Trim() : "";

                    if (descrizione.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) descrizione = "";

                    if (string.IsNullOrWhiteSpace(tipologiaRaw)
                        && string.IsNullOrWhiteSpace(descrizione)
                        && string.IsNullOrWhiteSpace(luogo))
                    {
                        continue;
                    }

                    var codiceTipologia = tipologiaRaw.Length > 0 ? tipologiaRaw.Substring(0, 1).ToUpper() : "A";
                    var colore = ColoriTipologia.TryGetValue(codiceTipologia, out var c) ? c : "#A0A0A0";
                    var nomeTipologia = tipologiaRaw.Contains("-") ? tipologiaRaw.Substring(tipologiaRaw.IndexOf('-') + 1).Trim() : tipologiaRaw;

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
                        EventbriteLink = IsValidUrl(eventbriteLink) ? eventbriteLink : null,
                        AllDay = true
                    });
                }
                catch { continue; }
            }
        }
        return events;
    }

    private static List<CalendarEvent> RemoveRegionalBlockedEvents(List<CalendarEvent> events)
    {
        return events
            .Where(e =>
                string.Equals(e.Regione, "INTERREGIONALE", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(e.Tipologia, "H", StringComparison.OrdinalIgnoreCase))
            .ToList();
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

    private static bool IsValidUrl(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}