#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_water_bare_barrier_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: Water и Bare не должны загораться"
echo "============================================================"

run_case() {
  local label="$1"
  local vegetation="$2"

  local create_json="$OUT_DIR/${label}_create.json"
  local start_json="$OUT_DIR/${label}_start.json"
  local graph_json="$OUT_DIR/${label}_graph.json"

  curl -sS -X POST "$API_URL/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$label\",
      \"description\": \"non ignitable runtime test\",
      \"graphType\": 0,
      \"gridWidth\": 12,
      \"gridHeight\": 12,
      \"initialMoistureMin\": 0.30,
      \"initialMoistureMax\": 0.30,
      \"elevationVariation\": 0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 5,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 12345,
      \"temperature\": 35,
      \"humidity\": 20,
      \"windSpeed\": 12,
      \"windDirection\": 45,
      \"precipitation\": 0,
      \"vegetationDistributions\": [
        { \"vegetationType\": $vegetation, \"probability\": 1.0 }
      ]
    }" > "$create_json"

  local sim_id
  sim_id="$(get_sim_id "$create_json")"

  start_manual "$sim_id" "$start_json" 6 6
  fetch_graph "$sim_id" "$graph_json"

  python3 - "$graph_json" "$label" <<'PY'
import json
import sys

path, label = sys.argv[1:3]
with open(path, "r", encoding="utf-8") as f:
    nodes = json.load(f)["graph"]["nodes"]

burning = [n for n in nodes if n["state"] == "Burning"]
burned = [n for n in nodes if n["state"] == "Burned"]

print(f"{label}_burning={len(burning)}")
print(f"{label}_burned={len(burned)}")

if burning or burned:
    print(f"❌ {label}: негорючий тип загорелся")
    sys.exit(1)

print(f"✅ {label}: не загорается")
PY
}

run_case "water" 5
run_case "bare" 6

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"