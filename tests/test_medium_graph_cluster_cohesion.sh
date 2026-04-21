#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ: MediumGraph должен иметь кластерную локальную структуру"
echo "============================================================"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-medium-graph-cluster-cohesion",
    "description": "medium graph cluster cohesion",
    "gridWidth": 24,
    "gridHeight": 24,
    "graphType": 1,
    "graphScaleType": 1,
    "initialMoistureMin": 0.20,
    "initialMoistureMax": 0.70,
    "elevationVariation": 45,
    "initialFireCellsCount": 1,
    "simulationSteps": 5,
    "stepDurationSeconds": 900,
    "randomSeed": 424242,
    "temperature": 25,
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
import json, math, sys
from collections import defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

nodes = data["graph"]["nodes"]
edges = data["graph"]["edges"]

if not (20 <= len(nodes) <= 90):
    print(f"❌ Для MediumGraph число узлов вне ожидаемого диапазона: {len(nodes)}")
    sys.exit(1)

group_sizes = defaultdict(int)
for n in nodes:
    group_sizes[n.get("groupKey") or ""] += 1

if len(group_sizes) < 3:
    print("❌ Для MediumGraph слишком мало кластеров")
    sys.exit(1)

global_moisture = [n["moisture"] for n in nodes]
global_mean = sum(global_moisture) / len(global_moisture)
global_std = (sum((x - global_mean) ** 2 for x in global_moisture) / len(global_moisture)) ** 0.5

same_group_ratios = []
same_veg_ratios = []
local_stds = []

for node in nodes:
    dists = []
    for other in nodes:
        if other["id"] == node["id"]:
            continue
        dx = other["x"] - node["x"]
        dy = other["y"] - node["y"]
        d = math.sqrt(dx*dx + dy*dy)
        dists.append((d, other))

    dists.sort(key=lambda t: t[0])
    near = [o for _, o in dists[:6]]
    if not near:
        continue

    same_group = sum(1 for o in near if (o.get("groupKey") or "") == (node.get("groupKey") or ""))
    same_group_ratios.append(same_group / len(near))

    same_veg = sum(1 for o in near if o["vegetation"] == node["vegetation"])
    same_veg_ratios.append(same_veg / len(near))

    vals = [node["moisture"]] + [o["moisture"] for o in near]
    mean = sum(vals) / len(vals)
    std = (sum((x - mean) ** 2 for x in vals) / len(vals)) ** 0.5
    local_stds.append(std)

avg_same_group = sum(same_group_ratios) / len(same_group_ratios)
avg_same_veg = sum(same_veg_ratios) / len(same_veg_ratios)
avg_local_std = sum(local_stds) / len(local_stds)

cross_edges = 0
id_to_node = {n["id"]: n for n in nodes}
for e in edges:
    a = id_to_node[e["fromCellId"]]
    b = id_to_node[e["toCellId"]]
    if (a.get("groupKey") or "") != (b.get("groupKey") or ""):
        cross_edges += 1

print("node_count =", len(nodes))
print("group_count =", len(group_sizes))
print("avg_same_group_ratio =", round(avg_same_group, 3))
print("avg_same_vegetation_ratio =", round(avg_same_veg, 3))
print("global_moisture_std =", round(global_std, 3))
print("avg_local_moisture_std =", round(avg_local_std, 3))
print("cross_edges =", cross_edges)

if avg_same_group < 0.52:
    print("❌ Локальные соседи слишком слабо принадлежат одним кластерам")
    sys.exit(1)

if avg_same_veg < 0.48:
    print("❌ Локальная растительность слишком случайна для MediumGraph")
    sys.exit(1)

if avg_local_std >= global_std:
    print("❌ Локальная влажность не более однородна, чем глобальная")
    sys.exit(1)

if cross_edges < 2:
    print("❌ Для MediumGraph слишком мало межкластерных мостов")
    sys.exit(1)

print("✅ MediumGraph показывает выраженную кластерную структуру")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"