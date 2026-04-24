#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ: MediumGraph должен иметь отдельные области и мосты"
echo "============================================================"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-medium-graph-areas-bridges",
    "description": "medium graph areas and bridges profile",
    "gridWidth": 24,
    "gridHeight": 24,
    "graphType": 1,
    "graphScaleType": 1,
    "initialMoistureMin": 0.25,
    "initialMoistureMax": 0.55,
    "elevationVariation": 40,
    "initialFireCellsCount": 1,
    "simulationSteps": 8,
    "stepDurationSeconds": 900,
    "randomSeed": 424242,
    "temperature": 26,
    "humidity": 40,
    "windSpeed": 5,
    "windDirection": 45,
    "precipitation": 0
  }' > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать MediumGraph simulation"
  cat "$SIM_JSON"
  exit 1
fi

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json
import sys
from collections import Counter, defaultdict, deque

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

node_by_id = {n["id"]: n for n in nodes}
groups = Counter((n.get("groupKey") or "ungrouped") for n in nodes)

same_edges = []
cross_edges = []
adj = defaultdict(set)

for e in edges:
    a = node_by_id[e["fromCellId"]]
    b = node_by_id[e["toCellId"]]

    adj[a["id"]].add(b["id"])
    adj[b["id"]].add(a["id"])

    if (a.get("groupKey") or "") == (b.get("groupKey") or ""):
        same_edges.append(e)
    else:
        cross_edges.append(e)

node_count = len(nodes)
edge_count = len(edges)
group_count = len(groups)
avg_degree = 2.0 * edge_count / node_count if node_count else 0.0

avg_cross_distance = sum(e["distance"] for e in cross_edges) / len(cross_edges) if cross_edges else 0.0
avg_same_distance = sum(e["distance"] for e in same_edges) / len(same_edges) if same_edges else 0.0

print("node_count =", node_count)
print("edge_count =", edge_count)
print("group_count =", group_count)
print("groups =", dict(groups))
print("same_edges =", len(same_edges))
print("cross_edges =", len(cross_edges))
print("avg_degree =", round(avg_degree, 3))
print("avg_cross_distance =", round(avg_cross_distance, 3))
print("avg_same_distance =", round(avg_same_distance, 3))

if not (45 <= node_count <= 80):
    print("❌ MediumGraph должен иметь примерно 45..80 узлов")
    sys.exit(1)

if group_count < 2:
    print("❌ MediumGraph должен иметь несколько областей")
    sys.exit(1)

if len(cross_edges) < 1:
    print("❌ MediumGraph должен иметь мосты между областями")
    sys.exit(1)

if len(same_edges) <= len(cross_edges):
    print("❌ В MediumGraph локальных связей должно быть больше, чем мостов")
    sys.exit(1)

if avg_cross_distance <= avg_same_distance:
    print("❌ Межобластные мосты должны быть в среднем длиннее локальных связей")
    sys.exit(1)

start = nodes[0]["id"]
visited = {start}
q = deque([start])

while q:
    v = q.popleft()
    for to in adj[v]:
        if to not in visited:
            visited.add(to)
            q.append(to)

if len(visited) != len(nodes):
    print("❌ MediumGraph должен быть связным через мосты")
    sys.exit(1)

print("✅ MediumGraph соответствует новой структуре: области + мосты")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"