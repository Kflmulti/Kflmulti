using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui;

namespace Kflmulti.Platforms.Android;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.Keyboard
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.Navigation
        | ConfigChanges.Density
        | ConfigChanges.LayoutDirection
        | ConfigChanges.FontScale
        | ConfigChanges.Locale
        | ConfigChanges.SmallestScreenSize)]
public class MainActivity : MauiAppCompatActivity
{
    private const int REQUEST_POST_NOTIFICATIONS = 1001;
    private const string DAILY_CHANNEL_ID = "daily_channel";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // cria canal para Android 8+
        CreateNotificationChannel();

        // solicita permissão POST_NOTIFICATIONS (Android 13+)
        if ((int)Build.VERSION.SdkInt >= 33)
        {
            if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this,
                    new[] { global::Android.Manifest.Permission.PostNotifications },
                    REQUEST_POST_NOTIFICATIONS);
            }
        }
    }

    void CreateNotificationChannel()
    {
        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.O) return;

        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm == null) return;

        if (nm.GetNotificationChannel(DAILY_CHANNEL_ID) != null) return;

        var channel = new NotificationChannel(DAILY_CHANNEL_ID, "Resumo diário", NotificationImportance.High)
        {
            Description = "Canal para lembretes e resumo diário"
        };
        nm.CreateNotificationChannel(channel);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == REQUEST_POST_NOTIFICATIONS)
        {
            bool granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            System.Diagnostics.Debug.WriteLine($"POST_NOTIFICATIONS granted: {granted}");

            if (!granted)
            {
                Toast.MakeText(this,
                    "Permissão de notificações negada. O app não poderá enviar alertas.",
                    ToastLength.Long).Show();
            }
        }
    }
}
