#!/usr/bin/env bash
# Installs the simulator build, then captures each screen by opening it with a callbridge:// link.
# Fails if CallBridge is not running after a link, so a screenshot of the home screen is never committed.
set -euo pipefail

APP_ID="$1"
APP="$2"
OUT="docs/screenshots/ios"
mkdir -p "$OUT"

# Newest iPhone Pro simulator on this Xcode.
DEVICE=$(xcrun simctl list devices available -j | python3 -c '
import json, sys, re
devices = json.load(sys.stdin)["devices"]
best = None
for runtime, items in devices.items():
    if "iOS" not in runtime:
        continue
    version = tuple(int(x) for x in re.findall(r"\d+", runtime.split("iOS")[-1]))
    for d in items:
        if re.fullmatch(r"iPhone \d+ Pro", d["name"]):
            key = (version, d["name"])
            if best is None or key > best[0]:
                best = (key, d["udid"], d["name"], runtime)
print(best[1]); print(best[2] + " / " + best[3], file=sys.stderr)
')
echo "Simulator: $DEVICE"
xcrun simctl boot "$DEVICE" || true
xcrun simctl bootstatus "$DEVICE" -b
xcrun simctl status_bar "$DEVICE" override --time "9:41" --batteryState charged --batteryLevel 100 --wifiBars 3 --cellularBars 4
xcrun simctl install "$DEVICE" "$APP"
xcrun simctl launch "$DEVICE" "$APP_ID" >/dev/null
sleep 20   # first launch on a fresh simulator is slow
xcrun simctl terminate "$DEVICE" "$APP_ID" || true

running() { xcrun simctl spawn "$DEVICE" launchctl list | grep -q "UIKitApplication:$APP_ID"; }

# $1 = file name, $2 = link, $3 = seconds to wait after opening
capture() {
  local file="$1" link="$2" wait="${3:-6}"
  xcrun simctl terminate "$DEVICE" "$APP_ID" 2>/dev/null || true
  sleep 1
  xcrun simctl openurl "$DEVICE" "$link"
  sleep "$wait"
  if ! running; then
    echo "::error::CallBridge is not running after opening $link; refusing to save $file"
    exit 1
  fi
  xcrun simctl io "$DEVICE" screenshot "$OUT/$file" >/dev/null
  echo "Saved $OUT/$file"
}

xcrun simctl ui "$DEVICE" appearance light
capture 01_calls.png "callbridge://calls"
capture 02_messages.png "callbridge://messages"
capture 03_voicemail.png "callbridge://voicemail"
capture 04_in_call.png "callbridge://dial?number=4155550134" 9
capture 05_plans.png "callbridge://plans?plan=callbridge_pro_monthly"
capture 06_settings.png "callbridge://settings"

xcrun simctl ui "$DEVICE" appearance dark
capture 07_calls_dark.png "callbridge://calls"
capture 08_in_call_dark.png "callbridge://dial?number=2125550147" 9

ls -la "$OUT"
