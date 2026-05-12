#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_grid_weather_effects_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: влияние температуры, влажности воздуха, ветра и осадков"
echo "============================================================"
echo "OUT_DIR=$OUT_DIR"

run_case() {
  local label="$1"
  local temperature="$2"
  local humidity="$3"
  local wind="$4"
  local precipitation="$5"

  local create_json="$OUT_DIR/${label}_create.json"
  local start_json="$OUT_DIR/${label}_start.json"
  local status_json="$OUT_DIR/${label}_status.json"

  create_grid_sim "$create_json" \
    "$label" \
    25 25 \
    0.18 0.28 \
    20 \
    20 900 \
    20260422 \
    "$temperature" "$humidity" "$wind" 45 "$precipitation"

  local sim_id
  sim_id="$(get_sim_id "$create_json")"

  start_manual "$sim_id" "$start_json" 12 12
  run_steps "$sim_id" 8 "$OUT_DIR" "$label" >/dev/null

  fetch_status "$sim_id" "$status_json"

  local area burning burned
  area="$(status_area "$status_json")"
  burning="$(status_burning "$status_json")"
  burned="$(status_burned "$status_json")"

  echo "$label: area=$area burning=$burning burned=$burned" >&2
  echo "$area"
}

TEMP_LOW="$(run_case temp_low 20 40 5 0)"
TEMP_HIGH="$(run_case temp_high 40 40 5 0)"

HUM_LOW="$(run_case humidity_low 30 20 5 0)"
HUM_HIGH="$(run_case humidity_high 30 80 5 0)"

WIND_LOW="$(run_case wind_low 32 25 2 0)"
WIND_HIGH="$(run_case wind_high 32 25 14 0)"

DRY="$(run_case dry 31 25 7 0)"
RAIN="$(run_case rain 31 25 7 100)"

echo ""
echo "RESULT:"
echo "temp_low=$TEMP_LOW temp_high=$TEMP_HIGH"
echo "humidity_low=$HUM_LOW humidity_high=$HUM_HIGH"
echo "wind_low=$WIND_LOW wind_high=$WIND_HIGH"
echo "dry=$DRY rain=$RAIN"

assert_gt "$TEMP_HIGH" "$TEMP_LOW" "Высокая температура усиливает распространение"
assert_gt "$HUM_LOW" "$HUM_HIGH" "Низкая влажность воздуха даёт большее распространение"
assert_gt "$WIND_HIGH" "$WIND_LOW" "Сильный ветер усиливает распространение"
assert_ge "$DRY" "$RAIN" "Осадки не должны усиливать пожар"

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"