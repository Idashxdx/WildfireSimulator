#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

WATER_SIM_JSON="$TMP_DIR/water_sim.json"
BARE_SIM_JSON="$TMP_DIR/bare_sim.json"

WATER_START_JSON="$TMP_DIR/water_start.json"
BARE_START_JSON="$TMP_DIR/bare_start.json"

echo "============================================================"
echo " ТЕСТ 11.5: Water и Bare не должны загораться"
echo "============================================================"

create_simulation() {
  local name="$1"
  local vegetation_type="$2"
  local out_file="$3"

  local payload
  payload="$(cat <<JSON
{
  "name": "$name",
  "description": "non-ignitable vegetation test",
  "gridWidth": 12,
  "gridHeight": 12,
  "graphType": 0,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.30,
  "elevationVariation": 0,
  "initialFireCellsCount": 1,
  "simulationSteps": 5,
  "stepDurationSeconds": 900,
  "randomSeed": 12345,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45,
  "precipitation": 0,
  "vegetationDistributions": [
    { "vegetationType": $vegetation_type, "probability": 1.0 }
  ]
}
JSON
)"

  curl -sS -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "$payload" > "$out_file"
}

start_manual() {
  local sim_id="$1"
  local x="$2"
  local y="$3"
  local out_file="$4"

  local payload
  payload="$(cat <<JSON
{
  "ignitionMode": "manual",
  "initialFirePositions": [
    { "x": $x, "y": $y }
  ]
}
JSON
)"

  curl -sS -X POST "$BASE_URL/api/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d "$payload" > "$out_file"
}

extract_cell_state() {
  local json_file="$1"
  local x="$2"
  local y="$3"

  jq -r --argjson x "$x" --argjson y "$y" '
    (.cells // [])
    | map(select(.x == $x and .y == $y))
    | if length == 0 then "MISSING" else .[0].state end
  ' "$json_file"
}

extract_burning_count() {
  local json_file="$1"
  jq -r '
    if .activeSimulation != null and .activeSimulation.totalBurningCells != null then
      .activeSimulation.totalBurningCells
    elif .cells != null then
      ([.cells[] | select(.state == "Burning")] | length)
    else
      0
    end
  ' "$json_file"
}

CENTER_X=6
CENTER_Y=6

create_simulation "test-water-do-not-ignite" 5 "$WATER_SIM_JSON"
WATER_SIM_ID="$(jq -r '.id' "$WATER_SIM_JSON")"

if [[ -z "$WATER_SIM_ID" || "$WATER_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать water simulation"
  cat "$WATER_SIM_JSON"
  exit 1
fi

create_simulation "test-bare-do-not-ignite" 6 "$BARE_SIM_JSON"
BARE_SIM_ID="$(jq -r '.id' "$BARE_SIM_JSON")"

if [[ -z "$BARE_SIM_ID" || "$BARE_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать bare simulation"
  cat "$BARE_SIM_JSON"
  exit 1
fi

echo "water_sim_id = $WATER_SIM_ID"
echo "bare_sim_id  = $BARE_SIM_ID"

start_manual "$WATER_SIM_ID" "$CENTER_X" "$CENTER_Y" "$WATER_START_JSON"
start_manual "$BARE_SIM_ID" "$CENTER_X" "$CENTER_Y" "$BARE_START_JSON"

WATER_BURNING="$(extract_burning_count "$WATER_START_JSON")"
BARE_BURNING="$(extract_burning_count "$BARE_START_JSON")"

WATER_CENTER_STATE="$(extract_cell_state "$WATER_START_JSON" "$CENTER_X" "$CENTER_Y")"
BARE_CENTER_STATE="$(extract_cell_state "$BARE_START_JSON" "$CENTER_X" "$CENTER_Y")"

echo "water_burning_count = $WATER_BURNING"
echo "bare_burning_count  = $BARE_BURNING"
echo "water_center_state  = $WATER_CENTER_STATE"
echo "bare_center_state   = $BARE_CENTER_STATE"

if [[ "$WATER_BURNING" != "0" ]]; then
  echo "❌ Water simulation unexpectedly has burning cells"
  exit 1
fi

if [[ "$BARE_BURNING" != "0" ]]; then
  echo "❌ Bare simulation unexpectedly has burning cells"
  exit 1
fi

if [[ "$WATER_CENTER_STATE" == "Burning" ]]; then
  echo "❌ Water cell ignited unexpectedly"
  exit 1
fi

if [[ "$BARE_CENTER_STATE" == "Burning" ]]; then
  echo "❌ Bare cell ignited unexpectedly"
  exit 1
fi

echo "✅ Water и Bare корректно работают как негорючие барьеры"
echo "============================================================"
echo "✅ ТЕСТ 11.5 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"