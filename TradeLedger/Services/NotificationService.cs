using Plugin.LocalNotification;

namespace TradeLedger.Services;

public class NotificationService
{
    // One slot per day-of-week: IDs 1001 (Sun) through 1007 (Sat)
    private const int ReminderBaseId = 1001;

    public async Task<bool> RequestPermissionAsync()
    {
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    // Schedules a weekly notification for each day set in workingDaysBitmask.
    // Bit position matches (int)DayOfWeek: bit 0 = Sunday … bit 6 = Saturday.
    public async Task ScheduleWorkingDayRemindersAsync(int hour, int minute, int workingDaysBitmask)
    {
        await LocalNotificationCenter.Current.RequestNotificationPermission();

        // Cancel all seven slots before rescheduling
        for (int d = 0; d < 7; d++)
            LocalNotificationCenter.Current.Cancel(ReminderBaseId + d);

        var today = DateTime.Today;

        for (int d = 0; d < 7; d++)
        {
            if ((workingDaysBitmask & (1 << d)) == 0) continue;

            var targetDow  = (DayOfWeek)d;
            int daysUntil  = ((int)targetDow - (int)today.DayOfWeek + 7) % 7;

            // If target is today but the time has already passed, push to next week
            if (daysUntil == 0 && today.AddHours(hour).AddMinutes(minute) <= DateTime.Now)
                daysUntil = 7;

            var notifyTime = today.AddDays(daysUntil).AddHours(hour).AddMinutes(minute);

            var request = new NotificationRequest
            {
                NotificationId = ReminderBaseId + d,
                Title          = "TradeLedger",
                Description    = "Don't forget to log your hours today!",
                BadgeNumber    = 1,
                Schedule       = new NotificationRequestSchedule
                {
                    NotifyTime = notifyTime,
                    RepeatType = NotificationRepeat.Weekly
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }
    }

    public void CancelDailyReminder()
    {
        for (int d = 0; d < 7; d++)
            LocalNotificationCenter.Current.Cancel(ReminderBaseId + d);
    }
}
