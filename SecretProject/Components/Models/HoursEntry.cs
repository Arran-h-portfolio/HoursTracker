namespace Data.Models;
// hours entry replaces 'Dictionary<DateOnly, double>'
public class HoursEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public double HoursWorked { get; set; }
}

// In SQlite a primary key is needed, 
// here Id is picked up as the primary key by naming convention
