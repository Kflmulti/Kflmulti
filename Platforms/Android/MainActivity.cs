using Android.App;
using Android.Content.PM;
using Android.OS;
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
    const int REQUEST_POST_NOTIFICATIONS = 1001;
    const string DAILY_CHANNEL_ID = "daily_channel";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // cria canal para Android 8+
        CreateNotificationChannel();

        // solicita permissão POST_NOTIFICATIONS em runtime para Android 13+
        if ((int)Build.VERSION.SdkInt >= (int)BuildVersionCodes.Tiramisu)
        {
            if (ContextCompat.CheckSelfPermission(this, global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new[] { global::Android.Manifest.Permission.PostNotifications }, REQUEST_POST_NOTIFICATIONS);
            }
        }
    }

    void CreateNotificationChannel()
    {
        if ((int)Build.VERSION.SdkInt < (int)BuildVersionCodes.O) return;

        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm == null) return;

        // evita recriar se já existir
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
            // opcional: registar/logar resultado
            bool granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            System.Diagnostics.Debug.WriteLine($"POST_NOTIFICATIONS granted: {granted}");
        }
    }
}