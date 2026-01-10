param(
    [int]$HsmPort = 9000,
    [int]$DataPort = 9100,
    [int]$ServerPort = 9200,
    [string]$ListenIp = "0.0.0.0"
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root ".run-logs"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
"" | Set-Content (Join-Path $logDir "hsm.log")
"" | Set-Content (Join-Path $logDir "datastore.log")
"" | Set-Content (Join-Path $logDir "server.log")
"" | Set-Content (Join-Path $logDir "client.log")

$hsmCmd = "Set-Location `"$root`"; dotnet run --project HsmEmulator\HsmEmulator.csproj -- --port $HsmPort 2>&1 | Tee-Object -FilePath `"$logDir\hsm.log`" -Append"
$dataCmd = "Set-Location `"$root`"; dotnet run --project DataStore\DataStore.csproj -- --port $DataPort 2>&1 | Tee-Object -FilePath `"$logDir\datastore.log`" -Append"
$serverCmd = "Set-Location `"$root`"; dotnet run --project Server\Server.csproj -- --listenPort $ServerPort --listenIp $ListenIp --hsmPort $HsmPort --dataPort $DataPort 2>&1 | Tee-Object -FilePath `"$logDir\server.log`" -Append"
$clientCmd = "Set-Location `"$root`"; dotnet run --project Client\Client.csproj -- --host 127.0.0.1 --port $ServerPort 2>&1 | Tee-Object -FilePath `"$logDir\client.log`" -Append"

Start-Process powershell -ArgumentList "-NoExit", "-Command", $hsmCmd
Start-Process powershell -ArgumentList "-NoExit", "-Command", $dataCmd
Start-Process powershell -ArgumentList "-NoExit", "-Command", $serverCmd
Start-Process powershell -ArgumentList "-NoExit", "-Command", $clientCmd
