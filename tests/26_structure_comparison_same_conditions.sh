#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/structure_comparison_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: сравнение структур при одинаковых условиях"
echo "============================================================"
echo ""

GRAPH_TYPES=(0 1 2)
GRAPH_NAMES=("Grid" "ClusteredGraph" "RegionClusterGraph")

REAL_TIME_SECONDS=10800
STEP_DURATION=1800
STEPS=$((REAL_TIME_SECONDS / STEP_DURATION))

declare -A FINAL_AREA
declare -A FINAL_BURNED
declare -A FINAL_BURNING
declare -A FIRST_SPREAD_STEP

for i in "${!GRAPH_TYPES[@]}"; do
    GRAPH_TYPE="${GRAPH_TYPES[$i]}"
    GRAPH_NAME="${GRAPH_NAMES[$i]}"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " GRAPH TYPE: $GRAPH_NAME"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    CREATE_JSON="$OUT_DIR/create_${GRAPH_TYPE}.json"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"Compare ${GRAPH_NAME}\",
        \"description\": \"Structure comparison\",
        \"gridWidth\": 20,
        \"gridHeight\": 20,
        \"graphType\": $GRAPH_TYPE,
        \"initialMoistureMin\": 0.20,
        \"initialMoistureMax\": 0.20,
        \"elevationVariation\": 10.0,
        \"initialFireCellsCount\": 1,
        \"simulationSteps\": $STEPS,
        \"stepDurationSeconds\": $STEP_DURATION,
        \"randomSeed\": 424242,
        \"temperature\": 32,
        \"humidity\": 35,
        \"windSpeed\": 8,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$CREATE_JSON"

    SIM_ID=$(jq -r '.id' "$CREATE_JSON")

    if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
        echo "❌ Ошибка создания"
        cat "$CREATE_JSON"
        exit 1
    fi

    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start_${GRAPH_TYPE}.json"

    FIRST_SPREAD=0

    for step in $(seq 1 "$STEPS"); do
        STEP_JSON="$OUT_DIR/step_${GRAPH_TYPE}_${step}.json"
        curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

        SUCCESS=$(jq -r '.success // false' "$STEP_JSON")
        if [[ "$SUCCESS" != "true" ]]; then
            break
        fi

        NEWLY=$(jq -r '.step.newlyIgnitedCells // 0' "$STEP_JSON")
        if [[ "$FIRST_SPREAD" -eq 0 && "$NEWLY" -gt 0 ]]; then
            FIRST_SPREAD=$step
        fi

        RUNNING=$(jq -r '.isRunning // true' "$STEP_JSON")
        if [[ "$RUNNING" != "true" ]]; then
            break
        fi
    done

    STATUS_JSON="$OUT_DIR/status_${GRAPH_TYPE}.json"
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_JSON"

    FINAL_AREA["$GRAPH_NAME"]=$(jq -r '.simulation.fireArea // 0' "$STATUS_JSON")
    FINAL_BURNED["$GRAPH_NAME"]=$(jq -r '.simulation.totalBurnedCells // 0' "$STATUS_JSON")
    FINAL_BURNING["$GRAPH_NAME"]=$(jq -r '.simulation.totalBurningCells // 0' "$STATUS_JSON")
    FIRST_SPREAD_STEP["$GRAPH_NAME"]=$FIRST_SPREAD

    echo "   firstSpreadStep = ${FIRST_SPREAD_STEP["$GRAPH_NAME"]}"
    echo "   area            = ${FINAL_AREA["$GRAPH_NAME"]}"
    echo "   burned          = ${FINAL_BURNED["$GRAPH_NAME"]}"
    echo "   burning         = ${FINAL_BURNING["$GRAPH_NAME"]}"
    echo ""
done

echo "============================================================"
echo "📈 ИТОГОВОЕ СРАВНЕНИЕ"
echo "============================================================"
echo ""

echo "┌────────────────┬──────────────────┬──────────────┬──────────────┬──────────────┐"
echo "│ Структура      │ firstSpreadStep  │ Площадь (га) │ Сгорело      │ Горит        │"
echo "├────────────────┼──────────────────┼──────────────┼──────────────┼──────────────┤"

for NAME in "${GRAPH_NAMES[@]}"; do
    printf "│ %-14s │ %16s │ %12s │ %12s │ %12s │\n" \
      "$NAME" \
      "${FIRST_SPREAD_STEP["$NAME"]}" \
      "${FINAL_AREA["$NAME"]}" \
      "${FINAL_BURNED["$NAME"]}" \
      "${FINAL_BURNING["$NAME"]}"
done

echo "└────────────────┴──────────────────┴──────────────┴──────────────┴──────────────┘"
echo ""
echo "📁 Логи: $OUT_DIR"
echo "============================================================"