#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ 11.2: RegionClusterGraph должен быть связным"
echo "============================================================"

create_payload='{
  "name": "test-region-cluster-connected",
  "description": "connectivity test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 2,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 900,
  "randomSeed": 20260408,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45,
  "precipitation": 0
}'

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$create_payload" > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json
import sys
from collections import defaultdict, deque

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

graph = data.get("graph")
if not graph:
    print("❌ В ответе нет graph")
    sys.exit(1)

nodes = graph["nodes"]
edges = graph["edges"]

if not nodes:
    print("❌ Граф пустой")
    sys.exit(1)

adj = defaultdict(set)
node_ids = set()

for n in nodes:
    node_ids.add(n["id"])

for e in edges:
    a = e["fromCellId"]
    b = e["toCellId"]
    adj[a].add(b)
    adj[b].add(a)

start = nodes[0]["id"]
visited = set([start])
q = deque([start])

while q:
    cur = q.popleft()
    for nxt in adj[cur]:
        if nxt not in visited:
            visited.add(nxt)
            q.append(nxt)

component_count = 0
remaining = set(node_ids)

while remaining:
    component_count += 1
    seed = next(iter(remaining))
    dq = deque([seed])
    local = {seed}
    remaining.remove(seed)

    while dq:
        cur = dq.popleft()
        for nxt in adj[cur]:
            if nxt in remaining:
                remaining.remove(nxt)
                local.add(nxt)
                dq.append(nxt)

reachable_ratio = len(visited) / len(node_ids)

degrees = [len(adj[nid]) for nid in node_ids]
min_degree = min(degrees) if degrees else 0
max_degree = max(degrees) if degrees else 0
avg_degree = sum(degrees) / len(degrees) if degrees else 0.0

print("node_count =", len(node_ids))
print("edge_count =", len(edges))
print("visited_from_first =", len(visited))
print("reachable_ratio =", round(reachable_ratio, 4))
print("connected_components =", component_count)
print("min_degree =", min_degree)
print("max_degree =", max_degree)
print("avg_degree =", round(avg_degree, 3))

if len(visited) != len(node_ids):
    print("❌ Не все узлы достижимы из первой вершины")
    sys.exit(1)

if component_count != 1:
    print("❌ Граф распадается на несколько компонент")
    sys.exit(1)

if min_degree < 1:
    print("❌ Есть изолированные узлы")
    sys.exit(1)

print("✅ RegionClusterGraph связный и не содержит изолированных узлов")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.2 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"