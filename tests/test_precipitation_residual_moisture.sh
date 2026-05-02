#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: после прохода фронта осадков остаётся влажный след"
echo "============================================================"
echo "BASE_URL=$BASE_URL"
echo "TMP_DIR=$TMP_DIR"

SIM_JSON="$TMP_DIR/simulation.json"
START_JSON="$TMP_DIR/start.json"
GRAPH_EARLY_JSON="$TMP_DIR/graph_early.json"
GRAPH_LATE_JSON="$TMP_DIR/graph_late.json"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "precipitation-residual-moisture-test",
    "description": "Проверка остаточной влажности после прохода фронта осадков",
    "gridWidth": 30,
    "gridHeight": 30,
    "graphType": 0,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 0,
    "initialFireCellsCount": 1,
    "simulationSteps": 35,
    "stepDurationSeconds": 900,
    "randomSeed": 424242,
    "temperature": 30,
    "humidity": 30,
    "windSpeed": 8,
    "windDirection": 270,
    "precipitation": 100
  }' > "$SIM_JSON"

SIM_ID="$(jq -r '.id // empty' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d '{
    "ignitionMode": "manual",
    "initialFirePositions": [
      { "x": 5, "y": 15 }
    ]
  }' > "$START_JSON"

START_SUCCESS="$(jq -r '.success // false' "$START_JSON")"

if [[ "$START_SUCCESS" != "true" ]]; then
  echo "❌ Не удалось запустить симуляцию"
  cat "$START_JSON"
  exit 1
fi

for step in $(seq 1 10); do
  curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/step" > "$TMP_DIR/step_${step}.json"
done

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_EARLY_JSON"

for step in $(seq 11 24); do
  curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/step" > "$TMP_DIR/step_${step}.json"
done

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_LATE_JSON"

python3 - "$GRAPH_EARLY_JSON" "$GRAPH_LATE_JSON" <<'PY'
import json
import sys

early_path, late_path = sys.argv[1:3]

def load_nodes(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)["graph"]["nodes"]

def avg_moisture(nodes, x_min, x_max, y_min, y_max):
    zone = [
        n for n in nodes
        if x_min <= n["x"] <= x_max and y_min <= n["y"] <= y_max
    ]

    if not zone:
        return 0.0, 0

    return sum(float(n.get("moisture") or 0.0) for n in zone) / len(zone), len(zone)

early_nodes = load_nodes(early_path)
late_nodes = load_nodes(late_path)

# Эта зона должна быть уже пройдена фронтом к позднему снимку.
trail_early, trail_count = avg_moisture(early_nodes, 8, 14, 10, 20)
trail_late, _ = avg_moisture(late_nodes, 8, 14, 10, 20)

# Более дальняя зона используется как контроль: она не должна быть настолько же влажной,
# если эффект локальный и фронт не сделал всю карту одинаково мокрой.
control_late, control_count = avg_moisture(late_nodes, 24, 28, 10, 20)

print(f"trail_zone_count       = {trail_count}")
print(f"control_zone_count     = {control_count}")
print(f"trail_early_moisture   = {trail_early:.6f}")
print(f"trail_late_moisture    = {trail_late:.6f}")
print(f"control_late_moisture  = {control_late:.6f}")

if trail_count == 0 or control_count == 0:
    print("❌ Не удалось выделить зоны для проверки")
    sys.exit(1)

if trail_late <= trail_early + 0.05:
    print("❌ После прохода фронта влажность в следе почти не выросла")
    sys.exit(1)

if trail_late <= 0.45:
    print("❌ Остаточная влажность слишком низкая")
    sys.exit(1)

if trail_late <= control_late:
    print("❌ Влажный след не отличается от контрольной зоны")
    sys.exit(1)

print("✅ После прохода фронта остаётся повышенная влажность")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $TMP_DIR"
echo "============================================================"