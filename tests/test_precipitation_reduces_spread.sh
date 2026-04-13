#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_DRY_JSON="$TMP_DIR/sim_dry.json"
SIM_WET_JSON="$TMP_DIR/sim_wet.json"

STEP_DRY_JSON="$TMP_DIR/step_dry.json"
STEP_WET_JSON="$TMP_DIR/step_wet.json"

STATUS_DRY_JSON="$TMP_DIR/status_dry.json"
STATUS_WET_JSON="$TMP_DIR/status_wet.json"

echo "============================================================"
echo " ТЕСТ 9.2: Осадки должны уменьшать распространение пожара"
echo "============================================================"

COMMON_PAYLOAD_PREFIX='{
  "description": "precipitation influence test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 0,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 20,
  "stepDurationSeconds": 900,
  "randomSeed": 424242,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45'

create_sim() {
  local name="$1"
  local precipitation="$2"
  local out_file="$3"

  curl -sS -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"precipitation influence test\",
      \"gridWidth\": 20,
      \"gridHeight\": 20,
      \"graphType\": 0,
      \"initialMoistureMin\": 0.30,
      \"initialMoistureMax\": 0.70,
      \"elevationVariation\": 50,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 20,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 424242,
      \"temperature\": 25,
      \"humidity\": 40,
      \"windSpeed\": 5,
      \"windDirection\": 45,
      \"precipitation\": $precipitation
    }" > "$out_file"
}

start_sim() {
  local sim_id="$1"
  curl -sS -X POST "$BASE_URL/api/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d '{"ignitionMode":"saved-or-random"}' > /dev/null
}

step_sim() {
  local sim_id="$1"
  local out_file="$2"
  curl -sS -X POST "$BASE_URL/api/SimulationManager/$sim_id/step" > "$out_file"
}

status_sim() {
  local sim_id="$1"
  local out_file="$2"
  curl -sS "$BASE_URL/api/SimulationManager/$sim_id/status" > "$out_file"
}

create_sim "test-precipitation-dry" 0 "$SIM_DRY_JSON"
create_sim "test-precipitation-wet" 25 "$SIM_WET_JSON"

SIM_DRY_ID="$(jq -r '.id' "$SIM_DRY_JSON")"
SIM_WET_ID="$(jq -r '.id' "$SIM_WET_JSON")"

if [[ -z "$SIM_DRY_ID" || "$SIM_DRY_ID" == "null" ]]; then
  echo "❌ Не удалось создать dry simulation"
  cat "$SIM_DRY_JSON"
  exit 1
fi

if [[ -z "$SIM_WET_ID" || "$SIM_WET_ID" == "null" ]]; then
  echo "❌ Не удалось создать wet simulation"
  cat "$SIM_WET_JSON"
  exit 1
fi

echo "dry_sim_id = $SIM_DRY_ID"
echo "wet_sim_id = $SIM_WET_ID"

start_sim "$SIM_DRY_ID"
start_sim "$SIM_WET_ID"

for i in 1 2 3 4 5; do
  step_sim "$SIM_DRY_ID" "$STEP_DRY_JSON"
  step_sim "$SIM_WET_ID" "$STEP_WET_JSON"
done

status_sim "$SIM_DRY_ID" "$STATUS_DRY_JSON"
status_sim "$SIM_WET_ID" "$STATUS_WET_JSON"

python3 - "$STATUS_DRY_JSON" "$STATUS_WET_JSON" << 'PY'
import json
import sys

dry_path, wet_path = sys.argv[1], sys.argv[2]

with open(dry_path, "r", encoding="utf-8") as f:
    dry = json.load(f)

with open(wet_path, "r", encoding="utf-8") as f:
    wet = json.load(f)

dry_sim = dry["simulation"]
wet_sim = wet["simulation"]

dry_area = dry_sim["fireArea"]
wet_area = wet_sim["fireArea"]

dry_burned = dry_sim["totalBurnedCells"]
wet_burned = wet_sim["totalBurnedCells"]

dry_burning = dry_sim["totalBurningCells"]
wet_burning = wet_sim["totalBurningCells"]

dry_total = dry_burned + dry_burning
wet_total = wet_burned + wet_burning

print(f"dry_area = {dry_area}")
print(f"wet_area = {wet_area}")
print(f"dry_total_affected = {dry_total}")
print(f"wet_total_affected = {wet_total}")
print(f"dry_burning = {dry_burning}")
print(f"wet_burning = {wet_burning}")
print(f"dry_burned = {dry_burned}")
print(f"wet_burned = {wet_burned}")

if wet_area > dry_area:
    print("❌ При сильных осадках площадь пожара оказалась больше, чем без осадков")
    sys.exit(1)

if wet_total > dry_total:
    print("❌ При сильных осадках поражённых клеток оказалось больше, чем без осадков")
    sys.exit(1)

if wet_area == dry_area and wet_total == dry_total:
    print("⚠️ Разницы не видно. Возможно, влияние осадков слишком слабое для выбранных параметров.")
    sys.exit(2)

print("✅ Осадки реально уменьшают распространение пожара")
PY

PY_EXIT=$?

if [[ "$PY_EXIT" -eq 2 ]]; then
  echo "============================================================"
  echo "⚠️ ТЕСТ 9.2 ПОГРАНИЧНЫЙ: влияние осадков не проявилось достаточно явно"
  echo "Временные файлы: $TMP_DIR"
  echo "============================================================"
  exit 2
fi

echo "============================================================"
echo "✅ ТЕСТ 9.2 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"