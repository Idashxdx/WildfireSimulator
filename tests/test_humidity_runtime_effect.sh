#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_humidity_runtime_effect_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: влажность воздуха должна ослаблять распространение"
echo "============================================================"

STEP_COUNT=8

create_simulation() {
  local humidity="$1"
  local out_json="$2"

  curl -s -X POST "$API_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"Humidity runtime effect (${humidity})\",
      \"description\": \"Проверка runtime-влияния влажности воздуха\",
      \"graphType\": 0,
      \"gridWidth\": 25,
      \"gridHeight\": 25,
      \"initialMoistureMin\": 0.18,
      \"initialMoistureMax\": 0.28,
      \"elevationVariation\": 20.0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 40,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 20260422,
      \"mapCreationMode\": 1,
      \"scenarioType\": 0,
      \"temperature\": 30.0,
      \"humidity\": ${humidity},
      \"windSpeed\": 5.0,
      \"windDirection\": 45.0,
      \"precipitation\": 0.0
    }" > "$out_json"
}

run_case() {
  local sim_id="$1"
  local prefix="$2"

  local start_json="$OUT_DIR/${prefix}_start.json"
  curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d '{
      "ignitionMode": "saved-or-random"
    }' > "$start_json"

  local start_success
  start_success=$(jq -r '.success // false' "$start_json" 2>/dev/null || echo "false")
  if [[ "$start_success" != "true" ]]; then
    echo "❌ Не удалось запустить симуляцию $sim_id" >&2
    cat "$start_json" >&2
    exit 1
  fi

  echo "✅ Запущена симуляция $prefix: $sim_id" >&2

  local last_step_json=""
  for step in $(seq 1 "$STEP_COUNT"); do
    last_step_json="$OUT_DIR/${prefix}_step_${step}.json"

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/step" > "$last_step_json"

    if [[ ! -s "$last_step_json" ]]; then
      echo "❌ Step endpoint вернул пустой ответ для $prefix step=$step" >&2
      exit 1
    fi

    local step_success
    step_success=$(jq -r '.success // false' "$last_step_json" 2>/dev/null || echo "false")
    if [[ "$step_success" != "true" ]]; then
      echo "❌ Не удалось выполнить шаг для $prefix step=$step" >&2
      cat "$last_step_json" >&2
      exit 1
    fi
  done

  echo "$last_step_json"
}

LOW_JSON="$OUT_DIR/low_create.json"
MID_JSON="$OUT_DIR/mid_create.json"
HIGH_JSON="$OUT_DIR/high_create.json"

create_simulation 20 "$LOW_JSON"
create_simulation 40 "$MID_JSON"
create_simulation 80 "$HIGH_JSON"

LOW_SIM_ID=$(jq -r '.id // empty' "$LOW_JSON")
MID_SIM_ID=$(jq -r '.id // empty' "$MID_JSON")
HIGH_SIM_ID=$(jq -r '.id // empty' "$HIGH_JSON")

if [[ -z "$LOW_SIM_ID" || "$LOW_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать LOW humidity simulation"
  cat "$LOW_JSON"
  exit 1
fi

if [[ -z "$MID_SIM_ID" || "$MID_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать MID humidity simulation"
  cat "$MID_JSON"
  exit 1
fi

if [[ -z "$HIGH_SIM_ID" || "$HIGH_SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать HIGH humidity simulation"
  cat "$HIGH_JSON"
  exit 1
fi

echo "low_sim_id  = $LOW_SIM_ID"
echo "mid_sim_id  = $MID_SIM_ID"
echo "high_sim_id = $HIGH_SIM_ID"

LOW_LAST_STEP=$(run_case "$LOW_SIM_ID" "low")
MID_LAST_STEP=$(run_case "$MID_SIM_ID" "mid")
HIGH_LAST_STEP=$(run_case "$HIGH_SIM_ID" "high")

python3 - "$LOW_LAST_STEP" "$MID_LAST_STEP" "$HIGH_LAST_STEP" <<'PY'
import json
import sys

low_path = sys.argv[1]
mid_path = sys.argv[2]
high_path = sys.argv[3]

def parse_step(path):
    with open(path, "r", encoding="utf-8") as f:
        root = json.load(f)

    step = root.get("step", {})
    return {
        "area": float(step.get("fireArea", 0.0) or 0.0),
        "burning": int(step.get("burningCellsCount", 0) or 0),
        "burned": int(step.get("burnedCellsCount", 0) or 0),
        "affected": int(step.get("totalCellsAffected", 0) or 0),
        "running": bool(root.get("isRunning", False)),
    }

low = parse_step(low_path)
mid = parse_step(mid_path)
high = parse_step(high_path)

print(f"LOW  humidity: area={low['area']:.0f}, burning={low['burning']}, burned={low['burned']}, affected={low['affected']}, running={low['running']}")
print(f"MID  humidity: area={mid['area']:.0f}, burning={mid['burning']}, burned={mid['burned']}, affected={mid['affected']}, running={mid['running']}")
print(f"HIGH humidity: area={high['area']:.0f}, burning={high['burning']}, burned={high['burned']}, affected={high['affected']}, running={high['running']}")

errors = []

if low["area"] < mid["area"]:
    errors.append(f"при низкой влажности area должна быть >= средней: {low['area']} < {mid['area']}")

if mid["area"] < high["area"]:
    errors.append(f"при средней влажности area должна быть >= высокой: {mid['area']} < {high['area']}")

if low["affected"] < mid["affected"]:
    errors.append(f"при низкой влажности affected должно быть >= средней: {low['affected']} < {mid['affected']}")

if mid["affected"] < high["affected"]:
    errors.append(f"при средней влажности affected должно быть >= высокой: {mid['affected']} < {high['affected']}")

if low["area"] <= high["area"] and low["affected"] <= high["affected"]:
    errors.append("высокая влажность не ослабила распространение по сравнению с низкой")

if errors:
    print("❌ Влажность воздуха не показывает ожидаемого runtime-эффекта")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ Более высокая влажность воздуха ослабляет распространение в полной симуляции")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"