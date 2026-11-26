using Foundation;
using UIKit;

namespace CallBridge.Maui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    /// <summary>callbridge:// links (declared under CFBundleURLTypes in Info.plist).</summary>
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        DeepLinks.Handle(url.AbsoluteString);
        return true;
    }
}
