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
echo " ТЕСТ 8.1: RegionClusterGraph должен иметь неоднородные области"
echo "============================================================"

create_payload='{
  "name": "test-RegionClusterGraph-shapes",
  "description": "region shape test",
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
if not nodes:
    print("❌ Нет узлов в графе")
    sys.exit(1)

groups = defaultdict(list)
for n in nodes:
    key = n.get("groupKey") or "region-unknown"
    groups[key].append(n)

if len(groups) < 4:
    print(f"❌ Слишком мало областей: {len(groups)}")
    sys.exit(1)

sizes = [len(v) for v in groups.values()]
fill_ratios = []
aspect_ratios = []

for key, pts in groups.items():
    xs = [p["x"] for p in pts]
    ys = [p["y"] for p in pts]

    width = max(xs) - min(xs) + 1
    height = max(ys) - min(ys) + 1
    box_area = width * height
    fill = len(pts) / box_area
    aspect = max(width, height) / max(1, min(width, height))

    fill_ratios.append(fill)
    aspect_ratios.append(aspect)

min_size = min(sizes)
max_size = max(sizes)
min_fill = min(fill_ratios)
max_fill = max(fill_ratios)
max_aspect = max(aspect_ratios)

print("region_count =", len(groups))
print("min_region_size =", min_size)
print("max_region_size =", max_size)
print("min_fill_ratio =", round(min_fill, 3))
print("max_fill_ratio =", round(max_fill, 3))
print("max_aspect_ratio =", round(max_aspect, 3))

if max_size - min_size < 4:
    print("❌ Размеры областей слишком одинаковые")
    sys.exit(1)

if min_fill > 0.92:
    print("❌ Все области слишком плотно заполнены")
    sys.exit(1)

if max_aspect < 1.15:
    print("❌ Все области слишком одинаково-компактные")
    sys.exit(1)

print("✅ Региональная структура неоднородна и не выглядит как набор квадратов")
PY

echo "============================================================"
echo "✅ ТЕСТ 8.1 ПРОЙДЕН"
echo "============================================================"