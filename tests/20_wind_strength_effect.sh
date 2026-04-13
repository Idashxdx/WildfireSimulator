#!/usr/bin/env bash
set -euo pipefail

API="${API:-http://localhost:5198/api}"
STEPS=12
TMP_DIR="/tmp/wind_strength_effect_$$"
mkdir -p "$TMP_DIR"

echo "============================================================"
echo " ТЕСТ: сильный ветер должен усиливать распространение"
echo "============================================================"

run_case () {
  local SPEED="$1"
  local LABEL="$2"

  {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "CASE: $LABEL (wind=$SPEED)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  } >&2

  local SIM
  SIM=$(curl -s -X POST "$API/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"wind_strength_$LABEL\",
      \"description\": \"wind strength effect test\",
      \"gridWidth\": 20,
      \"gridHeight\": 20,
      \"graphType\": 1,
      \"initialMoistureMin\": 0.20,
      \"initialMoistureMax\": 0.20,
      \"elevationVariation\": 10.0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 30,
      \"stepDurationSeconds\": 3600,
      \"randomSeed\": 424242,
      \"temperature\": 32,
      \"humidity\": 20,
      \"windSpeed\": $SPEED,
      \"windDirection\": 90,
      \"precipitation\": 0
    }")

  local ID
  ID=$(echo "$SIM" | jq -r '.id')

  echo "SIM_ID=$ID" >&2

  curl -s -X POST "$API/SimulationManager/$ID/start" > /dev/null

  local AREA=0
  local STATUS_JSON=""

  for i in $(seq 1 $STEPS); do
    STATUS_JSON=$(curl -s -X POST "$API/SimulationManager/$ID/step")
    local SUCCESS
    SUCCESS=$(echo "$STATUS_JSON" | jq -r '.success // false')

    if [[ "$SUCCESS" != "true" ]]; then
      echo "step=$i | simulation finished or error" >&2
      break
    fi

    AREA=$(echo "$STATUS_JSON" | jq -r '.step.fireArea // 0')
    local BURNING
    BURNING=$(echo "$STATUS_JSON" | jq -r '.step.burningCellsCount // 0')
    local BURNED
    BURNED=$(echo "$STATUS_JSON" | jq -r '.step.burnedCellsCount // 0')

    echo "step=$i area=$AREA burning=$BURNING burned=$BURNED" >&2

    local IS_RUNNING
    IS_RUNNING=$(echo "$STATUS_JSON" | jq -r '.isRunning // true')
    if [[ "$IS_RUNNING" != "true" ]]; then
      break
    fi
  done

  printf '%s\n' "$AREA"
}

LOW=$(run_case 3 "LOW")
HIGH=$(run_case 15 "HIGH")

echo ""
echo "============================================================"
echo "RESULT:"
echo "LOW wind area  = $LOW"
echo "HIGH wind area = $HIGH"

if (( $(echo "$HIGH > $LOW" | bc -l) )); then
  echo "✅ Сильный ветер усиливает распространение"
elif (( $(echo "$HIGH == $LOW" | bc -l) )); then
  echo "⚠️ В этом сценарии эффект ветра по площади не проявился"
  echo "   Нужно проверить либо топологию, либо усиление ветрового переноса"
else
  echo "❌ Сильный ветер дал меньшую площадь, чем слабый"
fi

echo "📁 Логи: $TMP_DIR"