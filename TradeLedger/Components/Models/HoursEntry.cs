namespace Data.Models;

public class HoursEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public double HoursWorked { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Label { get; set; } = string.Empty;
}
