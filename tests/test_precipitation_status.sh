#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
STATUS_BEFORE_JSON="$TMP_DIR/status_before.json"
START_JSON="$TMP_DIR/start.json"
STATUS_AFTER_JSON="$TMP_DIR/status_after.json"

EXPECTED_PRECIPITATION="${EXPECTED_PRECIPITATION:-17.5}"

cleanup() {
  if [[ -n "${SIM_ID:-}" && "${SIM_ID}" != "null" ]]; then
    curl -sS -X DELETE "$BASE_URL/api/SimulationManager/$SIM_ID" >/dev/null || true
  fi
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

echo "============================================================"
echo " ТЕСТ 9.1: precipitation должен проходить create -> DB -> status"
echo "============================================================"

create_payload="$(cat <<JSON
{
  "name": "test-precipitation-status",
  "description": "precipitation propagation test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 0,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 900,
  "randomSeed": 123456,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45,
  "precipitation": $EXPECTED_PRECIPITATION
}
JSON
)"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$create_payload" > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_BEFORE_JSON"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d '{"ignitionMode":"saved-or-random"}' > "$START_JSON"

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_AFTER_JSON"

python3 - "$STATUS_BEFORE_JSON" "$STATUS_AFTER_JSON" "$EXPECTED_PRECIPITATION" << 'PY'
import json
import math
import sys

status_before_path, status_after_path, expected_raw = sys.argv[1], sys.argv[2], sys.argv[3]
expected = float(expected_raw)

def read_status(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    if not data.get("success", False):
        print(f"❌ success=false in {path}")
        sys.exit(1)

    sim = data.get("simulation") or {}
    if "precipitation" not in sim:
        print(f"❌ precipitation отсутствует в simulation status: {path}")
        sys.exit(1)

    return sim

before = read_status(status_before_path)
after = read_status(status_after_path)

before_prec = float(before["precipitation"])
after_prec = float(after["precipitation"])

print(f"expected_precipitation = {expected}")
print(f"before_start_precipitation = {before_prec}")
print(f"after_start_precipitation  = {after_prec}")
print(f"before_status = {before.get('status')}, after_status = {after.get('status')}")

if not math.isclose(before_prec, expected, rel_tol=0.0, abs_tol=1e-9):
    print("❌ До старта precipitation не совпадает с ожидаемым")
    sys.exit(1)

if not math.isclose(after_prec, expected, rel_tol=0.0, abs_tol=1e-9):
    print("❌ После старта precipitation не совпадает с ожидаемым")
    sys.exit(1)

print("✅ precipitation корректно проходит через create/status/start/status")
PY

echo "============================================================"
echo "✅ ТЕСТ 9.1 ПРОЙДЕН"
echo "============================================================"