#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_precipitation_front_runtime_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: осадки локально повышают влажность и ослабляют пожар"
echo "============================================================"

run_case() {
  local label="$1"
  local precipitation="$2"

  local create_json="$OUT_DIR/${label}_create.json"
  local start_json="$OUT_DIR/${label}_start.json"
  local graph_json="$OUT_DIR/${label}_graph.json"
  local status_json="$OUT_DIR/${label}_status.json"

  create_grid_sim "$create_json" \
    "$label" \
    30 30 \
    0.20 0.20 \
    0 \
    30 900 \
    424242 \
    30 30 8 270 "$precipitation"

  local sim_id
  sim_id="$(get_sim_id "$create_json")"

  start_manual "$sim_id" "$start_json" 5 15
  run_steps "$sim_id" 16 "$OUT_DIR" "$label" >/dev/null
  fetch_status "$sim_id" "$status_json"
  fetch_graph "$sim_id" "$graph_json"

  echo "$graph_json|$status_json"
}

DRY_PAIR="$(run_case dry 0)"
RAIN_PAIR="$(run_case rain 100)"

DRY_GRAPH="${DRY_PAIR%%|*}"
DRY_STATUS="${DRY_PAIR##*|}"

RAIN_GRAPH="${RAIN_PAIR%%|*}"
RAIN_STATUS="${RAIN_PAIR##*|}"

python3 - "$DRY_GRAPH" "$RAIN_GRAPH" "$DRY_STATUS" "$RAIN_STATUS" <<'PY'
import json
import sys

dry_graph, rain_graph, dry_status, rain_status = sys.argv[1:5]

def nodes(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)["graph"]["nodes"]

def area(path):
    with open(path, "r", encoding="utf-8") as f:
        return float(json.load(f)["simulation"]["fireArea"])

def zone_moisture(ns, x1, x2, y1, y2):
    zone = [n for n in ns if x1 <= n["x"] <= x2 and y1 <= n["y"] <= y2]
    return sum(float(n.get("moisture") or 0.0) for n in zone) / max(1, len(zone)), len(zone)

dry_nodes = nodes(dry_graph)
rain_nodes = nodes(rain_graph)

dry_area = area(dry_status)
rain_area = area(rain_status)

rain_moisture, rain_count = zone_moisture(rain_nodes, 8, 17, 10, 20)
dry_moisture, dry_count = zone_moisture(dry_nodes, 8, 17, 10, 20)

print("dry_area =", dry_area)
print("rain_area =", rain_area)
print("dry_zone_moisture =", round(dry_moisture, 6))
print("rain_zone_moisture =", round(rain_moisture, 6))
print("zone_count =", rain_count)

if rain_count == 0 or dry_count == 0:
    print("❌ Не удалось выделить зону осадков")
    sys.exit(1)

if rain_moisture <= dry_moisture + 0.05:
    print("❌ Осадки не дали заметного роста влажности")
    sys.exit(1)

if rain_area > dry_area:
    print("❌ Осадки увеличили площадь пожара")
    sys.exit(1)

print("✅ Осадки повышают влажность и не усиливают пожар")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"