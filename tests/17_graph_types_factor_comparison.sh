#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_graph_types_factor_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo "ТЕСТ: сухой сценарий должен гореть сильнее влажного"
echo "============================================================"
echo ""

GRAPH_TYPES=(0 1)
GRAPH_NAMES=("Grid" "ClusteredGraph")

run_case() {
    local GRAPH_TYPE="$1"
    local GRAPH_NAME="$2"
    local LABEL="$3"
    local MOISTURE="$4"
    local HUMIDITY="$5"

    local CREATE_JSON="$OUT_DIR/create_${GRAPH_TYPE}_${LABEL}.json"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"${GRAPH_NAME}_${LABEL}\",
        \"description\": \"Factor comparison\",
        \"gridWidth\": 20,
        \"gridHeight\": 20,
        \"graphType\": $GRAPH_TYPE,
        \"initialMoistureMin\": $MOISTURE,
        \"initialMoistureMax\": $MOISTURE,
        \"elevationVariation\": 20.0,
        \"initialFireCellsCount\": 1,
        \"simulationSteps\": 10,
        \"stepDurationSeconds\": 1800,
        \"randomSeed\": 424242,
        \"temperature\": 32,
        \"humidity\": $HUMIDITY,
        \"windSpeed\": 7,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$CREATE_JSON"

    local SIM_ID
    SIM_ID=$(jq -r '.id' "$CREATE_JSON")

    if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
        echo "❌ Ошибка создания симуляции для $GRAPH_NAME / $LABEL" >&2
        cat "$CREATE_JSON" >&2
        exit 1
    fi

    local START_JSON="$OUT_DIR/start_${GRAPH_TYPE}_${LABEL}.json"
    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$START_JSON"

    local START_SUCCESS
    START_SUCCESS=$(jq -r '.success // false' "$START_JSON")
    if [[ "$START_SUCCESS" != "true" ]]; then
        echo "❌ Ошибка запуска симуляции для $GRAPH_NAME / $LABEL" >&2
        cat "$START_JSON" >&2
        exit 1
    fi

    local LAST_AREA=0
    local FINISHED_NORMALLY=0

    for step in $(seq 1 8); do
        local STEP_JSON="$OUT_DIR/step_${GRAPH_TYPE}_${LABEL}_${step}.json"
        curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

        local SUCCESS
        SUCCESS=$(jq -r '.success // false' "$STEP_JSON")

        if [[ "$SUCCESS" != "true" ]]; then
            local STATUS_JSON="$OUT_DIR/status_${GRAPH_TYPE}_${LABEL}_${step}.json"
            curl -s "$API_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_JSON"

            local STATUS_SUCCESS
            STATUS_SUCCESS=$(jq -r '.success // false' "$STATUS_JSON")
            local STATUS_CODE
            STATUS_CODE=$(jq -r '.simulation.status // -1' "$STATUS_JSON")
            local STATUS_RUNNING
            STATUS_RUNNING=$(jq -r '.simulation.isRunning // false' "$STATUS_JSON")

            if [[ "$STATUS_SUCCESS" == "true" && "$STATUS_RUNNING" != "true" && "$STATUS_CODE" == "2" ]]; then
                FINISHED_NORMALLY=1
                echo "       $LABEL завершилась до шага $step" >&2
                break
            fi

            echo "❌ Ошибка шага $step для $GRAPH_NAME / $LABEL" >&2
            cat "$STEP_JSON" >&2
            exit 1
        fi

        local AREA
        AREA=$(jq -r '.step.fireArea // 0' "$STEP_JSON")
        local BURNING
        BURNING=$(jq -r '.step.burningCellsCount // 0' "$STEP_JSON")
        local BURNED
        BURNED=$(jq -r '.step.burnedCellsCount // 0' "$STEP_JSON")

        LAST_AREA=$AREA

        echo "      step=$step | area=$AREA | burning=$BURNING | burned=$BURNED" >&2

        if [[ "$BURNING" -eq 0 ]]; then
            FINISHED_NORMALLY=1
            echo "       $LABEL: очагов больше нет" >&2
            break
        fi
    done

    echo "$LAST_AREA"
}

for i in "${!GRAPH_TYPES[@]}"; do
    GRAPH_TYPE="${GRAPH_TYPES[$i]}"
    GRAPH_NAME="${GRAPH_NAMES[$i]}"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "GRAPH TYPE: $GRAPH_NAME"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    echo "   dry scenario:"
    DRY_AREA=$(run_case "$GRAPH_TYPE" "$GRAPH_NAME" "dry" "0.15" "25")

    echo "   wet scenario:"
    WET_AREA=$(run_case "$GRAPH_TYPE" "$GRAPH_NAME" "wet" "0.65" "80")

    echo ""
    echo "   dry area = $DRY_AREA"
    echo "   wet area = $WET_AREA"

    if (( $(echo "$DRY_AREA < $WET_AREA" | bc -l) )); then
        echo "❌ На графе $GRAPH_NAME влажный сценарий горит сильнее сухого"
        exit 1
    fi

    echo "   ✅ Фактор влажности работает правильно"
    echo ""
done

echo "============================================================"
echo "✅ Сравнение факторов по типам графов пройдено"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"