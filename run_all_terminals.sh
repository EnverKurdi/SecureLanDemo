#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

HSM_PORT="${HSM_PORT:-9000}"
DATA_PORT="${DATA_PORT:-9100}"
SERVER_PORT="${SERVER_PORT:-9200}"

HSM_CMD="/bin/zsh -lc 'cd \"$ROOT\"; dotnet run --project HsmEmulator/HsmEmulator.csproj -- --port $HSM_PORT'"
DATA_CMD="/bin/zsh -lc 'cd \"$ROOT\"; dotnet run --project DataStore/DataStore.csproj -- --port $DATA_PORT'"
SERVER_CMD="/bin/zsh -lc 'cd \"$ROOT\"; dotnet run --project Server/Server.csproj -- --listenPort $SERVER_PORT --hsmPort $HSM_PORT --dataPort $DATA_PORT'"
CLIENT_CMD="/bin/zsh -lc 'cd \"$ROOT\"; dotnet run --project Client/Client.csproj -- --port $SERVER_PORT'"

osascript <<APPLESCRIPT
tell application "Terminal"
  do script "$HSM_CMD"
  do script "$DATA_CMD" in (make new window)
  do script "$SERVER_CMD" in (make new window)
  do script "$CLIENT_CMD" in (make new window)
  activate
end tell
APPLESCRIPT
