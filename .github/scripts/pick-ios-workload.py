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
    m = re.search(r"microsoft\.net\.sdk\.ios\D*?(\d+)\.(\d+)\.\d+", detail, re.IGNORECASE)
    if not m:
        continue
    ios = (int(m.group(1)), int(m.group(2)))
    print(f"  {version}: iOS SDK {ios[0]}.{ios[1]}", file=sys.stderr)
    if ios in xcodes:
        print(version, xcodes[ios])
        sys.exit(0)

sys.exit("No workload set matches an installed Xcode")
