#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_RegionClusterGraph_cluster_identity_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: RegionClusterGraph должен выглядеть как кластерная структура"
echo "============================================================"
echo ""

CREATE_JSON="$OUT_DIR/create.json"

curl -s -X POST "$API_URL/api/Simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "RegionClusterGraph cluster identity",
    "description": "RegionClusterGraph cluster identity test",
    "gridWidth": 20,
    "gridHeight": 20,
    "graphType": 2,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 20.0,
    "initialFireCellsCount": 1,
    "simulationSteps": 15,
    "stepDurationSeconds": 1800,
    "randomSeed": 424242,
    "temperature": 32,
    "humidity": 35,
    "windSpeed": 8,
    "windDirection": 45,
    "precipitation": 0
  }' > "$CREATE_JSON"

SIM_ID=$(jq -r '.id' "$CREATE_JSON")
[[ -z "$SIM_ID" || "$SIM_ID" == "null" ]] && echo "❌ Ошибка создания" && cat "$CREATE_JSON" && exit 1

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start.json"
curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$OUT_DIR/graph.json"

SUCCESS=$(jq -r '.success // false' "$OUT_DIR/graph.json")
if [[ "$SUCCESS" != "true" ]]; then
    echo "❌ Не удалось получить граф"
    cat "$OUT_DIR/graph.json"
    exit 1
fi

GROUP_COUNT=$(jq -r '.graph.nodes | map(.groupKey) | unique | length' "$OUT_DIR/graph.json")
NODE_COUNT=$(jq -r '.graph.nodes | length' "$OUT_DIR/graph.json")
EDGE_COUNT=$(jq -r '.graph.edges | length' "$OUT_DIR/graph.json")

echo "groupCount=$GROUP_COUNT"
echo "nodeCount=$NODE_COUNT"
echo "edgeCount=$EDGE_COUNT"

if [[ "$GROUP_COUNT" -lt 4 ]]; then
    echo "❌ Для RegionClusterGraph слишком мало кластеров"
    exit 1
fi

FIRST_AREA=0
LAST_AREA=0
MAX_AREA=0
FIRST_SPREAD_STEP=0
HAD_GROWTH=0
HAD_BURNOUT=0

for step in $(seq 1 10); do
    STEP_JSON="$OUT_DIR/step_${step}.json"
    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

    SUCCESS=$(jq -r '.success // false' "$STEP_JSON")
    if [[ "$SUCCESS" != "true" ]]; then
        echo "❌ Ошибка шага $step"
        cat "$STEP_JSON"
        exit 1
    fi

    AREA=$(jq -r '.step.fireArea // 0' "$STEP_JSON")
    BURNING=$(jq -r '.step.burningCellsCount // 0' "$STEP_JSON")
    BURNED=$(jq -r '.step.burnedCellsCount // 0' "$STEP_JSON")

    [[ "$step" -eq 1 ]] && FIRST_AREA=$AREA
    LAST_AREA=$AREA

    if (( $(echo "$AREA > $MAX_AREA" | bc -l) )); then
        MAX_AREA=$AREA
    fi

    if (( $(echo "$AREA > $FIRST_AREA" | bc -l) )); then
        HAD_GROWTH=1
        if [[ "$FIRST_SPREAD_STEP" -eq 0 ]]; then
            FIRST_SPREAD_STEP=$step
        fi
    fi

    if [[ "$BURNED" -gt 0 ]]; then
        HAD_BURNOUT=1
    fi

    echo "step=$step | area=$AREA | burning=$BURNING | burned=$BURNED"
done

echo ""
echo "Итог:"
echo "firstArea=$FIRST_AREA"
echo "lastArea=$LAST_AREA"
echo "maxArea=$MAX_AREA"
echo "firstSpreadStep=$FIRST_SPREAD_STEP"
echo "hadGrowth=$HAD_GROWTH"
echo "hadBurnout=$HAD_BURNOUT"

if [[ "$HAD_GROWTH" -ne 1 ]]; then
    echo "❌ RegionClusterGraph не распространяется"
    exit 1
fi

if [[ "$HAD_BURNOUT" -ne 1 ]]; then
    echo "❌ RegionClusterGraph не показывает выгорание"
    exit 1
fi

if (( $(echo "$MAX_AREA < 8" | bc -l) )); then
    echo "❌ RegionClusterGraph растёт слишком слабо"
    exit 1
fi

if (( $(echo "$MAX_AREA > 150" | bc -l) )); then
    echo "❌ RegionClusterGraph растёт слишком взрывно"
    exit 1
fi

echo "✅ RegionClusterGraph выглядит как живая кластерная структура"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"