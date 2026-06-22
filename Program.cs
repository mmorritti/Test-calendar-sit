using CalendarDemo.Data;
using CalendarDemo.Models;
using CalendarDemo.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

// Registra HttpClient e il nostro servizio
builder.Services.AddHttpClient<GoogleSheetsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

// ─────────────────────────────────────────
// MINIMAL API
// ─────────────────────────────────────────

// GET — legge dal Google Sheet in tempo reale
// GET — Unisce Google Sheets + Eventi Manuali
app.MapGet("/api/events", async (GoogleSheetsService sheets) =>
{
    // 1. Prendi eventi da Google
    var sheetEvents = await sheets.GetAllEventsAsync();

    // 3. Uniscili e mandala al browser
    var totalEvents = sheetEvents.ToList();
    return Results.Ok(totalEvents);
});

// POST, PUT, DELETE rimangono sull'EventStore per gli eventi manuali
app.MapPost("/api/events", (CalendarEvent newEvent) =>
{
    newEvent.Id = EventStore.GetNextId();
    EventStore.Events.Add(newEvent);
    return Results.Ok(newEvent);
});

app.MapPut("/api/events/{id}", (int id, CalendarEvent updated) =>
{
    var existing = EventStore.Events.FirstOrDefault(e => e.Id == id);
    if (existing is null) return Results.NotFound();

    existing.Title = updated.Title;
    existing.Start = updated.Start;
    existing.End = updated.End;
    existing.Color = updated.Color;
    existing.Description = updated.Description;
    existing.AllDay = updated.AllDay;
    existing.Location = updated.Location;
    existing.Tipologia = updated.Tipologia;
    existing.Regione = updated.Regione;

    return Results.Ok(existing);
});

app.MapDelete("/api/events/{id}", (int id) =>
{
    var existing = EventStore.Events.FirstOrDefault(e => e.Id == id);
    if (existing is null) return Results.NotFound();
    EventStore.Events.Remove(existing);
    return Results.Ok();
});

app.MapGet("/api/debug", async (GoogleSheetsService sheets) =>
{
    var url = $"https://docs.google.com/spreadsheets/d/1dHEzF9bbvsSFD07b4-XJA9Yw7ViCXu76F3razowZVA8/export?format=csv&sheet=LOMBARDIA";
    var http = new HttpClient();
    var csv = await http.GetStringAsync(url);
    return Results.Ok(csv);
});

app.MapGet("/api/eventbrite/preview", async (string url, IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(url))
        return Results.BadRequest(new { error = "URL mancante" });

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        return Results.BadRequest(new { error = "URL non valido" });

    if (!uri.Host.Contains("eventbrite", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Sono accettati solo link Eventbrite" });

    try
    {
        var http = httpClientFactory.CreateClient();

        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36"
        );

        var html = await http.GetStringAsync(uri);

        var imageUrl =
              ExtractMetaContent(html, "og:image")
              ?? ExtractMetaContent(html, "twitter:image")
              ?? ExtractMetaContent(html, "twitter:image:src");

        imageUrl = NormalizeEventbriteImageUrl(imageUrl, uri);

        return Results.Ok(new
        {
            imageUrl
        });

        return Results.Ok(new
        {
            imageUrl
        });
    }
    catch
    {
        return Results.Ok(new
        {
            imageUrl = (string?)null
        });
    }
});

static string? ExtractMetaContent(string html, string propertyName)
{
    var patternPropertyFirst =
        $"<meta[^>]+property=[\"']{System.Text.RegularExpressions.Regex.Escape(propertyName)}[\"'][^>]+content=[\"']([^\"']+)[\"']";

    var match = System.Text.RegularExpressions.Regex.Match(
        html,
        patternPropertyFirst,
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    );

    if (match.Success)
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);

    var patternContentFirst =
        $"<meta[^>]+content=[\"']([^\"']+)[\"'][^>]+property=[\"']{System.Text.RegularExpressions.Regex.Escape(propertyName)}[\"']";

    match = System.Text.RegularExpressions.Regex.Match(
        html,
        patternContentFirst,
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    );

    if (match.Success)
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);

    return null;
}

static string? NormalizeEventbriteImageUrl(string? imageUrl, Uri eventbriteUri)
{
    if (string.IsNullOrWhiteSpace(imageUrl))
        return null;

    imageUrl = System.Net.WebUtility.HtmlDecode(imageUrl);

    // Caso Eventbrite/Next.js:
    // /_next/image?url=https%3A%2F%2Fimg.evbuc.com%2F...
    if (imageUrl.StartsWith("/_next/image", StringComparison.OrdinalIgnoreCase))
    {
        var absoluteNextUrl = new Uri(eventbriteUri, imageUrl);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(absoluteNextUrl.Query);

        if (query.TryGetValue("url", out var realImageUrl))
        {
            var decoded = System.Net.WebUtility.UrlDecode(realImageUrl.ToString());
            return decoded;
        }

        return absoluteNextUrl.ToString();
    }

    // Caso URL relativo generico
    if (imageUrl.StartsWith("/", StringComparison.OrdinalIgnoreCase))
    {
        return new Uri(eventbriteUri, imageUrl).ToString();
    }

    // Caso URL già assoluto
    return imageUrl;
}

app.Run();