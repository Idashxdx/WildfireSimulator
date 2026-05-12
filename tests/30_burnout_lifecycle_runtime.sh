#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_burnout_lifecycle_runtime_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: runtime выгорание клетки"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
START_JSON="$OUT_DIR/start.json"
GRAPH_JSON="$OUT_DIR/graph.json"

curl -sS -X POST "$API_URL/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "runtime-burnout-grass",
    "description": "Проверка выгорания травяной клетки",
    "graphType": 0,
    "gridWidth": 9,
    "gridHeight": 9,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 0,
    "initialFireCellsCount": 1,
    "simulationSteps": 10,
    "stepDurationSeconds": 900,
    "randomSeed": 12345,
    "temperature": 25,
    "humidity": 40,
    "windSpeed": 0,
    "windDirection": 0,
    "precipitation": 0,
    "vegetationDistributions": [
      { "vegetationType": 0, "probability": 1.0 }
    ]
  }' > "$CREATE_JSON"

SIM_ID="$(get_sim_id "$CREATE_JSON")"

start_manual "$SIM_ID" "$START_JSON" 4 4
run_steps "$SIM_ID" 8 "$OUT_DIR" "burnout" >/dev/null
fetch_graph "$SIM_ID" "$GRAPH_JSON"

python3 - "$GRAPH_JSON" <<'PY'
import json
import sys

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    nodes = json.load(f)["graph"]["nodes"]

center = next((n for n in nodes if n["x"] == 4 and n["y"] == 4), None)

if center is None:
    print("❌ Центральная клетка не найдена")
    sys.exit(1)

print("center_state =", center["state"])
print("center_stage =", center["fireStage"])
print("center_elapsed =", center["burningElapsedSeconds"])
print("center_fuel =", center["currentFuelLoad"], "/", center["fuelLoad"])

burned = [n for n in nodes if n["state"] == "Burned"]
burning = [n for n in nodes if n["state"] == "Burning"]

print("burned_count =", len(burned))
print("burning_count =", len(burning))

if center["state"] != "Burned":
    print("❌ Стартовая grass-клетка должна выгореть за 2 часа")
    sys.exit(1)

if len(burned) < 1:
    print("❌ В runtime нет выгоревших клеток")
    sys.exit(1)

print("✅ Runtime lifecycle: клетка загорается, горит и выгорает")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"