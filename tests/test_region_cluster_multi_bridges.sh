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
echo " ТЕСТ 8.2: Между регионами должны быть bundle-связи, а не одиночные мосты"
echo "============================================================"

create_payload='{
  "name": "test-RegionClusterGraph-multi-bridges",
  "description": "multi bridge test",
  "gridWidth": 24,
  "gridHeight": 24,
  "graphType": 2,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 20,
  "stepDurationSeconds": 900,
  "randomSeed": 777001,
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

if not nodes or not edges:
    print("❌ В графе нет узлов или рёбер")
    sys.exit(1)

node_to_region = {}
for n in nodes:
    node_to_region[n["id"]] = n.get("groupKey") or "region-unknown"

inter_region_edges = defaultdict(list)

for e in edges:
    a = node_to_region.get(e["fromCellId"])
    b = node_to_region.get(e["toCellId"])
    if not a or not b or a == b:
        continue

    pair = tuple(sorted((a, b)))
    inter_region_edges[pair].append(e)

if not inter_region_edges:
    print("❌ Вообще нет межрегиональных связей")
    sys.exit(1)

pair_counts = {pair: len(lst) for pair, lst in inter_region_edges.items()}
max_count = max(pair_counts.values())
multi_pairs = {pair: cnt for pair, cnt in pair_counts.items() if cnt >= 2}
strong_multi_pairs = {pair: cnt for pair, cnt in pair_counts.items() if cnt >= 3}

print("inter_region_pair_count =", len(pair_counts))
print("max_edges_between_two_regions =", max_count)
print("pairs_with_2plus_edges =", len(multi_pairs))
print("pairs_with_3plus_edges =", len(strong_multi_pairs))

for pair, cnt in sorted(pair_counts.items(), key=lambda x: (-x[1], x[0])):
    print(f"{pair[0]} <-> {pair[1]} : {cnt}")

if len(multi_pairs) == 0:
    print("❌ Нет ни одной пары регионов с множественными межрегиональными связями")
    sys.exit(1)

print("✅ В данных есть реальные bundle-связи между регионами")
PY

echo "============================================================"
echo "✅ ТЕСТ 8.2 ПРОЙДЕН"
echo "============================================================"