#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: RandomSeed должен воспроизводить Small / Medium / Large"
echo "============================================================"

create_sim() {
  local name="$1"
  local scale="$2"
  local out_file="$3"

  curl -sS -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"graph scale reproducibility test\",
      \"gridWidth\": 24,
      \"gridHeight\": 24,
      \"graphType\": 1,
      \"graphScaleType\": $scale,
      \"initialMoistureMin\": 0.30,
      \"initialMoistureMax\": 0.70,
      \"elevationVariation\": 40,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 6,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 777777,
      \"temperature\": 25,
      \"humidity\": 40,
      \"windSpeed\": 5,
      \"windDirection\": 45,
      \"precipitation\": 0
    }" > "$out_file"
}

check_pair() {
  local label="$1"
  local scale="$2"

  local SIM1_JSON="$TMP_DIR/${label}_sim1.json"
  local SIM2_JSON="$TMP_DIR/${label}_sim2.json"
  local GRAPH1_JSON="$TMP_DIR/${label}_graph1.json"
  local GRAPH2_JSON="$TMP_DIR/${label}_graph2.json"

  create_sim "${label}-1" "$scale" "$SIM1_JSON"
  create_sim "${label}-2" "$scale" "$SIM2_JSON"

  local SIM1_ID
  local SIM2_ID
  SIM1_ID="$(jq -r '.id' "$SIM1_JSON")"
  SIM2_ID="$(jq -r '.id' "$SIM2_JSON")"

  if [[ -z "$SIM1_ID" || "$SIM1_ID" == "null" ]]; then
    echo "❌ Не удалось создать первую симуляцию для $label"
    cat "$SIM1_JSON"
    exit 1
  fi

  if [[ -z "$SIM2_ID" || "$SIM2_ID" == "null" ]]; then
    echo "❌ Не удалось создать вторую симуляцию для $label"
    cat "$SIM2_JSON"
    exit 1
  fi

  curl -sS "$BASE_URL/api/SimulationManager/$SIM1_ID/graph" > "$GRAPH1_JSON"
  curl -sS "$BASE_URL/api/SimulationManager/$SIM2_ID/graph" > "$GRAPH2_JSON"

  python3 - "$GRAPH1_JSON" "$GRAPH2_JSON" "$label" << 'PY'
import json
import sys

g1_path, g2_path, label = sys.argv[1], sys.argv[2], sys.argv[3]

with open(g1_path, "r", encoding="utf-8") as f:
    g1 = json.load(f)["graph"]

with open(g2_path, "r", encoding="utf-8") as f:
    g2 = json.load(f)["graph"]

nodes1 = g1["nodes"]
nodes2 = g2["nodes"]
edges1 = g1["edges"]
edges2 = g2["edges"]

if len(nodes1) != len(nodes2):
    print(f"❌ {label}: разное число узлов")
    sys.exit(1)

if len(edges1) != len(edges2):
    print(f"❌ {label}: разное число рёбер")
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
    print(f"❌ {label}: узлы отличаются при одинаковом seed")
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
    print(f"❌ {label}: рёбра отличаются при одинаковом seed")
    sys.exit(1)

print(f"✅ {label}: граф воспроизводим")
PY
}

check_pair "small" 0
check_pair "medium" 1
check_pair "large" 2

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"