#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_graph_modifier_memory_runtime_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: FireSpreadModifier + EdgeMemory в runtime"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
START_JSON="$OUT_DIR/start.json"
GRAPH0="$OUT_DIR/graph0.json"
GRAPH1="$OUT_DIR/graph1.json"
GRAPH2="$OUT_DIR/graph2.json"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"
EDGE_AB="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"
EDGE_AC="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"

curl -sS -X POST "$API_URL/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"modifier-memory-runtime\",
    \"description\": \"manual graph modifier and memory test\",
    \"graphType\": 1,
    \"graphScaleType\": 2,
    \"gridWidth\": 20,
    \"gridHeight\": 10,
    \"initialMoistureMin\": 0.12,
    \"initialMoistureMax\": 0.12,
    \"elevationVariation\": 0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 8,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 20260422,
    \"mapCreationMode\": 2,
    \"clusteredBlueprint\": {
      \"canvasWidth\": 20,
      \"canvasHeight\": 10,
      \"candidates\": [],
      \"nodes\": [
        { \"id\": \"$NODE_A\", \"x\": 3,  \"y\": 5, \"clusterId\": \"alpha\", \"vegetation\": 3, \"moisture\": 0.12, \"elevation\": 2.0 },
        { \"id\": \"$NODE_B\", \"x\": 8,  \"y\": 4, \"clusterId\": \"alpha\", \"vegetation\": 3, \"moisture\": 0.12, \"elevation\": 2.0 },
        { \"id\": \"$NODE_C\", \"x\": 8,  \"y\": 6, \"clusterId\": \"alpha\", \"vegetation\": 3, \"moisture\": 0.12, \"elevation\": 2.0 }
      ],
      \"edges\": [
        { \"id\": \"$EDGE_AB\", \"fromNodeId\": \"$NODE_A\", \"toNodeId\": \"$NODE_B\", \"distanceOverride\": 3.0, \"fireSpreadModifier\": 1.50 },
        { \"id\": \"$EDGE_AC\", \"fromNodeId\": \"$NODE_A\", \"toNodeId\": \"$NODE_C\", \"distanceOverride\": 3.0, \"fireSpreadModifier\": 0.40 }
      ]
    },
    \"temperature\": 30,
    \"humidity\": 28,
    \"windSpeed\": 0,
    \"windDirection\": 90,
    \"precipitation\": 0
  }" > "$CREATE_JSON"

SIM_ID="$(get_sim_id "$CREATE_JSON")"

fetch_graph "$SIM_ID" "$GRAPH0"
start_manual "$SIM_ID" "$START_JSON" 3 5
run_steps "$SIM_ID" 1 "$OUT_DIR" "modifier_memory" >/dev/null
fetch_graph "$SIM_ID" "$GRAPH1"
run_steps "$SIM_ID" 1 "$OUT_DIR" "modifier_memory_2" >/dev/null
fetch_graph "$SIM_ID" "$GRAPH2"

python3 - "$GRAPH0" "$GRAPH1" "$GRAPH2" <<'PY'
import json
import sys

g0_path, g1_path, g2_path = sys.argv[1:4]

def load(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)["graph"]

g0 = load(g0_path)
g1 = load(g1_path)
g2 = load(g2_path)

nodes1 = {(n["x"], n["y"]): n for n in g1["nodes"]}
nodes2 = {(n["x"], n["y"]): n for n in g2["nodes"]}

strong1 = nodes1[(8,4)]
weak1 = nodes1[(8,6)]
strong2 = nodes2[(8,4)]
weak2 = nodes2[(8,6)]

print("strong_probability_step1 =", strong1["burnProbability"])
print("weak_probability_step1   =", weak1["burnProbability"])
print("strong_state_step2       =", strong2["state"])
print("weak_state_step2         =", weak2["state"])

edges1 = g1["edges"]
edges2 = g2["edges"]

mem1 = sum(float(e.get("accumulatedHeat") or 0.0) for e in edges1)
mem2 = sum(float(e.get("accumulatedHeat") or 0.0) for e in edges2)

print("edge_memory_step1 =", mem1)
print("edge_memory_step2 =", mem2)

if float(strong1["burnProbability"]) <= float(weak1["burnProbability"]):
    print("❌ Сильное ребро не дало большую вероятность")
    sys.exit(1)

if mem1 <= 0:
    print("❌ Edge memory не накопилась после первого шага")
    sys.exit(1)

print("✅ FireSpreadModifier влияет на вероятность, EdgeMemory накапливается")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"