#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_medium_graph_barrier_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: MediumGraph должен быть barrier / patch scenario"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
GRAPH_JSON="$OUT_DIR/graph.json"

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Medium barrier scenario test\",
    \"description\": \"Проверка barrier scenario для MediumGraph\",
    \"graphType\": 1,
    \"graphScaleType\": 1,
    \"gridWidth\": 24,
    \"gridHeight\": 24,
    \"initialMoistureMin\": 0.18,
    \"initialMoistureMax\": 0.42,
    \"elevationVariation\": 35.0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 80,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 135791,
    \"mapCreationMode\": 1,
    \"clusteredScenarioType\": 1,
    \"temperature\": 26.0,
    \"humidity\": 42.0,
    \"windSpeed\": 5.0,
    \"windDirection\": 90.0,
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

water_nodes = [n for n in nodes if n.get("vegetation") == "Water"]

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
avg_degree = (2.0 * edge_count / node_count) if node_count else 0.0

avg_cross_modifier = (
    sum(e["fireSpreadModifier"] for e in cross_edges) / len(cross_edges)
    if cross_edges else 0.0
)
avg_same_modifier = (
    sum(e["fireSpreadModifier"] for e in same_edges) / len(same_edges)
    if same_edges else 0.0
)

avg_water_moisture = (
    sum(n.get("moisture", 0.0) for n in water_nodes) / len(water_nodes)
    if water_nodes else 0.0
)

print(f"node_count = {node_count}")
print(f"edge_count = {edge_count}")
print(f"group_count = {group_count}")
print(f"water_node_count = {len(water_nodes)}")
print(f"cross_edge_count = {cross_edge_count}")
print(f"avg_degree = {avg_degree:.3f}")
print(f"avg_cross_modifier = {avg_cross_modifier:.3f}")
print(f"avg_same_modifier = {avg_same_modifier:.3f}")
print(f"avg_water_moisture = {avg_water_moisture:.3f}")

errors = []

if not (20 <= node_count <= 100):
    errors.append(f"ожидалось 20..100 узлов, получено {node_count}")

if group_count < 2:
    errors.append(f"ожидалось минимум 2 группы, получено {group_count}")

if len(water_nodes) < 2:
    errors.append(f"ожидалось минимум 2 водных узла, получено {len(water_nodes)}")

if cross_edge_count < 1:
    errors.append("ожидались межкластерные связи")

if avg_cross_modifier >= avg_same_modifier:
    errors.append(
        f"межкластерные связи должны быть слабее локальных: cross={avg_cross_modifier:.3f}, same={avg_same_modifier:.3f}"
    )

if avg_water_moisture < 0.95:
    errors.append(f"водные узлы должны быть почти полностью влажными: {avg_water_moisture:.3f}")

if errors:
    print("❌ MediumGraph не выглядит barrier / patch scenario")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ MediumGraph показывает barrier / patch scenario")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $OUT_DIR"
echo "============================================================"