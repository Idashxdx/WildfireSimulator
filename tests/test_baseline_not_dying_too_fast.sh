#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_baseline_not_dying_too_fast_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: baseline не должен тухнуть слишком быстро"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
START_JSON="$OUT_DIR/start.json"

STEP_COUNT=6
STEP_FILES=()

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Baseline not dying too fast test",
    "description": "Проверка, что baseline grid не затухает мгновенно в умеренных условиях",
    "graphType": 0,
    "gridWidth": 25,
    "gridHeight": 25,
    "initialMoistureMin": 0.18,
    "initialMoistureMax": 0.28,
    "elevationVariation": 20.0,
    "initialFireCellsCount": 1,
    "simulationSteps": 30,
    "stepDurationSeconds": 900,
    "randomSeed": 20260422,
    "mapCreationMode": 1,
    "scenarioType": 0,
    "temperature": 27.0,
    "humidity": 38.0,
    "windSpeed": 5.0,
    "windDirection": 45.0,
    "precipitation": 0.0
  }' > "$CREATE_JSON"

SIM_ID=$(jq -r '.id // empty' "$CREATE_JSON")

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$CREATE_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d '{
    "ignitionMode": "saved-or-random"
  }' > "$START_JSON"

START_SUCCESS=$(jq -r '.success // false' "$START_JSON" 2>/dev/null || echo "false")
if [[ "$START_SUCCESS" != "true" ]]; then
  echo "❌ Не удалось запустить симуляцию"
  cat "$START_JSON"
  exit 1
fi

echo "✅ Симуляция запущена"

for step in $(seq 1 "$STEP_COUNT"); do
  STEP_JSON="$OUT_DIR/step_${step}.json"
  STEP_FILES+=("$STEP_JSON")

  curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

  if [[ ! -s "$STEP_JSON" ]]; then
    echo "❌ Step endpoint вернул пустой ответ на шаге $step"
    exit 1
  fi

  STEP_SUCCESS=$(jq -r '.success // false' "$STEP_JSON" 2>/dev/null || echo "false")
  if [[ "$STEP_SUCCESS" != "true" ]]; then
    echo "❌ Не удалось выполнить шаг $step"
    cat "$STEP_JSON"
    exit 1
  fi

  AREA=$(jq -r '.step.fireArea // 0' "$STEP_JSON")
  BURNING=$(jq -r '.step.burningCellsCount // 0' "$STEP_JSON")
  BURNED=$(jq -r '.step.burnedCellsCount // 0' "$STEP_JSON")
  IS_RUNNING=$(jq -r '.isRunning // false' "$STEP_JSON")

  echo "step=$step area=$AREA burning=$BURNING burned=$BURNED isRunning=$IS_RUNNING"
done

python3 - "${STEP_FILES[@]}" <<'PY'
import json
import sys

step_paths = sys.argv[1:]
steps = []

for path in step_paths:
    with open(path, "r", encoding="utf-8") as f:
        root = json.load(f)

    step = root.get("step", {})
    steps.append({
        "step": int(step.get("step", 0)),
        "fireArea": float(step.get("fireArea", 0.0) or 0.0),
        "burning": int(step.get("burningCellsCount", 0) or 0),
        "burned": int(step.get("burnedCellsCount", 0) or 0),
        "totalAffected": int(step.get("totalCellsAffected", 0) or 0),
        "isRunning": bool(root.get("isRunning", False)),
    })

if not steps:
    print("❌ Не удалось прочитать шаги")
    sys.exit(1)

first = steps[0]
last = steps[-1]

max_area = max(s["fireArea"] for s in steps)
max_burning = max(s["burning"] for s in steps)
burning_positive_steps = sum(1 for s in steps if s["burning"] > 0)
affected_positive_steps = sum(1 for s in steps if s["totalAffected"] > 0)

print(f"first_step_area = {first['fireArea']}")
print(f"last_step_area = {last['fireArea']}")
print(f"max_area = {max_area}")
print(f"max_burning = {max_burning}")
print(f"burning_positive_steps = {burning_positive_steps}")
print(f"affected_positive_steps = {affected_positive_steps}")
print(f"last_is_running = {last['isRunning']}")

errors = []

if max_area <= 1.0:
    errors.append(f"пожар не должен оставаться на площади <= 1 га, сейчас max_area={max_area}")

if max_burning <= 0:
    errors.append("ни на одном шаге нет активного горения")

if burning_positive_steps < 3:
    errors.append(f"активное горение держится слишком мало шагов: {burning_positive_steps}")

if affected_positive_steps < 3:
    errors.append(f"затронутые клетки наблюдаются слишком мало шагов: {affected_positive_steps}")

if last["fireArea"] <= 0.0:
    errors.append("к последнему проверяемому шагу площадь пожара стала нулевой")

if errors:
    print("❌ Baseline тухнет слишком быстро")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ Baseline grid не тухнет мгновенно в умеренных условиях")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"
