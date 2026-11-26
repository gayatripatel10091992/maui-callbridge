#!/usr/bin/env bash
# Installs the APK on the running emulator and captures each screen by opening it with a callbridge:// deep link.
# Fails if CallBridge is not the foreground app, so a broken screenshot is never committed.
set -euo pipefail

APP_ID="$1"
APK="$2"
OUT="docs/screenshots"
mkdir -p "$OUT"

adb wait-for-device
until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = "1" ]; do sleep 2; done
sleep 45   # a cold-booted emulator is slow for the first minute

dismiss_dialogs() {
  adb shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null 2>&1 || true
}

foreground() {
  adb shell dumpsys activity activities | grep -E "topResumedActivity|mResumedActivity" | grep -q "$APP_ID"
}

# $1 = file name, $2 = deep link, $3 = seconds to wait after launch
capture() {
  local file="$1" link="$2" wait="${3:-8}"
  adb shell am force-stop "$APP_ID"
  dismiss_dialogs
  adb shell am start -W -a android.intent.action.VIEW -d "$link" -p "$APP_ID" >/dev/null
  for _ in $(seq 1 45); do foreground && break; sleep 2; done
  sleep "$wait"
  dismiss_dialogs
  sleep 1
  if ! foreground; then
    echo "::error::CallBridge is not in the foreground; refusing to save $file"
    adb logcat -d -s AndroidRuntime:E monodroid:E DOTNET:E | tail -60 || true
    exit 1
  fi
  adb exec-out screencap -p > "$OUT/$file"
  echo "Saved $OUT/$file"
}

adb install -r "$APK"

# Clean status bar: fixed clock, no notifications.
adb shell settings put global sysui_demo_allowed 1
adb shell am broadcast -a com.android.systemui.demo -e command enter >/dev/null
adb shell am broadcast -a com.android.systemui.demo -e command clock -e hhmm 0930 >/dev/null
adb shell am broadcast -a com.android.systemui.demo -e command notifications -e visible false >/dev/null

adb shell cmd uimode night no
capture 01_calls.png "callbridge://calls"
capture 02_messages.png "callbridge://messages"
capture 03_voicemail.png "callbridge://voicemail"
capture 04_in_call.png "callbridge://dial?number=4155550134" 9
capture 05_plans.png "callbridge://plans?plan=callbridge_pro_monthly"
capture 06_settings.png "callbridge://settings"

adb shell cmd uimode night yes
capture 07_calls_dark.png "callbridge://calls"
capture 08_in_call_dark.png "callbridge://dial?number=2125550147" 9
adb shell cmd uimode night no

ls -la "$OUT"
