"""Save a simulator screenshot once the app has drawn and settled.

Launch animations and the first blank frame are rejected; small live changes (a call timer, a spinner) are fine.
Usage: settle.py <device> <out.png>. Exits 1 if the screen never settles within ~60 s.
"""
import subprocess
import sys
import time

from PIL import Image, ImageChops, ImageStat

device, out = sys.argv[1], sys.argv[2]
tmp = out + ".tmp.png"


def grab():
    subprocess.run(["xcrun", "simctl", "io", device, "screenshot", tmp], check=True, capture_output=True)
    image = Image.open(tmp).convert("L")
    w, h = image.size
    # Below the status bar, scaled down: enough to see layout, blind to anti-aliasing noise.
    return image.crop((0, int(h * 0.06), w, h)).resize((120, 240))


previous = None
for _ in range(30):
    time.sleep(2)
    try:
        current = grab()
    except subprocess.CalledProcessError:
        continue
    drawn = ImageStat.Stat(current).stddev[0] > 12          # a blank frame is one flat colour
    if previous is not None and drawn:
        changed = ImageStat.Stat(ImageChops.difference(current, previous)).mean[0]
        if changed < 1.5:                                    # launch zoom moves everything; a timer barely registers
            Image.open(tmp).save(out)
            print(f"settled (stddev {ImageStat.Stat(current).stddev[0]:.1f}, change {changed:.2f})")
            sys.exit(0)
    previous = current
sys.exit("screen did not settle")
