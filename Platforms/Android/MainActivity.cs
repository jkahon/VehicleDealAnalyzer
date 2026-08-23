using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui; // <--- Required for MauiAppCompatActivity

namespace VehicleDealAnalyzer;

[Activity(
    Theme = "@style/Maui.SplashTheme", 
    MainLauncher = true, 
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
    Exported = true)]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/plain")]
public class MainActivity : MauiAppCompatActivity // <--- Inherit from MauiAppCompatActivity instead
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIncomingIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIncomingIntent(intent);
    }

    private void HandleIncomingIntent(Intent? intent)
    {
        if (intent?.Action == Intent.ActionSend && intent.Type == "text/plain")
        {
            string? sharedText = intent.GetStringExtra(Intent.ExtraText);
            if (!string.IsNullOrEmpty(sharedText))
            {
                // Pass incoming text to application state
            }
        }
    }
}
