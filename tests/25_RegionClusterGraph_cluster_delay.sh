#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/RegionClusterGraph_cluster_delay_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: переход между кластерами должен быть позже локального роста"
echo "============================================================"
echo ""

START_CLUSTER="region-0-0"
INITIAL_INSIDE_COUNT=3

CREATE_JSON="$OUT_DIR/create.json"

curl -s -X POST "$API_URL/api/Simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "RegionClusterGraph Cluster Delay",
    "description": "Local growth should happen before cross-cluster spread",
    "gridWidth": 12,
    "gridHeight": 12,
    "graphType": 2,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 10.0,
    "initialFireCellsCount": 3,
    "initialFirePositions": [
      { "x": 1, "y": 1 },
      { "x": 1, "y": 2 },
      { "x": 2, "y": 1 }
    ],
    "simulationSteps": 20,
    "stepDurationSeconds": 1800,
    "randomSeed": 424242,
    "temperature": 32,
    "humidity": 35,
    "windSpeed": 7,
    "windDirection": 45,
    "precipitation": 0
  }' > "$CREATE_JSON"

SIM_ID=$(jq -r '.id' "$CREATE_JSON")

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$CREATE_JSON"
  exit 1
fi

START_JSON="$OUT_DIR/start.json"
curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$START_JSON"

START_SUCCESS=$(jq -r '.success // false' "$START_JSON")
if [[ "$START_SUCCESS" != "true" ]]; then
  echo "❌ Не удалось запустить симуляцию"
  cat "$START_JSON"
  exit 1
fi

echo "Стартовый кластер: $START_CLUSTER"
echo "Начальный локальный очаг: $INITIAL_INSIDE_COUNT узла(ов)"
echo ""

FIRST_LOCAL_GROWTH=0
FIRST_CROSS_CLUSTER=0

for step in $(seq 1 15); do
    STEP_JSON="$OUT_DIR/step_${step}.json"
    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

    GRAPH_JSON="$OUT_DIR/graph_${step}.json"
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

    SUCCESS=$(jq -r '.success // false' "$GRAPH_JSON")
    if [[ "$SUCCESS" != "true" ]]; then
        echo "❌ Не удалось получить граф на шаге $step"
        cat "$GRAPH_JSON"
        exit 1
    fi

    read -r INSIDE OUTSIDE <<< "$(python3 - "$GRAPH_JSON" "$START_CLUSTER" <<'PY'
import json
import sys

path = sys.argv[1]
start_cluster = sys.argv[2]

with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]

inside = 0
outside = 0

for n in nodes:
    if n["state"] not in ("Burning", "Burned"):
        continue

    if n["groupKey"] == start_cluster:
        inside += 1
    else:
        outside += 1

print(inside, outside)
PY
)"

    echo "step=$step | inside=$INSIDE | outside=$OUTSIDE"

    if [[ "$FIRST_LOCAL_GROWTH" -eq 0 && "$INSIDE" -gt "$INITIAL_INSIDE_COUNT" ]]; then
        FIRST_LOCAL_GROWTH="$step"
    fi

    if [[ "$FIRST_CROSS_CLUSTER" -eq 0 && "$OUTSIDE" -gt 0 ]]; then
        FIRST_CROSS_CLUSTER="$step"
    fi
done

echo ""
echo "first local growth step   = $FIRST_LOCAL_GROWTH"
echo "first cross cluster step  = $FIRST_CROSS_CLUSTER"
echo ""

if [[ "$FIRST_LOCAL_GROWTH" -eq 0 ]]; then
    echo "❌ Не удалось зафиксировать локальный рост в стартовом кластере"
    exit 1
fi

if [[ "$FIRST_CROSS_CLUSTER" -eq 0 ]]; then
    echo "⚠️ За 15 шагов переход в другой кластер не произошёл"
    echo "   Это не обязательно ошибка: тест всё равно подтверждает, что сначала был локальный рост."
    echo "✅ Локальный рост зафиксирован, межкластерный переход пока не наблюдался"
    echo "📁 Логи: $OUT_DIR"
    echo "============================================================"
    exit 0
fi

if [[ "$FIRST_CROSS_CLUSTER" -le "$FIRST_LOCAL_GROWTH" ]]; then
    echo "❌ Переход между кластерами произошёл слишком рано"
    exit 1
fi

echo "✅ Сначала наблюдается локальный рост, потом переход между кластерами"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"