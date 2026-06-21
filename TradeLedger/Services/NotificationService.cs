using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;

namespace TradeLedger.Services;

public class NotificationService
{
    private const int DailyReminderId = 1001;

    public async Task<bool> RequestPermissionAsync()
    {
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    public async Task ScheduleDailyReminderAsync(int hour, int minute)
    {
        await LocalNotificationCenter.Current.RequestNotificationPermission();

        // Cancel any existing reminder before re-scheduling
        LocalNotificationCenter.Current.Cancel(DailyReminderId);

        var notifyTime = DateTime.Today.AddHours(hour).AddMinutes(minute);
        if (notifyTime <= DateTime.Now)
            notifyTime = notifyTime.AddDays(1);

        var request = new NotificationRequest
        {
            NotificationId = DailyReminderId,
            Title = "TradeLedger",
            Description = "Don't forget to log your hours today!",
            BadgeNumber = 1,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime,
                RepeatType = NotificationRepeat.Daily
            }
        };

        await LocalNotificationCenter.Current.Show(request);
    }

    public void CancelDailyReminder()
    {
        LocalNotificationCenter.Current.Cancel(DailyReminderId);
    }
}
