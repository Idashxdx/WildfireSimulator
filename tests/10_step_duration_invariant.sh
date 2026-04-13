#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/step_duration_invariant_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo "ТЕСТ: stepDurationSeconds НЕ МЕНЯЕТ ФИЗИКУ"
echo "============================================================"
echo ""

GRID_SIZE=15

REAL_TIME_SECONDS=10800
REAL_TIME_HOURS=3

STEP_DURATIONS=(300 600 900 1800 3600 5400)
FIXED_RANDOM_SEED=424242

declare -A FINAL_AREAS
declare -A FINAL_BURNED
declare -A FINAL_BURNING
declare -A FINAL_STATUS
declare -A FINAL_STOP_REASON

for DT in "${STEP_DURATIONS[@]}"; do
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "ТЕСТ: stepDurationSeconds = $DT сек"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    STEPS_NEEDED=$((REAL_TIME_SECONDS / DT))
    STEPS_NEEDED=$((STEPS_NEEDED > 0 ? STEPS_NEEDED : 1))

    echo "   Реальное время: $REAL_TIME_HOURS часов"
    echo "   Шагов потребуется: $STEPS_NEEDED"
    echo "   Проверка: $STEPS_NEEDED * $DT = $((STEPS_NEEDED * DT)) сек"
    echo "   RandomSeed: $FIXED_RANDOM_SEED"

    CREATE_JSON="$OUT_DIR/create_dt_${DT}.json"

    curl -s -X POST "$API_URL/api/simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"Dt Test ${DT}s\",
        \"description\": \"Проверка независимости от dt\",
        \"gridWidth\": $GRID_SIZE,
        \"gridHeight\": $GRID_SIZE,
        \"graphType\": 0,
        \"initialMoistureMin\": 0.20,
        \"initialMoistureMax\": 0.20,
        \"elevationVariation\": 20.0,
        \"initialFireCellsCount\": 1,
        \"initialFirePositions\": [
          { \"x\": 7, \"y\": 7 }
        ],
        \"simulationSteps\": $STEPS_NEEDED,
        \"stepDurationSeconds\": $DT,
        \"randomSeed\": $FIXED_RANDOM_SEED,
        \"temperature\": 30,
        \"humidity\": 40,
        \"windSpeed\": 8,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$CREATE_JSON"

    SIM_ID=$(jq -r '.id' "$CREATE_JSON")

    if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
      echo "   ❌ Ошибка создания симуляции"
      cat "$CREATE_JSON"
      exit 1
    fi

    echo "   ✅ Симуляция создана: $SIM_ID"

    echo "   🚀 Запуск..."
    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start_${DT}.json"

    echo "   ⚡ Выполнение до $STEPS_NEEDED шагов..."
    STOP_REASON="completed_requested_steps"

    for step in $(seq 1 $STEPS_NEEDED); do
        STEP_JSON="$OUT_DIR/step_${DT}_${step}.json"
        curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

        success=$(jq -r '.success // false' "$STEP_JSON" 2>/dev/null || echo "false")

        if [[ "$success" == "true" ]]; then
            isRunning=$(jq -r '.isRunning // true' "$STEP_JSON" 2>/dev/null || echo "true")
            echo -n "."

            if [[ "$isRunning" != "true" ]]; then
                STOP_REASON="simulation_finished_normally_on_step_${step}"
                echo ""
                echo "    Симуляция штатно завершилась на шаге $step"
                break
            fi
            continue
        fi

        STATUS_JSON="$OUT_DIR/status_after_failure_${DT}_${step}.json"
        curl -s "$API_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_JSON"

        status_success=$(jq -r '.success // false' "$STATUS_JSON" 2>/dev/null || echo "false")
        status_running=$(jq -r '.simulation.isRunning // false' "$STATUS_JSON" 2>/dev/null || echo "false")

        if [[ "$status_success" == "true" && "$status_running" != "true" ]]; then
            STOP_REASON="simulation_already_finished_before_step_${step}"
            echo ""
            echo "    Симуляция уже была завершена к шагу $step"
            break
        fi

        echo ""
        echo "   ❌ Реальная ошибка на шаге $step"
        cat "$STEP_JSON"
        STOP_REASON="real_error_on_step_${step}"
        break
    done

    STATUS_JSON="$OUT_DIR/status_${DT}.json"
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/status" > "$STATUS_JSON"

    FINAL_AREA=$(jq -r '.simulation.fireArea // 0' "$STATUS_JSON")
    BURNED_CELLS=$(jq -r '.simulation.totalBurnedCells // 0' "$STATUS_JSON")
    BURNING_CELLS=$(jq -r '.simulation.totalBurningCells // 0' "$STATUS_JSON")
    IS_RUNNING=$(jq -r '.simulation.isRunning // false' "$STATUS_JSON")
    STATUS_CODE=$(jq -r '.simulation.status // -1' "$STATUS_JSON")

    FINAL_AREAS["$DT"]=$FINAL_AREA
    FINAL_BURNED["$DT"]=$BURNED_CELLS
    FINAL_BURNING["$DT"]=$BURNING_CELLS
    FINAL_STATUS["$DT"]=$STATUS_CODE
    FINAL_STOP_REASON["$DT"]=$STOP_REASON

    echo ""
    echo "      РЕЗУЛЬТАТ:"
    echo "      Площадь пожара: $FINAL_AREA га"
    echo "      Сгорело клеток: $BURNED_CELLS"
    echo "      Горит клеток: $BURNING_CELLS"
    echo "      IsRunning: $IS_RUNNING"
    echo "      Status: $STATUS_CODE"
    echo "      Причина остановки: $STOP_REASON"

    if [[ "$IS_RUNNING" == "true" ]]; then
        curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/stop" > /dev/null || true
    fi
done

echo ""
echo "============================================================"
echo " ИТОГОВОЕ СРАВНЕНИЕ (за $REAL_TIME_HOURS часов)"
echo "============================================================"
echo ""

AREAS=()
for DT in "${STEP_DURATIONS[@]}"; do
    AREAS+=(${FINAL_AREAS["$DT"]})
done

IFS=$'\n' SORTED_AREAS=($(sort -n <<<"${AREAS[*]}"))
unset IFS
MEDIAN=${SORTED_AREAS[${#SORTED_AREAS[@]}/2]}

echo "┌──────────────┬──────────────┬──────────────┬──────────────┬─────────────────────────────────────┐"
echo "│ Длительность │ Площадь      │ Сгорело      │ Горит        │ Отклонение от медианы               │"
echo "│ шага (сек)   │ (га)         │              │              │                                     │"
echo "├──────────────┼──────────────┼──────────────┼──────────────┼─────────────────────────────────────┤"

MAX_DEVIATION=0
for DT in "${STEP_DURATIONS[@]}"; do
    AREA=${FINAL_AREAS["$DT"]}
    BURNED=${FINAL_BURNED["$DT"]}
    BURNING=${FINAL_BURNING["$DT"]}

    if (( $(echo "$MEDIAN > 0" | bc -l) )); then
        DEVIATION=$(echo "scale=1; (($AREA - $MEDIAN) / $MEDIAN) * 100" | bc -l)
        DEVIATION_ABS=$(echo "$DEVIATION" | tr -d '-' | cut -d. -f1)
        if [[ $DEVIATION_ABS -gt $MAX_DEVIATION ]]; then
            MAX_DEVIATION=$DEVIATION_ABS
        fi
    else
        DEVIATION=0
    fi

    if (( $(echo "$DEVIATION > 0" | bc -l) )); then
        BAR="📈 +${DEVIATION}%"
    elif (( $(echo "$DEVIATION < 0" | bc -l) )); then
        BAR="📉 ${DEVIATION}%"
    else
        BAR="✅ 0%"
    fi

    printf "│ %12s │ %12s │ %12s │ %12s │ %-35s │\n" "$DT" "$AREA" "$BURNED" "$BURNING" "$BAR"
done

echo "└──────────────┴──────────────┴──────────────┴──────────────┴─────────────────────────────────────┘"
echo ""

echo " Причины остановки:"
for DT in "${STEP_DURATIONS[@]}"; do
    echo "   dt=$DT -> ${FINAL_STOP_REASON["$DT"]}"
done
echo ""

if [[ $MAX_DEVIATION -le 10 ]]; then
    echo "✅ РЕЗУЛЬТАТ: stepDurationSeconds практически не меняет физику"
    echo "   Максимальное отклонение: ${MAX_DEVIATION}%"
elif [[ $MAX_DEVIATION -le 20 ]]; then
    echo "✅ РЕЗУЛЬТАТ: stepDurationSeconds НЕ меняет физику критично"
    echo "   Максимальное отклонение: ${MAX_DEVIATION}%"
    echo "   Это допустимо для вероятностной модели"
elif [[ $MAX_DEVIATION -le 35 ]]; then
    echo "⚠️ РЕЗУЛЬТАТ: stepDurationSeconds влияет умеренно"
    echo "   Максимальное отклонение: ${MAX_DEVIATION}%"
    echo "   Нужна дополнительная калибровка"
else
    echo "❌ РЕЗУЛЬТАТ: stepDurationSeconds сильно меняет физику!"
    echo "   Максимальное отклонение: ${MAX_DEVIATION}%"
    echo "   Требуется исправление механизма масштабирования"
fi

echo ""
echo "📁 Логи сохранены в: $OUT_DIR"
echo "============================================================"