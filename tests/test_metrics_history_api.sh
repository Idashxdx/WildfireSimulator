#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
START_JSON="$TMP_DIR/start.json"
STEP1_JSON="$TMP_DIR/step1.json"
STEP2_JSON="$TMP_DIR/step2.json"
METRICS_JSON="$TMP_DIR/metrics.json"

echo "============================================================"
echo " ТЕСТ 11.8: FireMetrics history должен писаться и читаться через API"
echo "============================================================"

create_payload='{
  "name": "test-metrics-history-api",
  "description": "metrics history api test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 0,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 900,
  "randomSeed": 123456,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45,
  "precipitation": 0
}'

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$create_payload" > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d '{"ignitionMode":"saved-or-random"}' > "$START_JSON"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/step" > "$STEP1_JSON"
curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/step" > "$STEP2_JSON"

curl -sS "$BASE_URL/api/simulations/$SIM_ID/metrics" > "$METRICS_JSON"

python3 - "$METRICS_JSON" << 'PY'
import json, sys

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

if not isinstance(data, list):
    print("❌ API вернул не список метрик")
    sys.exit(1)

count = len(data)
print("metrics_count =", count)

if count < 2:
    print("❌ Ожидалось минимум 2 записи метрик после двух шагов")
    sys.exit(1)

steps = [m["step"] for m in data]
print("steps =", steps)

if steps != sorted(steps):
    print("❌ Метрики пришли не в порядке шагов")
    sys.exit(1)

required_fields = [
    "simulationId",
    "step",
    "timestamp",
    "burningCellsCount",
    "burnedCellsCount",
    "totalCellsAffected",
    "fireSpreadSpeed",
    "averageTemperature",
    "averageWindSpeed"
]

for i, item in enumerate(data):
    for field in required_fields:
        if field not in item:
            print(f"❌ В метрике #{i} отсутствует поле {field}")
            sys.exit(1)

print("first_metric =", data[0])
print("last_metric  =", data[-1])

print("✅ История FireMetrics корректно читается через API")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.8 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"