using CalendarDemo.Models;

namespace CalendarDemo.Data
{
    public static class EventStore
    {
        private static int _nextId = 5;

        public static List<CalendarEvent> Events { get; } = new()
        {
            new() { Id = 1, Title = "🎉 Riunione Team",     Start = "2026-02-27", Color = "#3788d8", Description = "Riunione mensile" },
            new() { Id = 2, Title = "🚀 Deploy Produzione", Start = "2026-03-05", Color = "#e74c3c", Description = "Rilascio v2.0" },
            new() { Id = 3, Title = "📅 Workshop UX",       Start = "2026-03-10", End = "2026-03-12", Color = "#2ecc71", Description = "Workshop design" },
            new() { Id = 4, Title = "☕ Call con cliente",  Start = "2026-03-15T10:30:00", AllDay = false, Color = "#f39c12", Description = "Call Rossi S.r.l." },
        };

        public static int GetNextId() => _nextId++;
    }
}
