# Remet les réglages d'écran de DEVOUR (plein écran, résolution) sauvegardés par Lancer-Multi.ps1.

$backup = Join-Path ([IO.Path]::GetTempPath()) "DevourMultiTest\screen-backup.clixml"
$prefsKey = "HKCU:\Software\Straight Back Games\DEVOUR"

if (-not (Test-Path $backup)) { Write-Host "Aucune sauvegarde trouvée ($backup)." -ForegroundColor Yellow; exit 1 }
if (Get-Process -Name DEVOUR -ErrorAction SilentlyContinue) { Write-Host "Ferme DEVOUR avant de restaurer." -ForegroundColor Red; exit 1 }

$saved = Import-Clixml $backup
foreach ($name in $saved.Keys) {
    Set-ItemProperty -Path $prefsKey -Name $name -Value $saved[$name]
}
Remove-Item $backup
Write-Host "Réglages d'écran restaurés." -ForegroundColor Green
