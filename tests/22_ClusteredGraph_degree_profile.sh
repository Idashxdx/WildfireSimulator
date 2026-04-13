#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/ClusteredGraph_degree_profile_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: профиль степеней ClusteredGraph"
echo "============================================================"
echo ""

SIZES=(5 8 12 20 40)

run_case () {
    local SIDE="$1"
    local LABEL="$2"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " CASE: $LABEL (grid=${SIDE}x${SIDE})"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    local CREATE_JSON="$OUT_DIR/create_${LABEL}.json"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"ClusteredGraph Degree ${LABEL}\",
        \"description\": \"Degree profile test\",
        \"gridWidth\": $SIDE,
        \"gridHeight\": $SIDE,
        \"graphType\": 1,
        \"initialMoistureMin\": 0.20,
        \"initialMoistureMax\": 0.20,
        \"elevationVariation\": 10.0,
        \"initialFireCellsCount\": 1,
        \"simulationSteps\": 5,
        \"stepDurationSeconds\": 1800,
        \"randomSeed\": 424242,
        \"temperature\": 30,
        \"humidity\": 30,
        \"windSpeed\": 5,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$CREATE_JSON"

    local SIM_ID
    SIM_ID=$(jq -r '.id' "$CREATE_JSON")

    if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
        echo "❌ Ошибка создания"
        cat "$CREATE_JSON"
        exit 1
    fi

    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$OUT_DIR/start_${LABEL}.json"

    local GRAPH_JSON="$OUT_DIR/graph_${LABEL}.json"
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

    local DEGREE_CSV="$OUT_DIR/degrees_${LABEL}.csv"
    jq -r '
      .graph as $g
      | $g.nodes[]
      | .id as $id
      | [
          $id,
          (
            [$g.edges[] | select(.fromCellId == $id or .toCellId == $id)] | length
          )
        ]
      | @csv
    ' "$GRAPH_JSON" > "$DEGREE_CSV"

    local MIN_DEGREE
    local MAX_DEGREE
    local AVG_DEGREE
    local ZERO_COUNT
    local ONE_COUNT
    local NODE_COUNT

    NODE_COUNT=$(wc -l < "$DEGREE_CSV" | tr -d ' ')
    MIN_DEGREE=$(cut -d',' -f2 "$DEGREE_CSV" | tr -d '"' | sort -n | head -n1)
    MAX_DEGREE=$(cut -d',' -f2 "$DEGREE_CSV" | tr -d '"' | sort -n | tail -n1)
    AVG_DEGREE=$(cut -d',' -f2 "$DEGREE_CSV" | tr -d '"' | awk '{s+=$1} END { if (NR>0) printf "%.2f", s/NR; else print "0" }')
    ZERO_COUNT=$(cut -d',' -f2 "$DEGREE_CSV" | tr -d '"' | awk '$1==0 {c++} END {print c+0}')
    ONE_COUNT=$(cut -d',' -f2 "$DEGREE_CSV" | tr -d '"' | awk '$1==1 {c++} END {print c+0}')

    echo "   nodes        = $NODE_COUNT"
    echo "   min degree   = $MIN_DEGREE"
    echo "   avg degree   = $AVG_DEGREE"
    echo "   max degree   = $MAX_DEGREE"
    echo "   degree==0    = $ZERO_COUNT"
    echo "   degree==1    = $ONE_COUNT"
    echo ""

    if [[ "$ZERO_COUNT" -gt 0 ]]; then
        echo "❌ Есть изолированные вершины"
        exit 1
    fi

    if [[ "$SIDE" -ge 8 && "$MIN_DEGREE" -lt 2 ]]; then
        echo "❌ Для графа $LABEL минимальная степень слишком мала"
        exit 1
    fi

    if [[ "$SIDE" -le 12 && "$MAX_DEGREE" -gt 6 ]]; then
        echo "❌ Для маленького графа $LABEL максимальная степень слишком велика"
        exit 1
    fi

    echo "✅ Профиль степеней выглядит разумно"
    echo ""
}

for SIDE in "${SIZES[@]}"; do
    run_case "$SIDE" "${SIDE}x${SIDE}"
done

echo "============================================================"
echo "✅ Тест профиля степеней завершён"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"