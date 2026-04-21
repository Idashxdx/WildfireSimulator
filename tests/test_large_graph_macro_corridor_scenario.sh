#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_large_graph_macro_corridor_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: LargeGraph должен быть macro / corridor scenario"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
GRAPH_JSON="$OUT_DIR/graph.json"

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Large macro corridor scenario test\",
    \"description\": \"Проверка macro/corridor scenario для LargeGraph\",
    \"graphType\": 1,
    \"graphScaleType\": 2,
    \"gridWidth\": 34,
    \"gridHeight\": 34,
    \"initialMoistureMin\": 0.10,
    \"initialMoistureMax\": 0.32,
    \"elevationVariation\": 55.0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 120,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 97531,
    \"mapCreationMode\": 1,
    \"clusteredScenarioType\": 2,
    \"temperature\": 29.0,
    \"humidity\": 30.0,
    \"windSpeed\": 7.0,
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
avg_degree = (2.0 * edge_count / node_count) if node_count else 0.0

avg_cross_distance = (
    sum(e["distance"] for e in cross_edges) / len(cross_edges)
    if cross_edges else 0.0
)
avg_same_distance = (
    sum(e["distance"] for e in same_edges) / len(same_edges)
    if same_edges else 0.0
)

avg_cross_modifier = (
    sum(e["fireSpreadModifier"] for e in cross_edges) / len(cross_edges)
    if cross_edges else 0.0
)
avg_same_modifier = (
    sum(e["fireSpreadModifier"] for e in same_edges) / len(same_edges)
    if same_edges else 0.0
)

xs = [n.get("renderX", 0.0) for n in nodes]
ys = [n.get("renderY", 0.0) for n in nodes]
bbox_area = (max(xs) - min(xs)) * (max(ys) - min(ys)) if xs and ys else 0.0

corridor_like_edges = [
    e for e in cross_edges
    if e["distance"] >= avg_same_distance * 1.35
]

cross_ratio = (len(cross_edges) / edge_count) if edge_count else 0.0

print(f"node_count = {node_count}")
print(f"edge_count = {edge_count}")
print(f"group_count = {group_count}")
print(f"avg_degree = {avg_degree:.3f}")
print(f"cross_edge_count = {len(cross_edges)}")
print(f"same_edge_count = {len(same_edges)}")
print(f"cross_ratio = {cross_ratio:.3f}")
print(f"avg_cross_distance = {avg_cross_distance:.3f}")
print(f"avg_same_distance = {avg_same_distance:.3f}")
print(f"avg_cross_modifier = {avg_cross_modifier:.3f}")
print(f"avg_same_modifier = {avg_same_modifier:.3f}")
print(f"corridor_like_edge_count = {len(corridor_like_edges)}")
print(f"bbox_area = {bbox_area:.1f}")

errors = []

if node_count < 120:
    errors.append(f"для LargeGraph ожидалось >=120 узлов, получено {node_count}")

if group_count < 4:
    errors.append(f"ожидалось минимум 4 macro-group, получено {group_count}")

if len(cross_edges) < 8:
    errors.append(f"ожидалось хотя бы 8 межзонных связей, получено {len(cross_edges)}")

if avg_cross_distance <= avg_same_distance * 1.5:
    errors.append(
        f"межзонные связи должны быть существенно длиннее локальных: cross={avg_cross_distance:.3f}, same={avg_same_distance:.3f}"
    )

if len(corridor_like_edges) < 8:
    errors.append(f"ожидалось минимум 8 corridor-like edge, получено {len(corridor_like_edges)}")

if bbox_area < 700:
    errors.append(f"пространственный охват слишком мал для LargeGraph: bbox_area={bbox_area:.1f}")

if avg_degree < 3.2:
    errors.append(f"связность LargeGraph слишком мала: avg_degree={avg_degree:.3f}")

if avg_cross_modifier >= avg_same_modifier:
    errors.append(
        f"межзонные связи должны быть слабее локальных: cross={avg_cross_modifier:.3f}, same={avg_same_modifier:.3f}"
    )

if errors:
    print("❌ LargeGraph не выглядит macro / corridor scenario")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ LargeGraph показывает macro sectors и corridor logic")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $OUT_DIR"
echo "============================================================"