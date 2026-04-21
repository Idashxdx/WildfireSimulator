#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ: SmallGraph должен быть topology-first"
echo "============================================================"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-small-graph-topology",
    "description": "small graph topology-first profile",
    "gridWidth": 14,
    "gridHeight": 14,
    "graphType": 1,
    "graphScaleType": 0,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.40,
    "elevationVariation": 20,
    "initialFireCellsCount": 1,
    "simulationSteps": 5,
    "stepDurationSeconds": 900,
    "randomSeed": 424242,
    "temperature": 28,
    "humidity": 35,
    "windSpeed": 4,
    "windDirection": 45,
    "precipitation": 0
  }' > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать SmallGraph simulation"
  cat "$SIM_JSON"
  exit 1
fi

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json
import sys
from collections import defaultdict, deque

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

graph = data["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

if not (8 <= len(nodes) <= 20):
    print(f"❌ Для SmallGraph число узлов вне диапазона 8..20: {len(nodes)}")
    sys.exit(1)

id_to_node = {n["id"]: n for n in nodes}
adj = defaultdict(set)
degrees = defaultdict(int)
cross_edges = 0

for e in edges:
    a = e["fromCellId"]
    b = e["toCellId"]
    adj[a].add(b)
    adj[b].add(a)
    degrees[a] += 1
    degrees[b] += 1

    ga = id_to_node[a].get("groupKey") or ""
    gb = id_to_node[b].get("groupKey") or ""
    if ga != gb:
        cross_edges += 1

min_degree = min(degrees.values()) if degrees else 0
max_degree = max(degrees.values()) if degrees else 0
avg_degree = (sum(degrees.values()) / len(nodes)) if nodes else 0.0

print("node_count =", len(nodes))
print("edge_count =", len(edges))
print("min_degree =", min_degree)
print("max_degree =", max_degree)
print("avg_degree =", round(avg_degree, 3))
print("cross_edges =", cross_edges)

if min_degree < 1:
    print("❌ В SmallGraph есть изолированные вершины")
    sys.exit(1)

if max_degree > 4:
    print("❌ В SmallGraph слишком высокая локальная степень")
    sys.exit(1)

if avg_degree > 3.4:
    print("❌ SmallGraph получился слишком плотным")
    sys.exit(1)

if cross_edges > 3:
    print("❌ SmallGraph имеет слишком много межкластерных мостов")
    sys.exit(1)

# Проверка связности
start = nodes[0]["id"]
visited = set([start])
q = deque([start])

while q:
    v = q.popleft()
    for to in adj[v]:
        if to not in visited:
            visited.add(to)
            q.append(to)

if len(visited) != len(nodes):
    print("❌ SmallGraph не связен")
    sys.exit(1)

print("✅ SmallGraph соответствует topology-first профилю")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"