#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ: LargeGraph должен иметь macro-zones и corridor-like связи"
echo "============================================================"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-large-graph-corridor-logic",
    "description": "large graph corridor logic",
    "gridWidth": 30,
    "gridHeight": 30,
    "graphType": 1,
    "graphScaleType": 2,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.70,
    "elevationVariation": 55,
    "initialFireCellsCount": 1,
    "simulationSteps": 5,
    "stepDurationSeconds": 900,
    "randomSeed": 424242,
    "temperature": 27,
    "humidity": 38,
    "windSpeed": 5,
    "windDirection": 45,
    "precipitation": 0
  }' > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать LargeGraph simulation"
  cat "$SIM_JSON"
  exit 1
fi

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json, math, sys
from collections import defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

graph = data["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

if len(nodes) < 80:
    print(f"❌ LargeGraph слишком мал по числу узлов: {len(nodes)}")
    sys.exit(1)

id_to_node = {n["id"]: n for n in nodes}
group_sizes = defaultdict(int)
degrees = defaultdict(int)

cross_edges = []
same_edges = []

for e in edges:
    a = id_to_node[e["fromCellId"]]
    b = id_to_node[e["toCellId"]]
    ga = a.get("groupKey") or ""
    gb = b.get("groupKey") or ""

    degrees[a["id"]] += 1
    degrees[b["id"]] += 1

    record = {
        "distance": float(e["distance"]),
        "modifier": float(e["fireSpreadModifier"]),
        "cross": ga != gb
    }

    if ga != gb:
        cross_edges.append(record)
    else:
        same_edges.append(record)

for n in nodes:
    group_sizes[n.get("groupKey") or ""] += 1

avg_degree = sum(degrees.values()) / len(nodes) if nodes else 0.0
avg_cross_distance = sum(e["distance"] for e in cross_edges) / len(cross_edges) if cross_edges else 0.0
avg_same_distance = sum(e["distance"] for e in same_edges) / len(same_edges) if same_edges else 0.0
avg_cross_modifier = sum(e["modifier"] for e in cross_edges) / len(cross_edges) if cross_edges else 0.0
avg_same_modifier = sum(e["modifier"] for e in same_edges) / len(same_edges) if same_edges else 0.0

xs = [n["x"] for n in nodes]
ys = [n["y"] for n in nodes]
bbox_area = (max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1)

large_groups = sum(1 for size in group_sizes.values() if size >= 8)

print("node_count =", len(nodes))
print("edge_count =", len(edges))
print("group_count =", len(group_sizes))
print("large_groups_count =", large_groups)
print("avg_degree =", round(avg_degree, 3))
print("cross_edge_count =", len(cross_edges))
print("avg_cross_distance =", round(avg_cross_distance, 3))
print("avg_same_distance =", round(avg_same_distance, 3))
print("avg_cross_modifier =", round(avg_cross_modifier, 3))
print("avg_same_modifier =", round(avg_same_modifier, 3))
print("bbox_area =", bbox_area)

if len(group_sizes) < 4:
    print("❌ LargeGraph имеет слишком мало макрозон")
    sys.exit(1)

if large_groups < 3:
    print("❌ LargeGraph не содержит достаточно крупных зон")
    sys.exit(1)

if len(cross_edges) < 4:
    print("❌ LargeGraph имеет слишком мало межзонных corridor-like связей")
    sys.exit(1)

if avg_cross_distance <= avg_same_distance:
    print("❌ Межзонные связи не длиннее локальных — corridor логика не видна")
    sys.exit(1)

if avg_cross_modifier >= avg_same_modifier:
    print("❌ Межзонные связи не ослаблены относительно локальных")
    sys.exit(1)

print("✅ LargeGraph показывает macro-structure и corridor logic")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"