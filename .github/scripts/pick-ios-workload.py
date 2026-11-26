"""Pick the newest .NET workload set whose iOS SDK matches an Xcode installed on this runner.

The newest MAUI workload can require an Xcode that the hosted macOS image does not have yet. Workload sets pin
every workload manifest, so an older set with a matching iOS manifest gives a consistent toolchain.
Prints "<workload set version> <Xcode path>" for the workflow.
"""
import glob
import re
import subprocess
import sys


def run(*args: str) -> str:
    return subprocess.run(args, capture_output=True, text=True).stdout


xcodes = {}
for path in glob.glob("/Applications/Xcode_*.app"):
    m = re.match(r"/Applications/Xcode_(\d+)\.(\d+)(?:\.\d+)?\.app$", path)
    if m:
        xcodes.setdefault((int(m.group(1)), int(m.group(2))), path)
print("Xcode versions on runner:", sorted(xcodes), file=sys.stderr)

listing = run("dotnet", "workload", "search", "version", "--take", "40")
sets = re.findall(r"^\s*(\d+\.\d+\.\d+(?:\.\d+)?)\s*$", listing, re.MULTILINE)
print("Workload sets:", sets, file=sys.stderr)

for version in sets:
    detail = run("dotnet", "workload", "search", "version", version)
    line = next((l for l in detail.splitlines() if "microsoft.net.sdk.ios" in l.lower()), "")
    # The line also holds the SDK feature band (10.0.x); the iOS manifest version is the one with an iOS-sized major.
    versions = [(int(a), int(b)) for a, b in re.findall(r"(\d+)\.(\d+)\.\d+", line) if int(a) >= 15]
    if not versions:
        print(f"  {version}: no iOS manifest in: {line.strip()!r}", file=sys.stderr)
        continue
    ios = versions[0]
    print(f"  {version}: iOS SDK {ios[0]}.{ios[1]}", file=sys.stderr)
    if ios in xcodes:
        print(version, xcodes[ios])
        sys.exit(0)

sys.exit("No workload set matches an installed Xcode")
