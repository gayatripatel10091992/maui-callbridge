using Foundation;
using UIKit;

namespace CallBridge.Maui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    /// <summary>
    /// <c>-link callbridge://calls</c> on the launch command line opens that screen; CI uses it on the simulator,
    /// where opening a URL in a running app now asks "Open in CallBridge?" first.
    /// </summary>
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);
        DeepLinks.Handle(NSUserDefaults.StandardUserDefaults.StringForKey("link"));
        return result;
    }

    /// <summary>callbridge:// links (declared under CFBundleURLTypes in Info.plist).</summary>
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        DeepLinks.Handle(url.AbsoluteString);
        return true;
    }
}
