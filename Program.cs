using CalendarDemo.Data;
using CalendarDemo.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

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

// GET — restituisce tutti gli eventi
app.MapGet("/api/events", (string? start, string? end) =>
{
    var result = EventStore.Events.AsEnumerable();

    if (!string.IsNullOrEmpty(start) && DateTime.TryParse(start, out var startDate))
        result = result.Where(e => DateTime.Parse(e.Start) >= startDate);

    if (!string.IsNullOrEmpty(end) && DateTime.TryParse(end, out var endDate))
        result = result.Where(e => DateTime.Parse(e.Start) <= endDate);

    return Results.Ok(result.ToList());
});

// POST — crea un nuovo evento
app.MapPost("/api/events", (CalendarEvent newEvent) =>
{
    newEvent.Id = EventStore.GetNextId();
    EventStore.Events.Add(newEvent);
    return Results.Ok(newEvent);
});

// PUT — aggiorna un evento esistente
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

    return Results.Ok(existing);
});

// DELETE — elimina un evento
app.MapDelete("/api/events/{id}", (int id) =>
{
    var existing = EventStore.Events.FirstOrDefault(e => e.Id == id);
    if (existing is null) return Results.NotFound();

    EventStore.Events.Remove(existing);
    return Results.Ok();
});

app.Run();
