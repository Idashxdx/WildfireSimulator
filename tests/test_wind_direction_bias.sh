#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_wind_direction_bias_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ 11.6: направление ветра должно смещать распространение"
echo "============================================================"

STEP_COUNT=3

run_sim() {
  local wind_dir="$1"
  local prefix="$2"
  local create_json="$OUT_DIR/${prefix}_create.json"

  curl -s -X POST "$API_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"Wind bias test ${prefix}\",
      \"description\": \"Проверка влияния направления ветра\",
      \"gridWidth\": 25,
      \"gridHeight\": 25,
      \"initialMoistureMin\": 0.10,
      \"initialMoistureMax\": 0.10,
      \"elevationVariation\": 0.0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 10,
      \"stepDurationSeconds\": 900,
      \"temperature\": 30.0,
      \"humidity\": 30.0,
      \"windSpeed\": 10.0,
      \"windDirection\": ${wind_dir},
      \"precipitation\": 0.0
    }" > "$create_json"

  local sim_id
  sim_id=$(jq -r '.id' "$create_json")

  echo "$prefix = $sim_id" >&2

  curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d "{
      \"ignitionMode\": \"manual\",
      \"initialFirePositions\": [
        { \"x\": 12, \"y\": 12 }
      ]
    }" > /dev/null

  for step in $(seq 1 "$STEP_COUNT"); do
    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/step" > /dev/null
  done

  local graph_json="$OUT_DIR/${prefix}_graph.json"
  curl -s "$API_URL/api/SimulationManager/$sim_id/graph" > "$graph_json"

  echo "$graph_json"
}

RIGHT_GRAPH=$(run_sim 270 "right_wind")  # ветер вправо
LEFT_GRAPH=$(run_sim 90 "left_wind")     # ветер влево

python3 - "$RIGHT_GRAPH" "$LEFT_GRAPH" <<'PY'
import json
import sys

right_path = sys.argv[1]
left_path = sys.argv[2]

def load_nodes(path):
    with open(path, "r", encoding="utf-8") as f:
        root = json.load(f)
    nodes = root["graph"]["nodes"]
    return nodes

def analyze(nodes):
    node_map = {(n["x"], n["y"]): n for n in nodes}

    right_coords = [(13, 12), (14, 12), (13, 11), (13, 13)]
    left_coords = [(11, 12), (10, 12), (11, 11), (11, 13)]

    def heat_sum(coords):
        total = 0.0
        affected = 0

        for xy in coords:
            n = node_map.get(xy)
            if n is None:
                continue

            heat = float(n.get("accumulatedHeatJ") or 0.0)
            state = n.get("state")

            total += heat

            if heat > 0.0 or state != "Normal":
                affected += 1

        return total, affected

    right_total, right_affected = heat_sum(right_coords)
    left_total, left_affected = heat_sum(left_coords)

    return right_total, left_total, right_affected, left_affected

right_nodes = load_nodes(right_path)
left_nodes = load_nodes(left_path)

r_r, r_l, r_ar, r_al = analyze(right_nodes)
l_r, l_l, l_ar, l_al = analyze(left_nodes)

print(f"right_wind_right_avg_heat = {r_r:.3f}")
print(f"right_wind_left_avg_heat  = {r_l:.3f}")
print(f"left_wind_right_avg_heat  = {l_r:.3f}")
print(f"left_wind_left_avg_heat   = {l_l:.3f}")

print(f"right_wind_right_affected = {r_ar}")
print(f"right_wind_left_affected  = {r_al}")
print(f"left_wind_right_affected  = {l_ar}")
print(f"left_wind_left_affected   = {l_al}")

if r_r <= r_l:
    print("❌ При ветре вправо правая сторона не получила больше тепла")
    sys.exit(1)

if l_l <= l_r:
    print("❌ При ветре влево левая сторона не получила больше тепла")
    sys.exit(1)

print("✅ Направление ветра корректно влияет на распространение")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"