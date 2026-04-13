#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

echo "============================================================"
echo " ТЕСТ 8.3: Bundle-связи агрегируются по паре регионов"
echo "============================================================"

create_payload='{
  "name": "test-RegionClusterGraph-aggregation-basis",
  "description": "aggregation basis test",
  "gridWidth": 24,
  "gridHeight": 24,
  "graphType": 2,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 20,
  "stepDurationSeconds": 900,
  "randomSeed": 777002,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45,
  "precipitation": 0
}'

curl -fsS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$create_payload" > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

curl -fsS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json
import sys
from collections import defaultdict

path = sys.argv[1]

with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

graph = data.get("graph")
if not graph:
    print("❌ В ответе нет graph")
    sys.exit(1)

nodes = graph.get("nodes", [])
edges = graph.get("edges", [])

node_to_region = {n["id"]: (n.get("groupKey") or "region-unknown") for n in nodes}

inter_region_edges = []
for e in edges:
    ra = node_to_region.get(e["fromCellId"])
    rb = node_to_region.get(e["toCellId"])
    if not ra or not rb or ra == rb:
        continue
    inter_region_edges.append((tuple(sorted((ra, rb))), e))

if not inter_region_edges:
    print("❌ Нет межрегиональных рёбер")
    sys.exit(1)

grouped = defaultdict(list)
for pair, edge in inter_region_edges:
    grouped[pair].append(edge)

bundle_pairs = {pair: lst for pair, lst in grouped.items() if len(lst) >= 2}

print("total_inter_region_edges =", len(inter_region_edges))
print("distinct_region_pairs =", len(grouped))
print("bundle_pairs =", len(bundle_pairs))

if len(bundle_pairs) == 0:
    print("❌ Нет пар регионов с bundle-мостами")
    sys.exit(1)

for pair, lst in sorted(bundle_pairs.items(), key=lambda x: (-len(x[1]), x[0])):
    print(f"{pair[0]} <-> {pair[1]} : bundle_size={len(lst)}")

print("✅ Структура данных подходит для одной агрегированной линии в UI")
PY

echo "============================================================"
echo "✅ ТЕСТ 8.3 ПРОЙДЕН"
echo "============================================================"