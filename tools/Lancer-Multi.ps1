# Lance plusieurs instances de DEVOUR sur le même PC pour tester DevourMorePlayers.
# Steam doit être lancé et connecté. L'instance #1 est l'hôte, les autres rejoignent par le menu du jeu.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Lancer-Multi.ps1 -Count 6

param(
    [ValidateRange(1, 8)][int]$Count = 4,
    [string]$GamePath = "",
    [int]$Width = 0,
    [int]$Height = 0,
    [int]$DelaySeconds = 20,
    [switch]$Sound,
    [switch]$NoTile
)

$ErrorActionPreference = "Stop"
$AppId = "1274570"

function Find-Devour {
    $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
    if (-not $steam) { return $null }
    $libs = @($steam)
    $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $libs += $m.Groups[1].Value -replace '\\\\', '\'
        }
    }
    foreach ($lib in $libs | Select-Object -Unique) {
        $p = Join-Path $lib "steamapps\common\Devour"
        if (Test-Path (Join-Path $p "DEVOUR.exe")) { return $p }
    }
    return $null
}

if (-not $GamePath) {
    if (Test-Path (Join-Path $PSScriptRoot "DEVOUR.exe")) { $GamePath = $PSScriptRoot } else { $GamePath = Find-Devour }
}
if (-not $GamePath -or -not (Test-Path (Join-Path $GamePath "DEVOUR.exe"))) {
    Write-Host "DEVOUR.exe introuvable. Relance avec -GamePath `"chemin\vers\Devour`"." -ForegroundColor Red
    exit 1
}
$exe = Join-Path $GamePath "DEVOUR.exe"
Write-Host "Jeu : $exe"

if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
    Write-Host "Steam doit être lancé et connecté avant. Lance Steam puis relance ce script." -ForegroundColor Red
    exit 1
}

# Permet de lancer DEVOUR.exe directement (sans passer par Steam) plusieurs fois
$appidFile = Join-Path $GamePath "steam_appid.txt"
if (-not (Test-Path $appidFile)) {
    Set-Content -Path $appidFile -Value $AppId -NoNewline -Encoding ASCII
    Write-Host "steam_appid.txt créé ($AppId)."
}

if (-not (Test-Path (Join-Path $GamePath "Mods\DevourMorePlayers.dll"))) {
    Write-Host "Attention : Mods\DevourMorePlayers.dll absent, les clients seront refusés par l'hôte." -ForegroundColor Yellow
}

$logs = Join-Path ([IO.Path]::GetTempPath()) "DevourMultiTest"
New-Item -ItemType Directory -Force -Path $logs | Out-Null

# Le mode fenêtré forcé écrase les réglages d'écran du jeu : on les sauvegarde une fois
$prefsKey = "HKCU:\Software\Straight Back Games\DEVOUR"
$backup = Join-Path $logs "screen-backup.clixml"
if ((Test-Path $prefsKey) -and -not (Test-Path $backup)) {
    $item = Get-Item $prefsKey
    $saved = @{}
    foreach ($n in $item.GetValueNames()) { if ($n -like "Screenmanager*") { $saved[$n] = $item.GetValue($n) } }
    $saved | Export-Clixml $backup
}

Add-Type -AssemblyName System.Windows.Forms
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$cols = [math]::Ceiling([math]::Sqrt($Count))
$rows = [math]::Ceiling($Count / $cols)
$cellW = [math]::Floor($area.Width / $cols)
$cellH = [math]::Floor($area.Height / $rows)
if ($Width -le 0)  { $Width  = [math]::Max(480, $cellW - 16) }
if ($Height -le 0) { $Height = [math]::Max(270, $cellH - 39) }

Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool SetWindowText(IntPtr h, string t);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
  // La fenêtre principale du process est la console MelonLoader : on cherche la fenêtre Unity
  public static IntPtr FindUnityWindow(int pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p != (uint)pid || !IsWindowVisible(h)) return true;
      var sb = new System.Text.StringBuilder(64); GetClassName(h, sb, 64);
      if (sb.ToString() == "UnityWndClass") { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@

for ($i = 1; $i -le $Count; $i++) {
    $logFile = Join-Path $logs "unity-$i.log"
    $gameArgs = @("-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height",
                  "-logFile", "`"$logFile`"", "--devour-instance=$i")
    if ($Sound) { $gameArgs += "--devour-sound" }

    Write-Host "Lancement instance #$i ..."
    $p = Start-Process -FilePath $exe -WorkingDirectory $GamePath -ArgumentList $gameArgs -PassThru

    if (-not $NoTile) {
        $deadline = (Get-Date).AddSeconds(120)
        $hwnd = [IntPtr]::Zero
        while ((Get-Date) -lt $deadline) {
            $p.Refresh()
            if ($p.HasExited) { Write-Host "  l'instance #$i s'est fermée (voir $logFile)" -ForegroundColor Yellow; break }
            $hwnd = [Win]::FindUnityWindow($p.Id)
            if ($hwnd -ne [IntPtr]::Zero) { break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $p.HasExited -and $hwnd -ne [IntPtr]::Zero) {
            $c = ($i - 1) % $cols; $r = [math]::Floor(($i - 1) / $cols)
            Start-Sleep -Seconds 2
            [Win]::MoveWindow($hwnd, $area.X + $c * $cellW, $area.Y + $r * $cellH, $cellW, $cellH, $true) | Out-Null
            [Win]::SetWindowText($hwnd, "DEVOUR #$i" + $(if ($i -eq 1) { " (HOTE)" } else { "" })) | Out-Null
        }
    }
    if ($i -lt $Count) { Start-Sleep -Seconds $DelaySeconds }
}

Write-Host ""
Write-Host "$Count instance(s) lancée(s). Logs Unity : $logs" -ForegroundColor Green
