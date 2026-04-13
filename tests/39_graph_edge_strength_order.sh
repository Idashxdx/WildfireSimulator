#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"

echo "============================================================"
echo " ТЕСТ: сильные графовые связи должны быть не слабее слабых"
echo "============================================================"

TMP_DIR="$(mktemp -d)"
GRAPH_JSON="$TMP_DIR/graph.json"

SIM_ID=$(
curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"edge-strength-order-test",
    "description":"clustered edge strength order",
    "gridWidth":12,
    "gridHeight":12,
    "graphType":1,
    "initialMoistureMin":0.15,
    "initialMoistureMax":0.25,
    "elevationVariation":20,
    "initialFireCellsCount":1,
    "simulationSteps":3,
    "stepDurationSeconds":900,
    "randomSeed":424242,
    "temperature":30,
    "humidity":30,
    "windSpeed":0,
    "windDirection":0,
    "precipitation":0
  }' | jq -r '.id'
)

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

START_NODE=$(jq -r '
  .graph.nodes as $nodes
  | .graph.edges as $edges
  | $nodes[]
  | . as $n
  | [ $edges[] | select(.fromCellId == $n.id or .toCellId == $n.id) ] as $incident
  | select(($incident | length) >= 6)
  | "\(.x),\(.y)"
' "$GRAPH_JSON" | head -n 1)

if [[ -z "$START_NODE" ]]; then
  echo "❌ Не найден узел с достаточным числом соседей"
  exit 1
fi

START_X="$(echo "$START_NODE" | cut -d',' -f1)"
START_Y="$(echo "$START_NODE" | cut -d',' -f2)"

echo "source = ($START_X,$START_Y)"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{
    \"ignitionMode\":\"manual\",
    \"initialFirePositions\":[{\"x\":$START_X,\"y\":$START_Y}]
  }" >/dev/null

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" >/dev/null
curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" "$START_X" "$START_Y" << 'PY'
import json
import sys

graph_file, sx, sy = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])

with open(graph_file, "r", encoding="utf-8") as f:
    data = json.load(f)["graph"]

nodes = data["nodes"]
edges = data["edges"]

source = next(n for n in nodes if n["x"] == sx and n["y"] == sy)
source_id = source["id"]

neighbor_rows = []
for e in edges:
    if e["fromCellId"] == source_id:
        nid = e["toCellId"]
    elif e["toCellId"] == source_id:
        nid = e["fromCellId"]
    else:
        continue

    n = next(x for x in nodes if x["id"] == nid)
    neighbor_rows.append({
        "modifier": e["fireSpreadModifier"],
        "prob": n["burnProbability"],
        "state": n["state"],
        "x": n["x"],
        "y": n["y"]
    })

neighbor_rows.sort(key=lambda r: r["modifier"])
half = len(neighbor_rows) // 2

weak = neighbor_rows[:half]
strong = neighbor_rows[-half:] if half > 0 else neighbor_rows

def avg(arr, key):
    return sum(x[key] for x in arr) / len(arr) if arr else 0.0

weak_avg_modifier = avg(weak, "modifier")
strong_avg_modifier = avg(strong, "modifier")

weak_avg_prob = avg(weak, "prob")
strong_avg_prob = avg(strong, "prob")

print(f"neighbor_count = {len(neighbor_rows)}")
print(f"weak_avg_modifier = {weak_avg_modifier:.6f}")
print(f"strong_avg_modifier = {strong_avg_modifier:.6f}")
print(f"weak_avg_prob = {weak_avg_prob:.6f}")
print(f"strong_avg_prob = {strong_avg_prob:.6f}")

if strong_avg_modifier <= weak_avg_modifier:
    print("❌ strong modifiers are not stronger than weak ones")
    sys.exit(1)

if strong_avg_prob + 1e-12 < weak_avg_prob:
    print("❌ strong edges give lower average burnProbability than weak edges")
    sys.exit(1)

print("✅ strong edges are not weaker than weak edges")
PY