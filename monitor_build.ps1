param([int]$pidToWait = 31968)

$logPath = "C:\Users\testcb\Pac-man-Semantico-Privado\Logs\build_standalone.log"
$apkPath = "C:\Users\testcb\Desktop\PacManSemantico_Quest3.apk"

Write-Host "Monitorando processo $pidToWait e log $logPath..."

for ($i = 0; $i -lt 120; $i++) {
    $proc = Get-Process -Id $pidToWait -ErrorAction SilentlyContinue
    if (-not $proc) {
        Write-Host "Processo Unity finalizou!"
        break
    }
    
    if (Test-Path $logPath) {
        $lastLines = Get-Content $logPath -Tail 5 -ErrorAction SilentlyContinue
        Write-Host "[$i s] Unity ativo... CPU: $($proc.CPU)s | Ultimas linhas:"
        $lastLines | ForEach-Object { Write-Host "   $_" }
    } else {
        Write-Host "[$i s] Aguardando criação do arquivo de log..."
    }
    
    Start-Sleep -Seconds 5
}

if (Test-Path $apkPath) {
    $apkItem = Get-Item $apkPath
    Write-Host "STATUS DO APK FINAL: $($apkItem.FullName)"
    Write-Host "Data/Hora: $($apkItem.LastWriteTime) | Tamanho: $([math]::Round($apkItem.Length / 1MB, 2)) MB"
}

if (Test-Path $logPath) {
    Write-Host "--- ÚLTIMAS 30 LINHAS DO LOG ---"
    Get-Content $logPath -Tail 30
}

