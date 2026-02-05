using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using PdfSharpCore.Fonts;
using Plugin.LocalNotification;

namespace Kflmulti;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
#if ANDROID
            .UseLocalNotification() // extensão do plugin
#endif   

            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                
                // Remova se não tiver esse arquivo:
                //fonts.AddFont("NotoColorEmoji.ttf", "NotoColorEmoji");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        GlobalFontSettings.FontResolver = new CustomFontResolver();
        return builder.Build();
    }
}