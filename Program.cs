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
app.MapGet("/api/events", async (GoogleSheetsService sheets, string? start, string? end) =>
{
    var events = await sheets.GetAllEventsAsync();

    if (!string.IsNullOrEmpty(start) && DateTime.TryParse(start, out var startDate))
        events = events.Where(e => DateTime.Parse(e.Start) >= startDate).ToList();

    if (!string.IsNullOrEmpty(end) && DateTime.TryParse(end, out var endDate))
        events = events.Where(e => DateTime.Parse(e.Start) <= endDate).ToList();

    return Results.Ok(events);
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
    var url = $"https://docs.google.com/spreadsheets/d/1XOgZtvlO7GTkTSIXtB5mlDw9lslzwWF_rByQVWuIXOM/export?format=csv&sheet=LOMBARDIA";
    var http = new HttpClient();
    var csv = await http.GetStringAsync(url);
    return Results.Ok(csv);
});

app.Run();