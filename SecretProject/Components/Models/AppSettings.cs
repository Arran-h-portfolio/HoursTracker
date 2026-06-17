namespace Data.Models;

public class AppSettings
{
    public int Id { get; set; }

    // Stored as a fraction: 0.2 = 20%. The Settings page converts
    // to/from a 0-100 percentage for display
    public double TaxRate { get; set; }
    public double HourlyWage { get; set; }

    // Carried over from original model Data.cs - not wired into the UI yet
    // Kept for a future 'earnings this pay period' feature
    public DateTime PayDay { get; set; }
    public TimeSpan PayPeriod { get; set; }
}