#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_clustered_blueprint_validation_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: clustered blueprint должен валидироваться и нормализоваться"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
GRAPH_JSON="$OUT_DIR/graph.json"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"
FAKE_NODE="99999999-9999-9999-9999-999999999999"

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Blueprint validation test\",
    \"description\": \"Проверка нормализации clustered blueprint\",
    \"graphType\": 1,
    \"graphScaleType\": 0,
    \"gridWidth\": 20,
    \"gridHeight\": 20,
    \"initialMoistureMin\": 0.20,
    \"initialMoistureMax\": 0.20,
    \"elevationVariation\": 20.0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 30,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 987654,
    \"mapCreationMode\": 2,
    \"clusteredBlueprint\": {
      \"canvasWidth\": 20,
      \"canvasHeight\": 20,
      \"candidates\": [],
      \"nodes\": [
        {
          \"id\": \"${NODE_A}\",
          \"x\": 4,
          \"y\": 4,
          \"clusterId\": \"manual-a\",
          \"vegetation\": 3,
          \"moisture\": 0.15,
          \"elevation\": 2.0
        },
        {
          \"id\": \"${NODE_B}\",
          \"x\": 8,
          \"y\": 4,
          \"clusterId\": \"manual-a\",
          \"vegetation\": 4,
          \"moisture\": 0.20,
          \"elevation\": 3.0
        },
        {
          \"id\": \"${NODE_C}\",
          \"x\": 16,
          \"y\": 15,
          \"clusterId\": \"manual-c\",
          \"vegetation\": 2,
          \"moisture\": 0.25,
          \"elevation\": 5.0
        }
      ],
      \"edges\": [
        {
          \"id\": \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1\",
          \"fromNodeId\": \"${NODE_A}\",
          \"toNodeId\": \"${NODE_B}\",
          \"distanceOverride\": 1.5,
          \"fireSpreadModifier\": 1.10
        },
        {
          \"id\": \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2\",
          \"fromNodeId\": \"${NODE_A}\",
          \"toNodeId\": \"${NODE_B}\",
          \"distanceOverride\": 1.5,
          \"fireSpreadModifier\": 1.10
        },
        {
          \"id\": \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3\",
          \"fromNodeId\": \"${NODE_B}\",
          \"toNodeId\": \"${NODE_A}\",
          \"distanceOverride\": 1.5,
          \"fireSpreadModifier\": 1.10
        },
        {
          \"id\": \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4\",
          \"fromNodeId\": \"${NODE_B}\",
          \"toNodeId\": \"${NODE_B}\",
          \"distanceOverride\": 1.0,
          \"fireSpreadModifier\": 0.80
        },
        {
          \"id\": \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5\",
          \"fromNodeId\": \"${NODE_A}\",
          \"toNodeId\": \"${FAKE_NODE}\",
          \"distanceOverride\": 2.0,
          \"fireSpreadModifier\": 0.90
        }
      ]
    },
    \"temperature\": 25.0,
    \"humidity\": 40.0,
    \"windSpeed\": 5.0,
    \"windDirection\": 45.0,
    \"precipitation\": 0.0
  }" > "$CREATE_JSON"

SIM_ID=$(jq -r '.id // empty' "$CREATE_JSON")

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$CREATE_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

if [[ ! -s "$GRAPH_JSON" ]]; then
  echo "❌ Graph endpoint вернул пустой ответ"
  exit 1
fi

GRAPH_SUCCESS=$(jq -r '.success // false' "$GRAPH_JSON" 2>/dev/null || echo "false")
if [[ "$GRAPH_SUCCESS" != "true" ]]; then
  echo "❌ Не удалось получить graph JSON"
  cat "$GRAPH_JSON"
  exit 1
fi

python3 - "$GRAPH_JSON" "$NODE_A" "$NODE_B" "$NODE_C" <<'PY'
import json
import sys
from collections import defaultdict

graph_path = sys.argv[1]
node_a = sys.argv[2]
node_b = sys.argv[3]
node_c = sys.argv[4]

with open(graph_path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

node_ids = {n["id"] for n in nodes}
degrees = defaultdict(int)
normalized_pairs = set()

for e in edges:
    a = e["fromCellId"]
    b = e["toCellId"]

    degrees[a] += 1
    degrees[b] += 1

    key = tuple(sorted((a, b)))
    normalized_pairs.add(key)

node_count = len(nodes)
edge_count = len(edges)

ab_edges = [
    e for e in edges
    if {e["fromCellId"], e["toCellId"]} == {node_a, node_b}
]

self_loops = [
    e for e in edges
    if e["fromCellId"] == e["toCellId"]
]

invalid_edges = [
    e for e in edges
    if e["fromCellId"] not in node_ids or e["toCellId"] not in node_ids
]

c_degree = degrees.get(node_c, 0)

print(f"node_count = {node_count}")
print(f"edge_count = {edge_count}")
print(f"ab_edge_count = {len(ab_edges)}")
print(f"self_loop_count = {len(self_loops)}")
print(f"invalid_edge_count = {len(invalid_edges)}")
print(f"node_c_degree = {c_degree}")
print(f"unique_undirected_pairs = {len(normalized_pairs)}")

errors = []

if node_count != 3:
    errors.append(f"ожидалось 3 узла после нормализации, получено {node_count}")

if len(ab_edges) != 1:
    errors.append(f"дубликаты A-B должны схлопнуться до одного ребра, сейчас {len(ab_edges)}")

if len(self_loops) != 0:
    errors.append(f"самосвязи должны быть удалены, сейчас {len(self_loops)}")

if len(invalid_edges) != 0:
    errors.append(f"рёбра на несуществующие узлы должны быть удалены, сейчас {len(invalid_edges)}")

if c_degree == 0:
    errors.append("изолированный узел C должен быть мягко подключён SoftCompleteBlueprintConnectivity")

if errors:
    print("❌ Blueprint нормализован некорректно")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ Blueprint корректно нормализуется и мягко достраивается")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"