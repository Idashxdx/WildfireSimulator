#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_small_graph_bridge_critical_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: SmallGraph должен быть bridge-critical scenario"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
GRAPH_JSON="$OUT_DIR/graph.json"

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Small bridge-critical scenario test\",
    \"description\": \"Проверка сценария critical bridge для SmallGraph\",
    \"graphType\": 1,
    \"graphScaleType\": 0,
    \"gridWidth\": 20,
    \"gridHeight\": 20,
    \"initialMoistureMin\": 0.10,
    \"initialMoistureMax\": 0.22,
    \"elevationVariation\": 20.0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 50,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 246810,
    \"mapCreationMode\": 1,
    \"clusteredScenarioType\": 0,
    \"temperature\": 30.0,
    \"humidity\": 28.0,
    \"windSpeed\": 6.0,
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

python3 - "$GRAPH_JSON" <<'PY'
import json
import sys
from collections import Counter, defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

node_by_id = {n["id"]: n for n in nodes}
group_counter = Counter((n.get("groupKey") or "ungrouped") for n in nodes)

cross_edges = []
same_edges = []

for e in edges:
    a = node_by_id[e["fromCellId"]]
    b = node_by_id[e["toCellId"]]
    if (a.get("groupKey") or "ungrouped") != (b.get("groupKey") or "ungrouped"):
        cross_edges.append(e)
    else:
        same_edges.append(e)

degrees = defaultdict(int)
for e in edges:
    degrees[e["fromCellId"]] += 1
    degrees[e["toCellId"]] += 1

node_count = len(nodes)
edge_count = len(edges)
group_count = len([g for g in group_counter if g != "ungrouped"])
cross_edge_count = len(cross_edges)
min_degree = min(degrees.values()) if degrees else 0
max_degree = max(degrees.values()) if degrees else 0
avg_degree = (2.0 * edge_count / node_count) if node_count else 0.0

avg_cross_modifier = (
    sum(e["fireSpreadModifier"] for e in cross_edges) / len(cross_edges)
    if cross_edges else 0.0
)
avg_same_modifier = (
    sum(e["fireSpreadModifier"] for e in same_edges) / len(same_edges)
    if same_edges else 0.0
)

print(f"node_count = {node_count}")
print(f"edge_count = {edge_count}")
print(f"group_count = {group_count}")
print(f"groups = {dict(group_counter)}")
print(f"cross_edge_count = {cross_edge_count}")
print(f"same_edge_count = {len(same_edges)}")
print(f"min_degree = {min_degree}")
print(f"max_degree = {max_degree}")
print(f"avg_degree = {avg_degree:.3f}")
print(f"avg_cross_modifier = {avg_cross_modifier:.3f}")
print(f"avg_same_modifier = {avg_same_modifier:.3f}")

errors = []

if not (8 <= node_count <= 24):
    errors.append(f"ожидалось 8..24 узлов, получено {node_count}")

if group_count < 2:
    errors.append(f"ожидалось минимум 2 группы, получено {group_count}")

if cross_edge_count < 1:
    errors.append("ожидался хотя бы один bridge между группами")

if cross_edge_count > 3:
    errors.append(f"для bridge-critical сценария межгрупповых связей слишком много: {cross_edge_count}")

if max_degree > 4:
    errors.append(f"для SmallGraph max_degree слишком велик: {max_degree}")

if avg_degree > 3.2:
    errors.append(f"для SmallGraph avg_degree слишком велик: {avg_degree:.3f}")

if errors:
    print("❌ SmallGraph не выглядит bridge-critical scenario")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ SmallGraph показывает bridge-critical topology")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $OUT_DIR"
echo "============================================================"