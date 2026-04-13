#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_ClusteredGraph_nonjump_connectivity_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: ClusteredGraph должен распространяться без wind jump"
echo "============================================================"
echo ""

CREATE_JSON="$OUT_DIR/create.json"

curl -s -X POST "$API_URL/api/Simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ClusteredGraph nonjump connectivity",
    "description": "ClusteredGraph should spread via graph connectivity, not only wind jumps",
    "gridWidth": 20,
    "gridHeight": 20,
    "graphType": 1,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 20.0,
    "initialFireCellsCount": 1,
    "simulationSteps": 15,
    "stepDurationSeconds": 1800,
    "randomSeed": 424242,
    "temperature": 32,
    "humidity": 35,
    "windSpeed": 3,
    "windDirection": 45,
    "precipitation": 0
  }' > "$CREATE_JSON"

SIM_ID=$(jq -r '.id' "$CREATE_JSON")
[[ -z "$SIM_ID" || "$SIM_ID" == "null" ]] && echo "❌ Ошибка создания" && cat "$CREATE_JSON" && exit 1

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start.json"

FIRST_AREA=0
LAST_AREA=0
MAX_AREA=0
FIRST_SPREAD_STEP=0
HAD_GROWTH=0
HAD_BURNOUT=0
FINISHED_NORMALLY=0

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

    if [[ "$BURNING" -eq 0 ]]; then
        FINISHED_NORMALLY=1
        echo " Активных очагов больше нет"
        break
    fi
done

echo ""
echo "Итог:"
echo "firstArea=$FIRST_AREA"
echo "lastArea=$LAST_AREA"
echo "maxArea=$MAX_AREA"
echo "firstSpreadStep=$FIRST_SPREAD_STEP"
echo "hadGrowth=$HAD_GROWTH"
echo "hadBurnout=$HAD_BURNOUT"
echo "finishedNormally=$FINISHED_NORMALLY"

if [[ "$HAD_GROWTH" -ne 1 ]]; then
    echo "❌ ClusteredGraph не распространяется без wind jump"
    exit 1
fi

if [[ "$FIRST_SPREAD_STEP" -gt 4 || "$FIRST_SPREAD_STEP" -eq 0 ]]; then
    echo "❌ ClusteredGraph начинает распространяться слишком поздно"
    exit 1
fi

if (( $(echo "$MAX_AREA < 4" | bc -l) )); then
    echo "❌ ClusteredGraph всё ещё слишком слабый без wind jump: maxArea=$MAX_AREA"
    exit 1
fi

if (( $(echo "$MAX_AREA > 40" | bc -l) )); then
    echo "❌ ClusteredGraph стал слишком взрывным без wind jump: maxArea=$MAX_AREA"
    exit 1
fi

if [[ "$HAD_BURNOUT" -ne 1 ]]; then
    echo "❌ Не видно выгорания клеток"
    exit 1
fi

echo "✅ ClusteredGraph распространяется по связности даже без wind jump"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"