#!/usr/bin/env bash
set -euo pipefail

API="${API:-http://localhost:5198/api}"
STEPS=10
TMP_DIR="/tmp/wind_direction_bias_$$"
mkdir -p "$TMP_DIR"

echo "============================================================"
echo "ТЕСТ: ветер должен заметно смещать фронт по X"
echo "============================================================"

run_case () {
  local DIR="$1"
  local LABEL="$2"
  local LOG_FILE="$TMP_DIR/${LABEL}.log"

  {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "CASE: $LABEL (wind=$DIR)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  } >&2

  local SIM
  SIM=$(curl -s -X POST "$API/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"wind_test_$LABEL\",
      \"description\": \"wind direction bias test\",
      \"gridWidth\": 20,
      \"gridHeight\": 20,
      \"graphType\": 0,
      \"initialMoistureMin\": 0.20,
      \"initialMoistureMax\": 0.20,
      \"elevationVariation\": 10.0,
      \"initialFireCellsCount\": 1,
      \"initialFirePositions\": [
        { \"x\": 10, \"y\": 10 }
      ],
      \"simulationSteps\": 30,
      \"stepDurationSeconds\": 3600,
      \"randomSeed\": 424242,
      \"temperature\": 30,
      \"humidity\": 20,
      \"windSpeed\": 15,
      \"windDirection\": $DIR,
      \"precipitation\": 0
    }")

  local ID
  ID=$(echo "$SIM" | jq -r '.id')

  echo "SIM_ID=$ID" >&2

  curl -s -X POST "$API/SimulationManager/$ID/start" > /dev/null

  for i in $(seq 1 $STEPS); do
    curl -s -X POST "$API/SimulationManager/$ID/step" >> "$LOG_FILE"
    echo "" >> "$LOG_FILE"
  done

  local CELLS
  CELLS=$(curl -s "$API/SimulationManager/$ID/cells")

  local AVG_X
  AVG_X=$(echo "$CELLS" | jq -r '
    [.cells[] | select(.state=="Burning" or .state=="Burned") | .x] as $xs
    | if ($xs | length) > 0 then (($xs | add) / ($xs | length)) else 0 end
  ')

  echo "avg_x = $AVG_X" >&2

  printf '%s\n' "$AVG_X"
}

RIGHT=$(run_case 90 "RIGHT")
LEFT=$(run_case 270 "LEFT")

echo ""
echo "============================================================"
echo "RESULT:"
echo "RIGHT avg_x = $RIGHT"
echo "LEFT avg_x  = $LEFT"

DIFF=$(echo "$RIGHT - $LEFT" | bc -l)
ABS_DIFF=$(echo "$DIFF" | awk '{v=$1; if (v<0) v=-v; print v}')

echo "ΔX = $DIFF"
echo "|ΔX| = $ABS_DIFF"

if (( $(echo "$ABS_DIFF >= 2.0" | bc -l) )); then
  echo "✅ Ветер заметно влияет на направление распространения по оси X"

  if (( $(echo "$RIGHT > $LEFT" | bc -l) )); then
    echo " В вашей модели windDirection=90 смещает фронт вправо сильнее, чем 270"
  else
    echo " В вашей модели windDirection=270 смещает фронт вправо сильнее, чем 90"
    echo "   Это не обязательно ошибка — скорее особенность конвенции угла ветра."
  fi
else
  echo "❌ Ветер почти не влияет на направление распространения по оси X"
fi

echo "📁 Логи: $TMP_DIR"