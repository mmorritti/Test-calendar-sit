namespace CalendarDemo.Models;

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string? End { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
    public bool AllDay { get; set; } = true;

    // campi nuovi
    public string? Location { get; set; }
    public string? Tipologia { get; set; }
    public string? Regione { get; set; }
}