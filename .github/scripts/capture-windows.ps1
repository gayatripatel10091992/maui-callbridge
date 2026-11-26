# Starts the unpackaged Windows build once per screen, passing the same callbridge:// links the phones use,
# and saves the app window (without the desktop around it). Fails if the window never appears.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Out = "docs/screenshots/windows"
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Out | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT rect, int size);
}
"@
[Win32]::SetProcessDPIAware() | Out-Null
Write-Host "Display: $([System.Windows.Forms.SystemInformation]::PrimaryMonitorSize)"

function Capture([string]$File, [string[]]$AppArgs, [int]$Wait = 8) {
    Get-Process -Name "CallBridge.Maui" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    $process = Start-Process -FilePath $Exe -ArgumentList ($AppArgs + @("--size", "440x720")) -PassThru
    $handle = [IntPtr]::Zero
    for ($i = 0; $i -lt 90 -and $handle -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Seconds 1
        $process.Refresh()
        if ($process.HasExited) { throw "CallBridge exited with code $($process.ExitCode) before showing a window ($File)" }
        $handle = $process.MainWindowHandle
    }
    if ($handle -eq [IntPtr]::Zero) { throw "CallBridge window did not appear ($File)" }
    Start-Sleep -Seconds $Wait
    [Win32]::ShowWindow($handle, 9) | Out-Null   # SW_RESTORE
    [Win32]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Seconds 1

    # Extended frame bounds: the visible window, without the invisible resize border and shadow.
    $rect = New-Object Win32+RECT
    [Win32]::DwmGetWindowAttribute($handle, 9, [ref]$rect, [System.Runtime.InteropServices.Marshal]::SizeOf($rect)) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $path = Join-Path $Out $File
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
    Write-Host "Saved $path ($width x $height)"
}

Capture "01_calls.png" @("callbridge://calls")
Capture "02_messages.png" @("callbridge://messages")
Capture "03_voicemail.png" @("callbridge://voicemail")
Capture "04_in_call.png" @("callbridge://dial?number=4155550134") 10
Capture "05_plans.png" @("callbridge://plans?plan=callbridge_pro_monthly")
Capture "06_settings.png" @("callbridge://settings")
Capture "07_calls_dark.png" @("callbridge://calls", "--dark")
Capture "08_in_call_dark.png" @("callbridge://dial?number=2125550147", "--dark") 10

Get-Process -Name "CallBridge.Maui" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-ChildItem $Out
