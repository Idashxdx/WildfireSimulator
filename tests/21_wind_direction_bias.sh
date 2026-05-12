#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_wind_direction_bias_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: направление ветра смещает накопленное тепло"
echo "============================================================"

run_case() {
  local label="$1"
  local wind_direction="$2"

  local create_json="$OUT_DIR/${label}_create.json"
  local start_json="$OUT_DIR/${label}_start.json"
  local graph_json="$OUT_DIR/${label}_graph.json"

  create_grid_sim "$create_json" \
    "$label" \
    25 25 \
    0.10 0.10 \
    0 \
    10 900 \
    20260422 \
    30 30 10 "$wind_direction" 0

  local sim_id
  sim_id="$(get_sim_id "$create_json")"

  start_manual "$sim_id" "$start_json" 12 12
  run_steps "$sim_id" 3 "$OUT_DIR" "$label" >/dev/null
  fetch_graph "$sim_id" "$graph_json"

  echo "$graph_json"
}

RIGHT_GRAPH="$(run_case right_wind 270)"
LEFT_GRAPH="$(run_case left_wind 90)"

python3 - "$RIGHT_GRAPH" "$LEFT_GRAPH" <<'PY'
import json
import sys

right_path, left_path = sys.argv[1:3]

def load(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)["graph"]["nodes"]

def analyze(nodes):
    by_xy = {(n["x"], n["y"]): n for n in nodes}

    right = [(13, 12), (14, 12), (13, 11), (13, 13)]
    left = [(11, 12), (10, 12), (11, 11), (11, 13)]

    def score(coords):
        total_heat = 0.0
        affected = 0
        for xy in coords:
            n = by_xy.get(xy)
            if not n:
                continue
            heat = float(n.get("accumulatedHeatJ") or 0.0)
            state = n.get("state")
            total_heat += heat
            if heat > 0 or state != "Normal":
                affected += 1
        return total_heat, affected

    return score(right), score(left)

right_nodes = load(right_path)
left_nodes = load(left_path)

(right_heat_right, right_aff_right), (right_heat_left, right_aff_left) = analyze(right_nodes)
(left_heat_right, left_aff_right), (left_heat_left, left_aff_left) = analyze(left_nodes)

print(f"right_wind_right_heat={right_heat_right:.3f}")
print(f"right_wind_left_heat ={right_heat_left:.3f}")
print(f"left_wind_right_heat ={left_heat_right:.3f}")
print(f"left_wind_left_heat  ={left_heat_left:.3f}")

if right_heat_right <= right_heat_left:
    print("❌ При ветре вправо правая зона не получила больше тепла")
    sys.exit(1)

if left_heat_left <= left_heat_right:
    print("❌ При ветре влево левая зона не получила больше тепла")
    sys.exit(1)

print("✅ Направление ветра корректно смещает тепловой перенос")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"