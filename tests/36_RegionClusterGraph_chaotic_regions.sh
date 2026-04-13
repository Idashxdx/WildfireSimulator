#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

echo "============================================================"
echo " ТЕСТ: RegionClusterGraph должен иметь хаотичные области, а не квадраты"
echo "============================================================"

create_payload='{
  "name": "test-RegionClusterGraph-chaotic",
  "description": "chaotic region shape test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 2,
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
import json, sys
from collections import defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

nodes = data["graph"]["nodes"]
groups = defaultdict(list)

for n in nodes:
    groups[n["groupKey"]].append(n)

if len(groups) < 4:
    print("❌ Слишком мало областей")
    sys.exit(1)

sizes = [len(v) for v in groups.values()]
min_size = min(sizes)
max_size = max(sizes)

fill_ratios = []
for key, pts in groups.items():
    xs = [p["x"] for p in pts]
    ys = [p["y"] for p in pts]
    width = max(xs) - min(xs) + 1
    height = max(ys) - min(ys) + 1
    box_area = width * height
    fill = len(pts) / box_area
    fill_ratios.append(fill)

min_fill = min(fill_ratios)
max_fill = max(fill_ratios)

print("region_count =", len(groups))
print("min_region_size =", min_size)
print("max_region_size =", max_size)
print("min_fill_ratio =", round(min_fill, 3))
print("max_fill_ratio =", round(max_fill, 3))

if max_size - min_size < 4:
    print("❌ Размеры областей слишком одинаковые")
    sys.exit(1)

if min_fill > 0.92:
    print("❌ Все области слишком плотно и прямоугольно заполнены")
    sys.exit(1)

print("✅ Иерархия имеет неоднородные хаотичные области")
PY

echo "📁 Временные файлы: $TMP_DIR"
echo "============================================================"