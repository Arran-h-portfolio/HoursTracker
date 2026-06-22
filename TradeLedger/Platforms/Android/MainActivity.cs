using Android.App;
using Android.Content.PM;
using Android.OS;

namespace TradeLedger;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Passing null prevents Android restoring stale fragment back-stack state
        // from a previous build, which causes the NavigationRootManager crash
        // (IllegalArgumentException: No view found for id is_pooling_container_tag).
        base.OnCreate(null);
    }
}
