#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ: ClusteredGraph должен иметь локальную региональную схожесть"
echo "============================================================"

create_payload='{
  "name": "test-ClusteredGraph-regional-cohesion",
  "description": "regional cohesion test",
  "gridWidth": 18,
  "gridHeight": 18,
  "graphType": 1,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 20,
  "stepDurationSeconds": 900,
  "randomSeed": 424242,
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

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

python3 - "$GRAPH_JSON" << 'PY'
import json, math, sys
from collections import Counter, defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

nodes = data["graph"]["nodes"]
if len(nodes) < 10:
    print("❌ Слишком мало узлов")
    sys.exit(1)

global_moisture = [n["moisture"] for n in nodes]
global_mean = sum(global_moisture) / len(global_moisture)
global_std = (sum((x - global_mean) ** 2 for x in global_moisture) / len(global_moisture)) ** 0.5

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

    same_veg = sum(1 for o in near if o["vegetation"] == node["vegetation"])
    same_veg_ratios.append(same_veg / len(near))

    vals = [node["moisture"]] + [o["moisture"] for o in near]
    mean = sum(vals) / len(vals)
    std = (sum((x - mean) ** 2 for x in vals) / len(vals)) ** 0.5
    local_stds.append(std)

avg_same_veg = sum(same_veg_ratios) / len(same_veg_ratios)
avg_local_std = sum(local_stds) / len(local_stds)

print("avg_same_vegetation_ratio =", round(avg_same_veg, 3))
print("global_moisture_std       =", round(global_std, 3))
print("avg_local_moisture_std    =", round(avg_local_std, 3))

if avg_same_veg < 0.58:
    print("❌ Локальная растительность слишком случайна")
    sys.exit(1)

if avg_local_std >= global_std:
    print("❌ Локальная влажность не более однородна, чем глобальная")
    sys.exit(1)

print("✅ ClusteredGraph имеет локальные региональные пятна")
PY

echo "📁 Временные файлы: $TMP_DIR"
echo "============================================================"