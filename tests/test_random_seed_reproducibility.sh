#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM1_JSON="$TMP_DIR/sim1.json"
SIM2_JSON="$TMP_DIR/sim2.json"
GRAPH1_JSON="$TMP_DIR/graph1.json"
GRAPH2_JSON="$TMP_DIR/graph2.json"

echo "============================================================"
echo " ТЕСТ 11.1: одинаковый RandomSeed должен давать одинаковый граф"
echo "============================================================"

create_payload() {
cat <<'JSON'
{
  "name": "seed-repro-test",
  "description": "seed reproducibility test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 2,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 50,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 900,
  "randomSeed": 777777,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 5,
  "windDirection": 45,
  "precipitation": 0
}
JSON
}

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$(create_payload)" > "$SIM1_JSON"

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$(create_payload)" > "$SIM2_JSON"

SIM1_ID="$(jq -r '.id' "$SIM1_JSON")"
SIM2_ID="$(jq -r '.id' "$SIM2_JSON")"

if [[ -z "$SIM1_ID" || "$SIM1_ID" == "null" ]]; then
  echo "❌ Не удалось создать первую симуляцию"
  cat "$SIM1_JSON"
  exit 1
fi

if [[ -z "$SIM2_ID" || "$SIM2_ID" == "null" ]]; then
  echo "❌ Не удалось создать вторую симуляцию"
  cat "$SIM2_JSON"
  exit 1
fi

echo "sim1_id = $SIM1_ID"
echo "sim2_id = $SIM2_ID"

curl -sS "$BASE_URL/api/SimulationManager/$SIM1_ID/graph" > "$GRAPH1_JSON"
curl -sS "$BASE_URL/api/SimulationManager/$SIM2_ID/graph" > "$GRAPH2_JSON"

python3 - "$GRAPH1_JSON" "$GRAPH2_JSON" << 'PY'
import json
import sys

g1_path, g2_path = sys.argv[1], sys.argv[2]

with open(g1_path, "r", encoding="utf-8") as f:
    g1 = json.load(f)

with open(g2_path, "r", encoding="utf-8") as f:
    g2 = json.load(f)

graph1 = g1["graph"]
graph2 = g2["graph"]

nodes1 = graph1["nodes"]
nodes2 = graph2["nodes"]
edges1 = graph1["edges"]
edges2 = graph2["edges"]

if len(nodes1) != len(nodes2):
    print("❌ Разное число узлов")
    sys.exit(1)

if len(edges1) != len(edges2):
    print("❌ Разное число рёбер")
    sys.exit(1)

sig_nodes1 = sorted(
    (n["x"], n["y"], round(n["renderX"], 6), round(n["renderY"], 6), n["groupKey"], n["vegetation"], round(n["moisture"], 6), round(n["elevation"], 6))
    for n in nodes1
)
sig_nodes2 = sorted(
    (n["x"], n["y"], round(n["renderX"], 6), round(n["renderY"], 6), n["groupKey"], n["vegetation"], round(n["moisture"], 6), round(n["elevation"], 6))
    for n in nodes2
)

if sig_nodes1 != sig_nodes2:
    print("❌ Узлы отличаются при одинаковом seed")
    sys.exit(1)

id_to_xy_1 = {n["id"]: (n["x"], n["y"]) for n in nodes1}
id_to_xy_2 = {n["id"]: (n["x"], n["y"]) for n in nodes2}

sig_edges1 = sorted(
    tuple(sorted([id_to_xy_1[e["fromCellId"]], id_to_xy_1[e["toCellId"]]])) + (round(e["distance"], 6), round(e["slope"], 6), round(e["fireSpreadModifier"], 6))
    for e in edges1
)
sig_edges2 = sorted(
    tuple(sorted([id_to_xy_2[e["fromCellId"]], id_to_xy_2[e["toCellId"]]])) + (round(e["distance"], 6), round(e["slope"], 6), round(e["fireSpreadModifier"], 6))
    for e in edges2
)

if sig_edges1 != sig_edges2:
    print("❌ Рёбра отличаются при одинаковом seed")
    sys.exit(1)

print(f"node_count = {len(nodes1)}")
print(f"edge_count = {len(edges1)}")
print("✅ Граф воспроизводим при одинаковом RandomSeed")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.1 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"