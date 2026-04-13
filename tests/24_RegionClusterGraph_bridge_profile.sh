#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/RegionClusterGraph_bridge_profile_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: профиль связей RegionClusterGraph"
echo "============================================================"
echo ""

run_case() {
    local LABEL="$1"
    local W="$2"
    local H="$3"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " CASE: $LABEL"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    local CREATE_JSON="$OUT_DIR/create_${LABEL}.json"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"RegionClusterGraph Bridge Profile ${LABEL}\",
        \"description\": \"Bridge profile test\",
        \"gridWidth\": $W,
        \"gridHeight\": $H,
        \"graphType\": 2,
        \"initialMoistureMin\": 0.20,
        \"initialMoistureMax\": 0.20,
        \"elevationVariation\": 10.0,
        \"initialFireCellsCount\": 1,
        \"simulationSteps\": 10,
        \"stepDurationSeconds\": 1800,
        \"randomSeed\": 424242,
        \"temperature\": 30,
        \"humidity\": 40,
        \"windSpeed\": 5,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$CREATE_JSON"

    local SIM_ID
    SIM_ID=$(jq -r '.id' "$CREATE_JSON")

    if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
        echo "❌ Не удалось создать симуляцию"
        cat "$CREATE_JSON"
        exit 1
    fi

    local GRAPH_JSON="$OUT_DIR/graph_${LABEL}.json"
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

    local SUCCESS
    SUCCESS=$(jq -r '.success // false' "$GRAPH_JSON")
    if [[ "$SUCCESS" != "true" ]]; then
        echo "❌ Не удалось получить граф"
        cat "$GRAPH_JSON"
        exit 1
    fi

    python3 - "$GRAPH_JSON" <<'PY'
import json
import sys

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = {n["id"]: n for n in graph["nodes"]}
edges = graph["edges"]

intra = 0
inter = 0
by_pair = {}

for e in edges:
    a = nodes[e["fromCellId"]]["groupKey"]
    b = nodes[e["toCellId"]]["groupKey"]

    if a == b:
        intra += 1
    else:
        inter += 1
        pair = tuple(sorted((a, b)))
        by_pair[pair] = by_pair.get(pair, 0) + 1

print(f"   nodes              = {len(nodes)}")
print(f"   totalEdges         = {len(edges)}")
print(f"   intraClusterEdges  = {intra}")
print(f"   interClusterEdges  = {inter}")

if by_pair:
    print("   bridgePairs:")
    for pair, count in sorted(by_pair.items()):
        print(f"      {pair[0]} <-> {pair[1]} : {count}")

if inter <= 0:
    print("❌ Межкластерных связей нет")
    sys.exit(1)

if intra <= inter:
    print("❌ Внутрикластерных связей должно быть больше, чем межкластерных")
    sys.exit(1)

print("✅ Профиль связей выглядит разумно")
PY

    echo ""
}

run_case "12x12" 12 12
run_case "20x20" 20 20

echo "============================================================"
echo "✅ Тест профиля связей завершён"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"