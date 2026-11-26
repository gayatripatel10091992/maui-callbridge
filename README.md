# CallBridge · .NET MAUI (Windows, iOS, Android)

[![CI](https://github.com/gayatripatel10091992/maui-callbridge/actions/workflows/ci.yml/badge.svg)](https://github.com/gayatripatel10091992/maui-callbridge/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![MAUI](https://img.shields.io/badge/.NET%20MAUI-Windows%20%7C%20iOS%20%7C%20Android-512BD4)
![C#](https://img.shields.io/badge/C%23-MVVM%20Toolkit-239120?logo=csharp&logoColor=white)

The VoIP calling and texting app from [voip-callbridge](https://github.com/gayatripatel10091992/voip-callbridge)
(Kotlin), rebuilt in **.NET MAUI and C#**. One project targets **Windows, iOS and Android** (plus Mac Catalyst).
This mirrors my work at Cygnet.One, where I build the new .NET MAUI versions of a set of VoIP apps alongside their
native Android versions.

> Portfolio project. Original code with fictional data (555-01xx numbers); no client code, branding or APIs.

## Screenshots

One codebase, three platforms. CI builds each app on its own runner, opens every screen with the same
`callbridge://` link and commits the screenshots:

- **Android**: release APK on an API 34 emulator (Ubuntu runner).
- **iOS**: simulator build on an iPhone simulator (macOS runner), links opened with `simctl openurl`.
- **Windows**: unpackaged WinUI app (Windows runner), links passed on the command line.

| | Calls | In call | Plans |
|---|---|---|---|
| **Android** | <img src="docs/screenshots/android/01_calls.png" width="180"/> | <img src="docs/screenshots/android/04_in_call.png" width="180"/> | <img src="docs/screenshots/android/05_plans.png" width="180"/> |
| **iOS** | <img src="docs/screenshots/ios/01_calls.png" width="180"/> | <img src="docs/screenshots/ios/04_in_call.png" width="180"/> | <img src="docs/screenshots/ios/05_plans.png" width="180"/> |
| **Windows** | <img src="docs/screenshots/windows/01_calls.png" width="240"/> | <img src="docs/screenshots/windows/04_in_call.png" width="240"/> | <img src="docs/screenshots/windows/05_plans.png" width="240"/> |

<details open>
<summary><b>Android</b>: all screens</summary>

| Calls | Messages | Voicemail | In call |
|---|---|---|---|
| <img src="docs/screenshots/android/01_calls.png" width="180"/> | <img src="docs/screenshots/android/02_messages.png" width="180"/> | <img src="docs/screenshots/android/03_voicemail.png" width="180"/> | <img src="docs/screenshots/android/04_in_call.png" width="180"/> |

| Plans (from web signup) | Settings | Calls (dark) | In call (dark) |
|---|---|---|---|
| <img src="docs/screenshots/android/05_plans.png" width="180"/> | <img src="docs/screenshots/android/06_settings.png" width="180"/> | <img src="docs/screenshots/android/07_calls_dark.png" width="180"/> | <img src="docs/screenshots/android/08_in_call_dark.png" width="180"/> |

</details>

<details>
<summary><b>iOS</b>: all screens</summary>

| Calls | Messages | Voicemail | In call |
|---|---|---|---|
| <img src="docs/screenshots/ios/01_calls.png" width="180"/> | <img src="docs/screenshots/ios/02_messages.png" width="180"/> | <img src="docs/screenshots/ios/03_voicemail.png" width="180"/> | <img src="docs/screenshots/ios/04_in_call.png" width="180"/> |

| Plans (from web signup) | Settings | Calls (dark) | In call (dark) |
|---|---|---|---|
| <img src="docs/screenshots/ios/05_plans.png" width="180"/> | <img src="docs/screenshots/ios/06_settings.png" width="180"/> | <img src="docs/screenshots/ios/07_calls_dark.png" width="180"/> | <img src="docs/screenshots/ios/08_in_call_dark.png" width="180"/> |

</details>

<details>
<summary><b>Windows</b>: all screens</summary>

| Calls | Messages | Voicemail | In call |
|---|---|---|---|
| <img src="docs/screenshots/windows/01_calls.png" width="200"/> | <img src="docs/screenshots/windows/02_messages.png" width="200"/> | <img src="docs/screenshots/windows/03_voicemail.png" width="200"/> | <img src="docs/screenshots/windows/04_in_call.png" width="200"/> |

| Plans (from web signup) | Settings | Calls (dark) | In call (dark) |
|---|---|---|---|
| <img src="docs/screenshots/windows/05_plans.png" width="200"/> | <img src="docs/screenshots/windows/06_settings.png" width="200"/> | <img src="docs/screenshots/windows/07_calls_dark.png" width="200"/> | <img src="docs/screenshots/windows/08_in_call_dark.png" width="200"/> |

</details>

## Architecture

```mermaid
graph LR
    subgraph core ["CallBridge.Core (net10.0) · xUnit-tested"]
        sm["CallStateMachine<br/>handover · reconnect · drop"]
        billing["EntitlementResolver · PlansPresenter<br/>ISubscriptionStore"]
        links["DeepLinkParser · HandoffParser"]
        misc["PhoneNumbers · CallScreener · OtpParser · RecentsPresenter"]
        vms["ViewModels (CommunityToolkit.Mvvm)<br/>Recents · Dialer · InCall · Messages · Voicemail · Plans · Settings"]
    end
    subgraph app ["CallBridge.Maui (android · ios · maccatalyst · windows)"]
        shell["AppShell: TabBar + routes"]
        pages["XAML pages with compiled bindings"]
        plat["Platforms/: Android intent filters · iOS URL types · WinUI"]
        ess["MAUI Essentials Connectivity → NetworkChanged"]
    end
    vms --> sm & billing & misc
    pages --> vms
    shell --> pages
    plat --> links
    ess --> sm
```

- **The core is the same design as the Kotlin app**, ported to C# records and pattern matching: the call state machine,
  subscription entitlement rules (upgrade now, downgrade at renewal, pending purchases never acknowledged), deep-link
  and web-handoff validation, spam screening, OTP parsing and call-log grouping.
- **View models are UI-framework free** (`net10.0`), so 30+ xUnit tests cover them without a device.
- **Platform code stays at the edges**: Android `[IntentFilter]`s and an iOS `OpenUrl` override feed one `DeepLinks`
  router; MAUI Essentials `Connectivity` feeds Wi-Fi/cellular changes into the state machine on every OS.
- **Stores**: `ISubscriptionStore` separates billing from the UI. The demo store follows the same plan-change rules on
  every platform; Google Play Billing, StoreKit 2 and the Microsoft Store each plug in behind it.

## CI

| Job | Runner | What it does |
|---|---|---|
| Core + view model tests | Ubuntu | `dotnet test` (xUnit) |
| Android | Ubuntu | Release APK, installed on an API 34 emulator; captures the Android screenshots |
| iOS | macOS | Simulator build with a workload set matched to the runner's Xcode; captures the iOS screenshots |
| Windows | Windows | Unpackaged WinUI app (`net10.0-windows10.0.19041.0`); captures the Windows screenshots |

## Build

```bash
dotnet test tests/CallBridge.Core.Tests
dotnet build src/CallBridge.Maui -f net10.0-android
dotnet build src/CallBridge.Maui -f net10.0-windows10.0.19041.0   # on Windows
dotnet build src/CallBridge.Maui -f net10.0-ios                    # on macOS with Xcode
```

Requires the .NET 10 SDK and `dotnet workload install maui`.

## License

MIT
