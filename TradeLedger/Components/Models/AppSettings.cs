namespace Data.Models;

public enum PayPeriodType { Monthly = 0, Weekly = 1 }

public class AppSettings
{
    public int Id { get; set; }

    // Stored as a fraction: 0.2 = 20%
    public double TaxRate { get; set; }
    public double HourlyWage { get; set; }

    public PayPeriodType PayPeriodType { get; set; } = PayPeriodType.Monthly;

    // Monthly: day of month (1–28). Weekly: DayOfWeek integer (0=Sun … 6=Sat).
    public int PayPeriodStartDay { get; set; } = 1;

    public bool IsPremium { get; set; } = false;
}
