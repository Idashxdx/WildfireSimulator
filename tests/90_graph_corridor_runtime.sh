#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_graph_corridor_runtime_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: corridor переносит пожар между областями"
echo "============================================================"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"
NODE_D="44444444-4444-4444-4444-444444444444"

EDGE_AB="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"
EDGE_BC="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"
EDGE_CD="cccccccc-cccc-cccc-cccc-ccccccccccc3"

make_case() {
  local label="$1"
  local with_corridor="$2"
  local create_json="$OUT_DIR/${label}_create.json"
  local start_json="$OUT_DIR/${label}_start.json"
  local graph_json="$OUT_DIR/${label}_graph.json"

  local corridor_edge=""
  if [[ "$with_corridor" == "yes" ]]; then
    corridor_edge=", { \"id\": \"$EDGE_BC\", \"fromNodeId\": \"$NODE_B\", \"toNodeId\": \"$NODE_C\", \"distanceOverride\": 6.0, \"fireSpreadModifier\": 1.60 }"
  fi

  curl -sS -X POST "$API_URL/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$label\",
      \"description\": \"corridor runtime comparison\",
      \"graphType\": 1,
      \"graphScaleType\": 2,
      \"gridWidth\": 24,
      \"gridHeight\": 12,
      \"initialMoistureMin\": 0.10,
      \"initialMoistureMax\": 0.18,
      \"elevationVariation\": 8,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 10,
      \"stepDurationSeconds\": 1800,
      \"randomSeed\": 20260422,
      \"mapCreationMode\": 2,
      \"clusteredBlueprint\": {
        \"canvasWidth\": 24,
        \"canvasHeight\": 12,
        \"candidates\": [],
        \"nodes\": [
          { \"id\": \"$NODE_A\", \"x\": 3,  \"y\": 5, \"clusterId\": \"west\", \"vegetation\": 3, \"moisture\": 0.10, \"elevation\": 2.0 },
          { \"id\": \"$NODE_B\", \"x\": 5,  \"y\": 5, \"clusterId\": \"west\", \"vegetation\": 3, \"moisture\": 0.12, \"elevation\": 2.5 },
          { \"id\": \"$NODE_C\", \"x\": 17, \"y\": 5, \"clusterId\": \"east\", \"vegetation\": 3, \"moisture\": 0.10, \"elevation\": 2.0 },
          { \"id\": \"$NODE_D\", \"x\": 19, \"y\": 5, \"clusterId\": \"east\", \"vegetation\": 3, \"moisture\": 0.12, \"elevation\": 2.5 }
        ],
        \"edges\": [
          { \"id\": \"$EDGE_AB\", \"fromNodeId\": \"$NODE_A\", \"toNodeId\": \"$NODE_B\", \"distanceOverride\": 2.0, \"fireSpreadModifier\": 1.20 },
          { \"id\": \"$EDGE_CD\", \"fromNodeId\": \"$NODE_C\", \"toNodeId\": \"$NODE_D\", \"distanceOverride\": 2.0, \"fireSpreadModifier\": 1.20 }
          $corridor_edge
        ]
      },
      \"temperature\": 34,
      \"humidity\": 22,
      \"windSpeed\": 12,
      \"windDirection\": 90,
      \"precipitation\": 0
    }" > "$create_json"

  local sim_id
  sim_id="$(get_sim_id "$create_json")"

  start_manual "$sim_id" "$start_json" 3 5
  run_steps "$sim_id" 4 "$OUT_DIR" "$label" >/dev/null
  fetch_graph "$sim_id" "$graph_json"

  echo "$graph_json"
}

WITH_GRAPH="$(make_case with_corridor yes)"
WITHOUT_GRAPH="$(make_case without_corridor no)"

python3 - "$WITH_GRAPH" "$WITHOUT_GRAPH" <<'PY'
import json
import sys

with_path, without_path = sys.argv[1:3]

def metrics(path):
    with open(path, "r", encoding="utf-8") as f:
        nodes = json.load(f)["graph"]["nodes"]

    east = [
        n for n in nodes
        if (n.get("groupKey") or "") == "east"
    ]

    east_affected = [
        n for n in east
        if n.get("state") != "Normal" or float(n.get("accumulatedHeatJ") or 0.0) > 0.0
    ]

    east_burning = [
        n for n in east
        if n.get("state") == "Burning"
    ]

    total_affected = [
        n for n in nodes
        if n.get("state") != "Normal"
    ]

    return {
        "east_affected": len(east_affected),
        "east_burning": len(east_burning),
        "total_affected": len(total_affected),
    }

with_m = metrics(with_path)
without_m = metrics(without_path)

print("with_corridor =", with_m)
print("without_corridor =", without_m)

if with_m["east_affected"] <= without_m["east_affected"]:
    print("❌ Corridor не усилил перенос в восточную область")
    sys.exit(1)

if with_m["total_affected"] <= without_m["total_affected"]:
    print("❌ Corridor не увеличил общее распространение")
    sys.exit(1)

print("✅ Corridor реально усиливает межобластное распространение")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"