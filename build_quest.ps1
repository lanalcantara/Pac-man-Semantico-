param()

$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
$projectPath = "C:\Users\testcb\Pac-man-Semantico-Privado"
$logPath = "C:\Users\testcb\Pac-man-Semantico-Privado\Logs\build_standalone.log"

if (Test-Path $logPath) {
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Iniciando compilação Unity em batchmode..."
$process = Start-Process -FilePath $unityPath -ArgumentList "-batchmode", "-quit", "-projectPath", "`"$projectPath`"", "-executeMethod", "BuildQuest3.BuildMetaQuest3APK", "-logFile", "`"$logPath`"" -PassThru

Write-Host "Processo Unity iniciado com PID: $($process.Id)"

