#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_RegionClusterGraph_start_inside_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: стартовый очаг RegionClusterGraph должен быть внутри кластера"
echo "============================================================"
echo ""

CREATE_JSON="$OUT_DIR/create.json"

curl -s -X POST "$API_URL/api/Simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "RegionClusterGraph start inside cluster",
    "description": "Start point should prefer cluster interior",
    "gridWidth": 20,
    "gridHeight": 20,
    "graphType": 2,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.20,
    "elevationVariation": 20.0,
    "initialFireCellsCount": 1,
    "simulationSteps": 10,
    "stepDurationSeconds": 1800,
    "randomSeed": 424242,
    "temperature": 32,
    "humidity": 35,
    "windSpeed": 8,
    "windDirection": 45,
    "precipitation": 0
  }' > "$CREATE_JSON"

SIM_ID=$(jq -r '.id' "$CREATE_JSON")
[[ -z "$SIM_ID" || "$SIM_ID" == "null" ]] && echo "❌ Ошибка создания" && cat "$CREATE_JSON" && exit 1

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start.json"
curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$OUT_DIR/graph.json"

SUCCESS=$(jq -r '.success // false' "$OUT_DIR/graph.json")
if [[ "$SUCCESS" != "true" ]]; then
    echo "❌ Не удалось получить граф"
    cat "$OUT_DIR/graph.json"
    exit 1
fi

START_NODE_ID=$(jq -r '
  .graph.nodes
  | map(select(.state == "Burning"))
  | .[0].id
' "$OUT_DIR/graph.json")

START_CLUSTER=$(jq -r '
  .graph.nodes
  | map(select(.state == "Burning"))
  | .[0].groupKey
' "$OUT_DIR/graph.json")

echo "startNodeId=$START_NODE_ID"
echo "startCluster=$START_CLUSTER"

INTER_CLUSTER_NEIGHBORS=$(jq -r --arg ID "$START_NODE_ID" '
  .graph as $g
  | $g.nodes as $nodes
  | ($nodes[] | select(.id == $ID) | .groupKey) as $startGroup
  | $g.edges
  | map(
      if .fromCellId == $ID then .toCellId
      elif .toCellId == $ID then .fromCellId
      else empty
      end
    )
  | map($nodes[] | select(.id == .))
  ' "$OUT_DIR/graph.json" 2>/dev/null || true)

INTER_CLUSTER_NEIGHBOR_COUNT=$(jq -r --arg ID "$START_NODE_ID" '
  .graph as $g
  | $g.nodes as $nodes
  | ($nodes[] | select(.id == $ID) | .groupKey) as $startGroup
  | $g.edges
  | map(
      if .fromCellId == $ID then .toCellId
      elif .toCellId == $ID then .fromCellId
      else empty
      end
    )
  | map(
      $nodes[] | select(.id == .)
    )
' "$OUT_DIR/graph.json" >/dev/null 2>&1 || true)

START_INTER_COUNT=$(jq -r --arg ID "$START_NODE_ID" '
  .graph as $g
  | $g.nodes as $nodes
  | ($nodes[] | select(.id == $ID) | .groupKey) as $startGroup
  | $g.edges
  | map(
      if .fromCellId == $ID then .toCellId
      elif .toCellId == $ID then .fromCellId
      else empty
      end
    )
  | map(
      . as $nid
      | $nodes[] | select(.id == $nid)
    )
  | map(select(.groupKey != $startGroup))
  | length
' "$OUT_DIR/graph.json")

START_TOTAL_DEGREE=$(jq -r --arg ID "$START_NODE_ID" '
  .graph.edges
  | map(select(.fromCellId == $ID or .toCellId == $ID))
  | length
' "$OUT_DIR/graph.json")

START_SAME_CLUSTER_DEGREE=$(jq -r --arg ID "$START_NODE_ID" '
  .graph as $g
  | $g.nodes as $nodes
  | ($nodes[] | select(.id == $ID) | .groupKey) as $startGroup
  | $g.edges
  | map(select(.fromCellId == $ID or .toCellId == $ID))
  | map(
      if .fromCellId == $ID then .toCellId else .fromCellId end
    )
  | map(
      . as $nid
      | $nodes[] | select(.id == $nid)
    )
  | map(select(.groupKey == $startGroup))
  | length
' "$OUT_DIR/graph.json")

echo "startTotalDegree=$START_TOTAL_DEGREE"
echo "startSameClusterDegree=$START_SAME_CLUSTER_DEGREE"
echo "startInterClusterDegree=$START_INTER_COUNT"

if [[ "$START_INTER_COUNT" -gt 0 ]]; then
    echo "❌ Стартовый очаг стоит прямо на межкластерной границе"
    exit 1
fi

if [[ "$START_SAME_CLUSTER_DEGREE" -lt 3 ]]; then
    echo "❌ У стартового очага слишком слабая локальная связность"
    exit 1
fi

echo "✅ Стартовый очаг выбран внутри кластера"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"