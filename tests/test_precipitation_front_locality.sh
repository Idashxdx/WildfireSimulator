#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: фронт осадков должен влиять локально"
echo "============================================================"
echo "BASE_URL=$BASE_URL"
echo "TMP_DIR=$TMP_DIR"

SIM_JSON="$TMP_DIR/simulation.json"
START_JSON="$TMP_DIR/start.json"
GRAPH_JSON="$TMP_DIR/graph.json"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "precipitation-front-locality-test",
    "description": "Проверка локального влияния движущегося фронта осадков",
    "gridWidth": 30,
    "gridHeight": 30,
    "graphType": 0,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 0,
    "initialFireCellsCount": 1,
    "simulationSteps": 25,
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

for step in $(seq 1 16); do
  curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/step" > "$TMP_DIR/step_$step.json"
done

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" <<'PY'
import json
import sys

path = sys.argv[1]

with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

nodes = root["graph"]["nodes"]

# Для windDirection=270 поток осадков идёт вправо.
# После нескольких шагов фронт/след должны сильнее затронуть левую/центральную часть,
# чем дальнюю правую сухую область.
rain_zone = []
dry_zone = []

for n in nodes:
    x = n["x"]
    y = n["y"]

    if 8 <= x <= 17 and 10 <= y <= 20:
        rain_zone.append(n)

    if 23 <= x <= 28 and 10 <= y <= 20:
        dry_zone.append(n)

def avg_moisture(items):
    return sum(float(n.get("moisture") or 0.0) for n in items) / max(1, len(items))

def avg_heat(items):
    return sum(float(n.get("accumulatedHeatJ") or 0.0) for n in items) / max(1, len(items))

rain_moisture = avg_moisture(rain_zone)
dry_moisture = avg_moisture(dry_zone)

rain_heat = avg_heat(rain_zone)
dry_heat = avg_heat(dry_zone)

print(f"rain_zone_count = {len(rain_zone)}")
print(f"dry_zone_count  = {len(dry_zone)}")
print(f"rain_zone_avg_moisture = {rain_moisture:.6f}")
print(f"dry_zone_avg_moisture  = {dry_moisture:.6f}")
print(f"rain_zone_avg_heat     = {rain_heat:.3f}")
print(f"dry_zone_avg_heat      = {dry_heat:.3f}")

if len(rain_zone) == 0 or len(dry_zone) == 0:
    print("❌ Не удалось выделить зоны для проверки")
    sys.exit(1)

if rain_moisture <= dry_moisture:
    print("❌ Зона осадков не получила большую влажность")
    sys.exit(1)

print("✅ Фронт осадков локально повышает влажность выбранной зоны")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $TMP_DIR"
echo "============================================================"