using System.Dynamic;
using System.Globalization;

namespace Data.Models;

public class TrackingData()
{
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public double HoursWorked { get; set; }
    public DateTime PayDay { get; set; }
    // timespan to represent time between pays i.e. 1st of June to 1st of July
    public TimeSpan PayPeriod { get; set; }
    // Current day variable allows the calculation of current wages up until the current day
    public DateTime CurrentDay { get; set; } = DateTime.Now;
    public double HourlyWage { get; set; }
    public double CurrentEarnings { get; set; }
    // Using dictionary to bind data together keeping date and hours worked together
    public Dictionary<DateOnly, double> HoursLogged { get; set; } = new Dictionary<DateOnly, double>();


}