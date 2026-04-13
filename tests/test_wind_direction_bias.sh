#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

RIGHT_SIM_JSON="$TMP_DIR/right_sim.json"
LEFT_SIM_JSON="$TMP_DIR/left_sim.json"

RIGHT_START_JSON="$TMP_DIR/right_start.json"
LEFT_START_JSON="$TMP_DIR/left_start.json"

RIGHT_STEP_JSON="$TMP_DIR/right_step.json"
LEFT_STEP_JSON="$TMP_DIR/left_step.json"

RIGHT_CELLS_JSON="$TMP_DIR/right_cells.json"
LEFT_CELLS_JSON="$TMP_DIR/left_cells.json"

echo "============================================================"
echo " ТЕСТ 11.6: направление ветра должно смещать распространение"
echo "============================================================"

create_sim () {
  local name="$1"
  local wind_direction="$2"
  local out_file="$3"

  curl -sS -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"wind direction bias test\",
      \"gridWidth\": 25,
      \"gridHeight\": 25,
      \"graphType\": 0,
      \"initialMoistureMin\": 0.30,
      \"initialMoistureMax\": 0.30,
      \"elevationVariation\": 0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 10,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 424242,
      \"temperature\": 25,
      \"humidity\": 35,
      \"windSpeed\": 12,
      \"windDirection\": $wind_direction,
      \"precipitation\": 0
    }" > "$out_file"
}

create_sim "test-wind-bias-right" 270 "$RIGHT_SIM_JSON"
create_sim "test-wind-bias-left" 90 "$LEFT_SIM_JSON"

RIGHT_SIM_ID="$(jq -r '.id' "$RIGHT_SIM_JSON")"
LEFT_SIM_ID="$(jq -r '.id' "$LEFT_SIM_JSON")"

if [[ -z "$RIGHT_SIM_ID" || "$RIGHT_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать right simulation"
  cat "$RIGHT_SIM_JSON"
  exit 1
fi

if [[ -z "$LEFT_SIM_ID" || "$LEFT_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать left simulation"
  cat "$LEFT_SIM_JSON"
  exit 1
fi

echo "right_sim_id = $RIGHT_SIM_ID"
echo "left_sim_id  = $LEFT_SIM_ID"

MANUAL_START='{
  "ignitionMode": "manual",
  "initialFirePositions": [
    { "x": 12, "y": 12 }
  ]
}'

curl -sS -X POST "$BASE_URL/api/SimulationManager/$RIGHT_SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "$MANUAL_START" > "$RIGHT_START_JSON"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$LEFT_SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "$MANUAL_START" > "$LEFT_START_JSON"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$RIGHT_SIM_ID/step" > "$RIGHT_STEP_JSON"
curl -sS -X POST "$BASE_URL/api/SimulationManager/$LEFT_SIM_ID/step" > "$LEFT_STEP_JSON"

curl -sS "$BASE_URL/api/SimulationManager/$RIGHT_SIM_ID/cells" > "$RIGHT_CELLS_JSON"
curl -sS "$BASE_URL/api/SimulationManager/$LEFT_SIM_ID/cells" > "$LEFT_CELLS_JSON"

python3 - "$RIGHT_CELLS_JSON" "$LEFT_CELLS_JSON" <<'PY'
import json
import sys

right_path, left_path = sys.argv[1], sys.argv[2]

with open(right_path, "r", encoding="utf-8") as f:
    right_data = json.load(f)

with open(left_path, "r", encoding="utf-8") as f:
    left_data = json.load(f)

right_cells = right_data["cells"]
left_cells = left_data["cells"]

def cell_map(cells):
    return {(c["x"], c["y"]): c for c in cells}

right_map = cell_map(right_cells)
left_map = cell_map(left_cells)

source = (12, 12)

right_side = [(13, 12), (14, 12), (13, 11), (13, 13)]
left_side  = [(11, 12), (10, 12), (11, 11), (11, 13)]

def avg_prob(cmap, coords):
    vals = []
    for xy in coords:
        c = cmap.get(xy)
        if c is not None:
            vals.append(c.get("burnProbability", 0.0))
    return sum(vals) / len(vals) if vals else 0.0

def affected_count(cmap, coords):
    cnt = 0
    for xy in coords:
        c = cmap.get(xy)
        if c is None:
            continue
        if c.get("state") != "Normal" or c.get("burnProbability", 0.0) > 0.0:
            cnt += 1
    return cnt

right_wind_right_avg = avg_prob(right_map, right_side)
right_wind_left_avg  = avg_prob(right_map, left_side)

left_wind_right_avg = avg_prob(left_map, right_side)
left_wind_left_avg  = avg_prob(left_map, left_side)

right_wind_right_affected = affected_count(right_map, right_side)
right_wind_left_affected  = affected_count(right_map, left_side)

left_wind_right_affected = affected_count(left_map, right_side)
left_wind_left_affected  = affected_count(left_map, left_side)

print(f"source = {source}")
print(f"right_wind_right_avg_prob = {right_wind_right_avg:.6f}")
print(f"right_wind_left_avg_prob  = {right_wind_left_avg:.6f}")
print(f"left_wind_right_avg_prob  = {left_wind_right_avg:.6f}")
print(f"left_wind_left_avg_prob   = {left_wind_left_avg:.6f}")

print(f"right_wind_right_affected = {right_wind_right_affected}")
print(f"right_wind_left_affected  = {right_wind_left_affected}")
print(f"left_wind_right_affected  = {left_wind_right_affected}")
print(f"left_wind_left_affected   = {left_wind_left_affected}")

cond1 = right_wind_right_avg > right_wind_left_avg
cond2 = left_wind_left_avg > left_wind_right_avg

if not cond1:
    print("❌ При ветре вправо правая сторона не получила больший burnProbability")
    sys.exit(1)

if not cond2:
    print("❌ При ветре влево левая сторона не получила больший burnProbability")
    sys.exit(1)

print("✅ Направление ветра реально смещает распространение в нужную сторону")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.6 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"
