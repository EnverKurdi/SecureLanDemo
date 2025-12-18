#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

HSM_PORT="${HSM_PORT:-9000}"
DATA_PORT="${DATA_PORT:-9100}"
SERVER_PORT="${SERVER_PORT:-9200}"
LOG_DIR="${LOG_DIR:-"$ROOT/.run-logs"}"

mkdir -p "$LOG_DIR"

echo "[RUN] Logs -> $LOG_DIR"
echo "[RUN] Ports: HSM=$HSM_PORT DATA=$DATA_PORT SERVER=$SERVER_PORT"

dotnet run --project "$ROOT/HsmEmulator/HsmEmulator.csproj" -- --port "$HSM_PORT" >"$LOG_DIR/hsm.log" 2>&1 &
HSM_PID=$!

dotnet run --project "$ROOT/DataStore/DataStore.csproj" -- --port "$DATA_PORT" >"$LOG_DIR/datastore.log" 2>&1 &
DATA_PID=$!

dotnet run --project "$ROOT/Server/Server.csproj" -- --listenPort "$SERVER_PORT" --hsmPort "$HSM_PORT" --dataPort "$DATA_PORT" >"$LOG_DIR/server.log" 2>&1 &
SERVER_PID=$!

cleanup() {
  echo "[RUN] Stopping..."
  kill "$SERVER_PID" "$DATA_PID" "$HSM_PID" 2>/dev/null || true
}
trap cleanup EXIT

sleep 2
dotnet run --project "$ROOT/Client/Client.csproj" -- --port "$SERVER_PORT"
