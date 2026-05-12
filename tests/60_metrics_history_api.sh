#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_metrics_history_api_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: FireMetrics history API"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
START_JSON="$OUT_DIR/start.json"
METRICS_JSON="$OUT_DIR/metrics.json"

create_grid_sim "$CREATE_JSON" \
  "metrics-history-api" \
  20 20 \
  0.20 0.30 \
  20 \
  10 900 \
  123456 \
  28 35 5 45 0

SIM_ID="$(get_sim_id "$CREATE_JSON")"

start_manual "$SIM_ID" "$START_JSON" 10 10
run_steps "$SIM_ID" 3 "$OUT_DIR" "metrics" >/dev/null

curl -sS "$API_URL/simulations/$SIM_ID/metrics" > "$METRICS_JSON"

python3 - "$METRICS_JSON" "$SIM_ID" <<'PY'
import json
import sys

path, expected_id = sys.argv[1:3]

with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

if isinstance(data, list):
    metrics = data
else:
    if not data.get("success", False):
        print("❌ success=false")
        sys.exit(1)
    metrics = data.get("metrics", [])

print("metrics_count =", len(metrics))

if len(metrics) < 3:
    print("❌ После 3 шагов ожидалось минимум 3 записи метрик")
    sys.exit(1)

steps = [m.get("step") for m in metrics]
print("steps =", steps)

if steps != sorted(steps):
    print("❌ Метрики не отсортированы по шагам")
    sys.exit(1)

required = [
    "simulationId",
    "step",
    "timestamp",
    "burningCellsCount",
    "burnedCellsCount",
    "totalCellsAffected",
    "fireSpreadSpeed",
    "averageTemperature",
    "averageWindSpeed",
    "fireArea"
]

for i, item in enumerate(metrics):
    for field in required:
        if field not in item:
            print(f"❌ В metrics[{i}] нет поля {field}")
            sys.exit(1)

print("first_metric =", metrics[0])
print("last_metric =", metrics[-1])
print("✅ Метрики пишутся и читаются")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"