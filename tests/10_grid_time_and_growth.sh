#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_grid_time_and_growth_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: Grid рост площади + независимость от stepDurationSeconds"
echo "============================================================"
echo "OUT_DIR=$OUT_DIR"

REAL_TIME_SECONDS=10800
DT_VALUES=(300 600 900 1800 3600 5400)

declare -A AREA
declare -A BURNING
declare -A BURNED

for dt in "${DT_VALUES[@]}"; do
  steps=$((REAL_TIME_SECONDS / dt))

  create_json="$OUT_DIR/create_dt_${dt}.json"
  start_json="$OUT_DIR/start_dt_${dt}.json"
  status_json="$OUT_DIR/status_dt_${dt}.json"

  create_grid_sim "$create_json" \
    "grid-dt-${dt}" \
    15 15 \
    0.20 0.20 \
    20 \
    "$steps" "$dt" \
    424242 \
    30 40 8 45 0

  sim_id="$(get_sim_id "$create_json")"
  echo ""
  echo "dt=$dt sim_id=$sim_id steps=$steps"

  start_manual "$sim_id" "$start_json" 7 7
  run_steps "$sim_id" "$steps" "$OUT_DIR" "dt_${dt}" >/dev/null

  fetch_status "$sim_id" "$status_json"

  AREA["$dt"]="$(status_area "$status_json")"
  BURNING["$dt"]="$(status_burning "$status_json")"
  BURNED["$dt"]="$(status_burned "$status_json")"

  echo "area=${AREA[$dt]} burning=${BURNING[$dt]} burned=${BURNED[$dt]}"
done

python3 - "${DT_VALUES[@]}" -- \
  "${AREA[300]}" "${AREA[600]}" "${AREA[900]}" "${AREA[1800]}" "${AREA[3600]}" "${AREA[5400]}" <<'PY'
import sys, statistics

sep = sys.argv.index("--")
dts = [int(x) for x in sys.argv[1:sep]]
areas = [float(x) for x in sys.argv[sep+1:]]

median = statistics.median(areas)
max_dev = max(abs(a - median) / max(1.0, median) * 100.0 for a in areas)

print("")
print("ИТОГ:")
for dt, area in zip(dts, areas):
    print(f"dt={dt:4d} area={area:.1f}")

print(f"median_area={median:.1f}")
print(f"max_deviation={max_dev:.2f}%")

if median < 15:
    print("❌ Пожар слишком слабый за 3 часа")
    sys.exit(1)

if median > 40:
    print("❌ Пожар слишком агрессивный за 3 часа")
    sys.exit(1)

if max_dev > 5:
    print("❌ Результат слишком зависит от stepDurationSeconds")
    sys.exit(1)

print("✅ Grid: рост реалистичный, stepDurationSeconds не ломает физику")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"