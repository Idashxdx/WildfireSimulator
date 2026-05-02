#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_absolute_limit_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo "ТЕСТ: АБСОЛЮТНЫЙ ПРЕДЕЛ РОСТА ПЛОЩАДИ"
echo "    1 клетка = 1 га"
echo "============================================================"
echo ""

TIME_LABELS=("90min" "3h")
TIME_SECONDS=(5400 10800)

STEP_DURATIONS=(60 5400)
GRID_SIZE=21
FIXED_RANDOM_SEED=424242

TEMPERATURE=35
HUMIDITY=20
WIND_SPEED=12
WIND_DIRECTION=45
MOISTURE=0.20

LIMIT_90MIN=7
LIMIT_3H=25

declare -A FINAL_AREA
declare -A FINAL_BURNED
declare -A FINAL_BURNING

run_case() {
    local LABEL="$1"
    local REAL_TIME_SECONDS="$2"
    local DT="$3"

    local STEPS_NEEDED=$((REAL_TIME_SECONDS / DT))
    [[ "$STEPS_NEEDED" -le 0 ]] && STEPS_NEEDED=1

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "CASE: $LABEL | dt=$DT"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "   Реальное время: $REAL_TIME_SECONDS сек"
    echo "   Шагов: $STEPS_NEEDED"
    echo ""

    local CREATE_JSON="$OUT_DIR/create_${LABEL}_${DT}.json"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"AbsoluteLimit ${LABEL} dt=${DT}\",
        \"description\": \"Контроль абсолютного роста площади\",
        \"gridWidth\": $GRID_SIZE,
        \"gridHeight\": $GRID_SIZE,
        \"graphType\": 0,
        \"initialMoistureMin\": $MOISTURE,
        \"initialMoistureMax\": $MOISTURE,
        \"elevationVariation\": 10.0,
        \"initialFireCellsCount\": 1,
        \"initialFirePositions\": [
          { \"x\": 10, \"y\": 10 }
        ],
        \"simulationSteps\": $STEPS_NEEDED,
        \"stepDurationSeconds\": $DT,
        \"randomSeed\": $FIXED_RANDOM_SEED,
        \"temperature\": $TEMPERATURE,
        \"humidity\": $HUMIDITY,
        \"windSpeed\": $WIND_SPEED,
        \"windDirection\": $WIND_DIRECTION,
        \"precipitation\": 0
      }" > "$CREATE_JSON"

    local SIM_ID
    SIM_ID=$(jq -r '.id' "$CREATE_JSON")

    if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
        echo "❌ Ошибка создания симуляции"
        cat "$CREATE_JSON"
        exit 1
    fi

    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start_${LABEL}_${DT}.json"

    for step in $(seq 1 "$STEPS_NEEDED"); do
        local STEP_JSON="$OUT_DIR/step_${LABEL}_${DT}_${step}.json"
        curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

        local success
        success=$(jq -r '.success // false' "$STEP_JSON")
        if [[ "$success" != "true" ]]; then
            break
        fi

        printf "."

        local is_running
        is_running=$(jq -r '.isRunning // true' "$STEP_JSON")
        if [[ "$is_running" != "true" ]]; then
            break
        fi
    done
    echo ""

    local STATUS_JSON="$OUT_DIR/status_${LABEL}_${DT}.json"
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_JSON"

    FINAL_AREA["${LABEL}_${DT}"]=$(jq -r '.simulation.fireArea // 0' "$STATUS_JSON")
    FINAL_BURNED["${LABEL}_${DT}"]=$(jq -r '.simulation.totalBurnedCells // 0' "$STATUS_JSON")
    FINAL_BURNING["${LABEL}_${DT}"]=$(jq -r '.simulation.totalBurningCells // 0' "$STATUS_JSON")

    echo "   area=${FINAL_AREA["${LABEL}_${DT}"]} га, burned=${FINAL_BURNED["${LABEL}_${DT}"]}, burning=${FINAL_BURNING["${LABEL}_${DT}"]}"
    echo ""
}

for i in "${!TIME_LABELS[@]}"; do
    LABEL="${TIME_LABELS[$i]}"
    REAL_TIME="${TIME_SECONDS[$i]}"

    for DT in "${STEP_DURATIONS[@]}"; do
        run_case "$LABEL" "$REAL_TIME" "$DT"
    done
done

echo "============================================================"
echo "ИТОГОВАЯ ТАБЛИЦА"
echo "============================================================"
echo ""
echo "┌──────────┬──────────────┬──────────────┬──────────────┬─────────────────────┐"
echo "│ Период   │ dt           │ Площадь (га) │ Горит        │ Сгорело             │"
echo "├──────────┼──────────────┼──────────────┼──────────────┼─────────────────────┤"

for i in "${!TIME_LABELS[@]}"; do
    LABEL="${TIME_LABELS[$i]}"
    for DT in "${STEP_DURATIONS[@]}"; do
        printf "│ %-8s │ %12s │ %12s │ %12s │ %19s │\n" \
          "$LABEL" \
          "$DT" \
          "${FINAL_AREA["${LABEL}_${DT}"]}" \
          "${FINAL_BURNING["${LABEL}_${DT}"]}" \
          "${FINAL_BURNED["${LABEL}_${DT}"]}"
    done
done

echo "└──────────┴──────────────┴──────────────┴──────────────┴─────────────────────┘"
echo ""

AREA_90_60=${FINAL_AREA["90min_60"]}
AREA_90_5400=${FINAL_AREA["90min_5400"]}
AREA_3H_60=${FINAL_AREA["3h_60"]}
AREA_3H_5400=${FINAL_AREA["3h_5400"]}

FAILED=0

check_limit () {
    local area="$1"
    local limit="$2"
    local label="$3"

    if (( $(echo "$area <= $limit" | bc -l) )); then
        echo "✅ $label: $area га ≤ $limit га"
    else
        echo "❌ $label: $area га > $limit га"
        FAILED=1
    fi
}

echo "ПРОВЕРКА АБСОЛЮТНЫХ ОГРАНИЧЕНИЙ:"
check_limit "$AREA_90_60" "$LIMIT_90MIN" "90 минут, dt=60"
check_limit "$AREA_90_5400" "$LIMIT_90MIN" "90 минут, dt=5400"
check_limit "$AREA_3H_60" "$LIMIT_3H" "3 часа, dt=60"
check_limit "$AREA_3H_5400" "$LIMIT_3H" "3 часа, dt=5400"

echo ""
echo "ПРОВЕРКА СОГЛАСОВАННОСТИ 60 vs 5400:"
DIFF_90=$(echo "scale=4; $AREA_90_5400 - $AREA_90_60" | bc -l)
ABS_DIFF_90=$(echo "$DIFF_90" | tr -d '-')
if (( $(echo "$AREA_90_60 > 0" | bc -l) )); then
    DEV_90=$(echo "scale=4; ($ABS_DIFF_90 / $AREA_90_60) * 100" | bc -l)
else
    DEV_90=0
fi

DIFF_3H=$(echo "scale=4; $AREA_3H_5400 - $AREA_3H_60" | bc -l)
ABS_DIFF_3H=$(echo "$DIFF_3H" | tr -d '-')
if (( $(echo "$AREA_3H_60 > 0" | bc -l) )); then
    DEV_3H=$(echo "scale=4; ($ABS_DIFF_3H / $AREA_3H_60) * 100" | bc -l)
else
    DEV_3H=0
fi

echo "90 минут: Δ=$DIFF_90 га, dev=$DEV_90 %"
echo "3 часа:   Δ=$DIFF_3H га, dev=$DEV_3H %"

if (( $(echo "$DEV_90 > 5" | bc -l) )); then
    echo "❌ Для 90 минут результаты слишком зависят от dt"
    FAILED=1
fi

if (( $(echo "$DEV_3H > 5" | bc -l) )); then
    echo "❌ Для 3 часов результаты слишком зависят от dt"
    FAILED=1
fi

echo ""
echo "📁 Логи: $OUT_DIR"
echo "============================================================"

if [[ "$FAILED" -ne 0 ]]; then
    exit 1
fi

echo "✅ ТЕСТ ПРОЙДЕН"