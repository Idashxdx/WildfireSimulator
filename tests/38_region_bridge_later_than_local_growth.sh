#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"

echo "============================================================"
echo " ТЕСТ: локальный рост должен произойти раньше межрегионального"
echo "============================================================"

TMP_DIR="$(mktemp -d)"
GRAPH_JSON="$TMP_DIR/graph.json"

SIM_ID=$(
curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"region-local-first-test",
    "description":"local first then bridge",
    "gridWidth":20,
    "gridHeight":20,
    "graphType":2,
    "initialMoistureMin":0.15,
    "initialMoistureMax":0.25,
    "elevationVariation":50,
    "initialFireCellsCount":1,
    "simulationSteps":20,
    "stepDurationSeconds":900,
    "randomSeed":123456,
    "temperature":30,
    "humidity":30,
    "windSpeed":0,
    "windDirection":0,
    "precipitation":0
  }' | jq -r '.id'
)

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

START_NODE=$(
python3 - "$GRAPH_JSON" << 'PY'
import json
import sys

graph_file = sys.argv[1]

with open(graph_file, "r", encoding="utf-8") as f:
    graph = json.load(f)["graph"]

nodes = graph["nodes"]
edges = graph["edges"]

node_by_id = {n["id"]: n for n in nodes}

def ignitable(node):
    veg = (node.get("vegetation") or "").strip().lower()
    return veg not in ("water", "bare")

best = None
best_score = None

for n in nodes:
    if not ignitable(n):
        continue

    incident = [e for e in edges if e["fromCellId"] == n["id"] or e["toCellId"] == n["id"]]
    if len(incident) < 4:
        continue

    same_cluster_neighbors = []
    inter_cluster_neighbors = []

    for e in incident:
        other_id = e["toCellId"] if e["fromCellId"] == n["id"] else e["fromCellId"]
        other = node_by_id[other_id]

        if other.get("groupKey") == n.get("groupKey"):
            same_cluster_neighbors.append(other)
        else:
            inter_cluster_neighbors.append(other)

    ignitable_same_cluster = [x for x in same_cluster_neighbors if ignitable(x)]

    if len(inter_cluster_neighbors) != 0:
        continue

    if len(ignitable_same_cluster) < 3:
        continue

    score = (-len(ignitable_same_cluster), n["y"], n["x"])

    if best is None or score < best_score:
        best = n
        best_score = score

if best is None:
    sys.exit(1)

print(f'{best["x"]},{best["y"]},{best["groupKey"]}')
PY
) || {
  echo "❌ Не удалось выбрать хороший стартовый узел"
  exit 1
}

START_X="$(echo "$START_NODE" | cut -d',' -f1)"
START_Y="$(echo "$START_NODE" | cut -d',' -f2)"
START_CLUSTER="$(echo "$START_NODE" | cut -d',' -f3)"

echo "start = ($START_X,$START_Y)"
echo "start_cluster = $START_CLUSTER"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{
    \"ignitionMode\":\"manual\",
    \"initialFirePositions\":[{\"x\":$START_X,\"y\":$START_Y}]
  }" >/dev/null

FIRST_LOCAL_STEP=0
FIRST_CROSS_STEP=0

for i in $(seq 1 20); do
  curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" >/dev/null
  curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

  INSIDE=$(jq --arg c "$START_CLUSTER" '
    [.graph.nodes[]
      | select((.state == "Burning" or .state == "Burned") and .groupKey == $c)
    ] | length
  ' "$GRAPH_JSON")

  OUTSIDE=$(jq --arg c "$START_CLUSTER" '
    [.graph.nodes[]
      | select((.state == "Burning" or .state == "Burned") and .groupKey != $c)
    ] | length
  ' "$GRAPH_JSON")

  echo "step=$i inside=$INSIDE outside=$OUTSIDE"

  if [[ "$FIRST_LOCAL_STEP" -eq 0 && "$INSIDE" -ge 2 ]]; then
    FIRST_LOCAL_STEP="$i"
  fi

  if [[ "$FIRST_CROSS_STEP" -eq 0 && "$OUTSIDE" -ge 1 ]]; then
    FIRST_CROSS_STEP="$i"
  fi
done

echo "first_local_step = $FIRST_LOCAL_STEP"
echo "first_cross_step = $FIRST_CROSS_STEP"

if [[ "$FIRST_LOCAL_STEP" -eq 0 ]]; then
  echo "❌ Локальный рост так и не произошёл"
  exit 1
fi

if [[ "$FIRST_CROSS_STEP" -eq 0 ]]; then
  echo "✅ Межрегиональный переход не произошёл за лимит шагов, но локальный рост произошёл"
  exit 0
fi

if [[ "$FIRST_LOCAL_STEP" -lt "$FIRST_CROSS_STEP" ]]; then
  echo "✅ Сначала локальный рост, потом межрегиональный переход"
  exit 0
fi

echo "❌ Межрегиональный переход случился слишком рано"
exit 1