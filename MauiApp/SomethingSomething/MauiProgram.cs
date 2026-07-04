using Microsoft.Extensions.Logging;

namespace SomethingSomething;

public static class MauiProgram
{
    // This method is the MAUI app startup entry for service registration and app-wide setup.
    public static MauiApp CreateMauiApp()
    {
        // The builder collects everything the mobile app needs before the app is created.
        var builder = MauiApp.CreateBuilder();

        builder
            // App is the root MAUI application class defined in App.xaml/App.xaml.cs.
            .UseMauiApp<App>()
            // ConfigureFonts makes these font aliases available anywhere in XAML by name.
            .ConfigureFonts(fonts =>
            {
                // OpenSansRegular can be used for normal body text.
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                // OpenSansSemibold is used for stronger headings and labels.
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        // Debug logging helps during development when checking runtime messages in Visual Studio.
        builder.Logging.AddDebug();
#endif

        // Build creates the final MAUI app object that the platform launches.
        return builder.Build();
    }
}
