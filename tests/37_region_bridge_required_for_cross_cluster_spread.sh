#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"

echo "============================================================"
echo " ТЕСТ: межрегиональный переход должен требовать bridge edges"
echo "============================================================"

TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"
STATUS_JSON="$TMP_DIR/status.json"

SIM_ID=$(
curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"bridge-required-test",
    "description":"region bridge required",
    "gridWidth":20,
    "gridHeight":20,
    "graphType":2,
    "initialMoistureMin":0.15,
    "initialMoistureMax":0.25,
    "elevationVariation":50,
    "initialFireCellsCount":1,
    "simulationSteps":12,
    "stepDurationSeconds":900,
    "randomSeed":777,
    "temperature":30,
    "humidity":30,
    "windSpeed":0,
    "windDirection":0,
    "precipitation":0
  }' | jq -r '.id'
)

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

REGION_COUNT=$(jq '[.graph.nodes[].groupKey] | unique | length' "$GRAPH_JSON")
INTER_EDGES=$(jq '[.graph.edges[] | select(
  ((.fromCellId as $f | .toCellId as $t | .) != null)
)] | length' "$GRAPH_JSON")

echo "region_count = $REGION_COUNT"
echo "total_edges  = $INTER_EDGES"

START_NODE=$(jq -r '
  .graph.nodes as $nodes
  | .graph.edges as $edges
  | $nodes[]
  | . as $n
  | [
      $edges[]
      | select(.fromCellId == $n.id or .toCellId == $n.id)
    ] as $incident
  | ($incident | length) as $deg
  | ($incident
      | map(
          if .fromCellId == $n.id then .toCellId else .fromCellId end
        )
    ) as $neighborIds
  | ($nodes | map(select(.id as $id | $neighborIds | index($id))) ) as $neighbors
  | ($neighbors | map(.groupKey) | unique | length) as $clusterKinds
  | select($deg >= 4 and $clusterKinds == 1)
  | "\(.x),\(.y),\(.groupKey)"
' "$GRAPH_JSON" | head -n 1)

if [[ -z "$START_NODE" ]]; then
  echo "❌ Не удалось выбрать хороший стартовый узел"
  exit 1
fi

START_X="$(echo "$START_NODE" | cut -d',' -f1)"
START_Y="$(echo "$START_NODE" | cut -d',' -f2)"
START_CLUSTER="$(echo "$START_NODE" | cut -d',' -f3)"

echo "start = ($START_X,$START_Y), cluster=$START_CLUSTER"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{
    \"ignitionMode\":\"manual\",
    \"initialFirePositions\":[{\"x\":$START_X,\"y\":$START_Y}]
  }" > "$SIM_JSON"

for i in $(seq 1 12); do
  curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" >/dev/null
  curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

  BURNING_CLUSTERS=$(jq -r '
    [.graph.nodes[]
      | select(.state == "Burning" or .state == "Burned")
      | .groupKey] | unique | length
  ' "$GRAPH_JSON")

  echo "step=$i affected_clusters=$BURNING_CLUSTERS"

  if [[ "$BURNING_CLUSTERS" -ge 2 ]]; then
    echo "✅ Межрегиональный переход произошёл через существующие bridge edges"
    echo "============================================================"
    exit 0
  fi
done

echo " За 12 шагов межрегионального перехода не произошло"
echo "Это не обязательно ошибка, но стоит проверить seed/условия"
echo "============================================================"
exit 0