using Plugin.LocalNotification;

namespace TradeLedger.Services;

public class NotificationService(HoursTrackerService trackerService)
{
    // One slot per date within the rolling window, keyed off DateOnly.DayNumber so
    // each date maps to a stable, collision-free id while the window stays small.
    private const int ReminderBaseId = 1001;
    private const int WindowDays = 14;

    private static int IdForDate(DateOnly date) => ReminderBaseId + (date.DayNumber % 1000);

    public async Task<bool> RequestPermissionAsync()
    {
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    // Recomputes the rolling reminder window from current settings + logged hours:
    // a working day only gets a reminder if it doesn't already have hours logged and
    // its reminder time hasn't already passed. Safe to call after any change that could
    // affect either — new/edited/deleted entries, settings save, or app startup.
    public async Task RescheduleAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Clear a slightly wider range than the schedule window so leftovers from a
        // previous, differently-shifted window don't linger.
        for (int offset = -3; offset < WindowDays + 3; offset++)
            LocalNotificationCenter.Current.Cancel(IdForDate(today.AddDays(offset)));

        var settings = await trackerService.GetSettingsAsync();
        if (!settings.NotificationsEnabled) return;

        await LocalNotificationCenter.Current.RequestNotificationPermission();

        var windowEnd = today.AddDays(WindowDays - 1);
        var loggedDates = (await trackerService.GetEntriesInRangeAsync(today, windowEnd))
            .Select(e => e.Date)
            .ToHashSet();

        for (var date = today; date <= windowEnd; date = date.AddDays(1))
        {
            if ((settings.WorkingDays & (1 << (int)date.DayOfWeek)) == 0) continue;
            if (loggedDates.Contains(date)) continue;

            var notifyTime = date.ToDateTime(TimeOnly.MinValue)
                .AddHours(settings.NotificationHour)
                .AddMinutes(settings.NotificationMinute);
            if (notifyTime <= DateTime.Now) continue;

            var request = new NotificationRequest
            {
                NotificationId = IdForDate(date),
                Title          = "TradeLedger",
                Description    = "Don't forget to log your hours today!",
                BadgeNumber    = 1,
                Schedule       = new NotificationRequestSchedule { NotifyTime = notifyTime }
            };

            await LocalNotificationCenter.Current.Show(request);
        }
    }

    public void CancelAllReminders()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (int offset = -3; offset < WindowDays + 3; offset++)
            LocalNotificationCenter.Current.Cancel(IdForDate(today.AddDays(offset)));
    }
}
