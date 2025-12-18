#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

HSM_PORT="${HSM_PORT:-9000}"
DATA_PORT="${DATA_PORT:-9100}"
SERVER_PORT="${SERVER_PORT:-9200}"
LOG_DIR="${LOG_DIR:-"$ROOT/.run-logs"}"

mkdir -p "$LOG_DIR"
: > "$LOG_DIR/hsm.log"
: > "$LOG_DIR/datastore.log"
: > "$LOG_DIR/server.log"
: > "$LOG_DIR/client.log"

osascript - "$ROOT" "$HSM_PORT" "$DATA_PORT" "$SERVER_PORT" "$LOG_DIR" <<'APPLESCRIPT'
on run argv
  set root to item 1 of argv
  set hsmPort to item 2 of argv
  set dataPort to item 3 of argv
  set serverPort to item 4 of argv
  set logDir to item 5 of argv

  set hsmCmd to "cd " & quoted form of root & "; dotnet run --project HsmEmulator/HsmEmulator.csproj -- --port " & hsmPort & " 2>&1 | tee -a " & quoted form of (logDir & "/hsm.log")
  set dataCmd to "cd " & quoted form of root & "; dotnet run --project DataStore/DataStore.csproj -- --port " & dataPort & " 2>&1 | tee -a " & quoted form of (logDir & "/datastore.log")
  set serverCmd to "cd " & quoted form of root & "; dotnet run --project Server/Server.csproj -- --listenPort " & serverPort & " --hsmPort " & hsmPort & " --dataPort " & dataPort & " 2>&1 | tee -a " & quoted form of (logDir & "/server.log")
  set clientCmd to "cd " & quoted form of root & "; bash -lc " & quoted form of ("until (echo > /dev/tcp/127.0.0.1/" & serverPort & ") >/dev/null 2>&1; do sleep 0.5; done; dotnet run --project Client/Client.csproj -- --port " & serverPort & " 2>&1 | tee -a " & quoted form of (logDir & "/client.log"))
  set followCmd to "bash -lc " & quoted form of ("tail -n 0 -F " & quoted form of (logDir & "/hsm.log") & " | awk '{if ($0 ~ /^\\[/) print; else print \"[HSM] \" $0}' & " & "tail -n 0 -F " & quoted form of (logDir & "/datastore.log") & " | awk '{if ($0 ~ /^\\[/) print; else print \"[DATASTORE] \" $0}' & " & "tail -n 0 -F " & quoted form of (logDir & "/server.log") & " | awk '{if ($0 ~ /^\\[/) print; else print \"[SERVER] \" $0}' & " & "tail -n 0 -F " & quoted form of (logDir & "/client.log") & " | sed -u -e '/^1) List files$/d' -e '/^2) Upload file$/d' -e '/^3) Upload file from disk$/d' -e '/^4) Download file to disk$/d' -e '/^5) Exit$/d' -e '/^> /d' -e '/^>$/d' -e '/^$/d' | awk '{if ($0 ~ /^\\[/) print; else print \"[CLIENT] \" $0}' & wait")

  tell application "iTerm2"
    activate
    set w to (create window with default profile)
    tell current session of w to write text hsmCmd
    tell w to create tab with default profile
    tell current session of w to write text dataCmd
    tell w to create tab with default profile
    tell current session of w to write text serverCmd
    tell w to create tab with default profile
    tell current session of w to write text clientCmd
    tell w to create tab with default profile
    tell current session of w to write text followCmd
  end tell
end run
APPLESCRIPT
