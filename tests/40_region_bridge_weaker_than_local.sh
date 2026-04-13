#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"

echo "============================================================"
echo " ТЕСТ: межрегиональные мосты в среднем слабее локальных связей"
echo "============================================================"

TMP_DIR="$(mktemp -d)"
GRAPH_JSON="$TMP_DIR/graph.json"

SIM_ID=$(
curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"region-bridge-weaker-than-local",
    "description":"bridge weaker than local",
    "gridWidth":20,
    "gridHeight":20,
    "graphType":2,
    "initialMoistureMin":0.2,
    "initialMoistureMax":0.3,
    "elevationVariation":50,
    "initialFireCellsCount":1,
    "simulationSteps":1,
    "stepDurationSeconds":900,
    "randomSeed":55555,
    "temperature":25,
    "humidity":40,
    "windSpeed":0,
    "windDirection":0,
    "precipitation":0
  }' | jq -r '.id'
)

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json
import sys

graph_file = sys.argv[1]

with open(graph_file, "r", encoding="utf-8") as f:
    graph = json.load(f)["graph"]

nodes = {n["id"]: n for n in graph["nodes"]}
edges = graph["edges"]

local = []
bridge = []

for e in edges:
    a = nodes[e["fromCellId"]]
    b = nodes[e["toCellId"]]

    if a["groupKey"] == b["groupKey"]:
        local.append(e["fireSpreadModifier"])
    else:
        bridge.append(e["fireSpreadModifier"])

if not local or not bridge:
    print("❌ Недостаточно данных: нет local или bridge edges")
    sys.exit(1)

avg_local = sum(local) / len(local)
avg_bridge = sum(bridge) / len(bridge)

print(f"local_count   = {len(local)}")
print(f"bridge_count  = {len(bridge)}")
print(f"avg_local     = {avg_local:.6f}")
print(f"avg_bridge    = {avg_bridge:.6f}")

if avg_bridge < avg_local:
    print("✅ В среднем межрегиональные мосты слабее локальных связей")
    sys.exit(0)

print("❌ В среднем bridge edges не слабее local edges")
sys.exit(1)
PY