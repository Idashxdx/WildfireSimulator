#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_large_graph_scenario_distinction_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: сценарии должны различаться только для LargeGraph"
echo "============================================================"

run_case() {
    local label="$1"
    local scenario="$2"
    local seed="$3"
    local moisture_min="$4"
    local moisture_max="$5"
    local elevation="$6"

    local create_json="$OUT_DIR/${label}_create.json"
    local graph_json="$OUT_DIR/${label}_graph.json"
    local metrics_txt="$OUT_DIR/${label}_metrics.txt"

    curl -s -X POST "$API_URL/api/simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"${label}\",
        \"description\": \"Large scenario distinction test: ${label}\",
        \"graphType\": 1,
        \"graphScaleType\": 2,
        \"gridWidth\": 34,
        \"gridHeight\": 34,
        \"initialMoistureMin\": ${moisture_min},
        \"initialMoistureMax\": ${moisture_max},
        \"elevationVariation\": ${elevation},
        \"initialFireCellsCount\": 1,
        \"simulationSteps\": 50,
        \"stepDurationSeconds\": 900,
        \"randomSeed\": ${seed},
        \"mapCreationMode\": 1,
        \"clusteredScenarioType\": ${scenario},
        \"temperature\": 28.0,
        \"humidity\": 35.0,
        \"windSpeed\": 6.0,
        \"windDirection\": 45.0,
        \"precipitation\": 0.0
      }" > "$create_json"

    local sim_id
    sim_id=$(jq -r '.id // empty' "$create_json")

    if [[ -z "$sim_id" || "$sim_id" == "null" ]]; then
        echo "❌ Не удалось создать симуляцию для ${label}"
        cat "$create_json"
        exit 1
    fi

    echo "case=${label} sim_id=${sim_id}"

    curl -s "$API_URL/api/SimulationManager/$sim_id/graph" > "$graph_json"

    if [[ ! -s "$graph_json" ]]; then
        echo "❌ Graph endpoint вернул пустой ответ для ${label}"
        exit 1
    fi

    local graph_success
    graph_success=$(jq -r '.success // false' "$graph_json" 2>/dev/null || echo "false")

    if [[ "$graph_success" != "true" ]]; then
        echo "❌ Не удалось получить graph JSON для ${label}"
        cat "$graph_json"
        exit 1
    fi

    python3 - "$graph_json" <<'PY' > "$metrics_txt"
import json
import sys
from collections import Counter

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

node_by_id = {n["id"]: n for n in nodes}
groups = Counter((n.get("groupKey") or "ungrouped") for n in nodes)

cross_edges = []
same_edges = []

for e in edges:
    a = node_by_id[e["fromCellId"]]
    b = node_by_id[e["toCellId"]]

    if (a.get("groupKey") or "") != (b.get("groupKey") or ""):
        cross_edges.append(e)
    else:
        same_edges.append(e)

node_count = len(nodes)
water_count = sum(1 for n in nodes if n.get("vegetation") == "Water")
bare_count = sum(1 for n in nodes if n.get("vegetation") == "Bare")

avg_moisture = sum(n.get("moisture", 0.0) for n in nodes) / node_count if node_count else 0.0
avg_elevation = sum(n.get("elevation", 0.0) for n in nodes) / node_count if node_count else 0.0

avg_cross_modifier = sum(e["fireSpreadModifier"] for e in cross_edges) / len(cross_edges) if cross_edges else 0.0
avg_same_modifier = sum(e["fireSpreadModifier"] for e in same_edges) / len(same_edges) if same_edges else 0.0

print(f"node_count={node_count}")
print(f"edge_count={len(edges)}")
print(f"group_count={len(groups)}")
print(f"water_count={water_count}")
print(f"bare_count={bare_count}")
print(f"cross_edge_count={len(cross_edges)}")
print(f"same_edge_count={len(same_edges)}")
print(f"avg_moisture={avg_moisture:.6f}")
print(f"avg_elevation={avg_elevation:.6f}")
print(f"avg_cross_modifier={avg_cross_modifier:.6f}")
print(f"avg_same_modifier={avg_same_modifier:.6f}")
PY
}

extract_metric() {
    local file="$1"
    local key="$2"
    grep "^${key}=" "$file" | head -n1 | cut -d'=' -f2
}

run_case "large_dense" 0 700100 0.16 0.36 45.0
run_case "large_water" 2 700101 0.16 0.36 45.0
run_case "large_firebreak" 4 700102 0.12 0.34 55.0
run_case "large_hilly" 5 700103 0.18 0.42 75.0
run_case "large_wet" 6 700104 0.38 0.72 35.0

dense_water=$(extract_metric "$OUT_DIR/large_dense_metrics.txt" "water_count")
water_water=$(extract_metric "$OUT_DIR/large_water_metrics.txt" "water_count")

dense_bare=$(extract_metric "$OUT_DIR/large_dense_metrics.txt" "bare_count")
fire_bare=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "bare_count")

dense_moist=$(extract_metric "$OUT_DIR/large_dense_metrics.txt" "avg_moisture")
wet_moist=$(extract_metric "$OUT_DIR/large_wet_metrics.txt" "avg_moisture")

dense_cross_mod=$(extract_metric "$OUT_DIR/large_dense_metrics.txt" "avg_cross_modifier")
fire_cross_mod=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "avg_cross_modifier")

dense_elev=$(extract_metric "$OUT_DIR/large_dense_metrics.txt" "avg_elevation")
hilly_elev=$(extract_metric "$OUT_DIR/large_hilly_metrics.txt" "avg_elevation")

echo "large_dense:     water=$dense_water bare=$dense_bare moisture=$dense_moist crossMod=$dense_cross_mod elevation=$dense_elev"
echo "large_water:     water=$water_water moisture=$(extract_metric "$OUT_DIR/large_water_metrics.txt" "avg_moisture")"
echo "large_firebreak: bare=$fire_bare crossMod=$fire_cross_mod"
echo "large_hilly:     elevation=$hilly_elev"
echo "large_wet:       moisture=$wet_moist"

if [[ "$water_water" -le "$dense_water" ]]; then
    echo "❌ ForestWithRiver для Large должен увеличивать число water-узлов"
    exit 1
fi

if [[ "$fire_bare" -le "$dense_bare" ]]; then
    echo "❌ ForestWithFirebreak для Large должен увеличивать число bare-узлов"
    exit 1
fi

python3 - <<PY
dense_m = float("$dense_moist")
wet_m = float("$wet_moist")
dense_cross = float("$dense_cross_mod")
fire_cross = float("$fire_cross_mod")
dense_elev = float("$dense_elev")
hilly_elev = float("$hilly_elev")

if not (wet_m > dense_m):
    raise SystemExit("❌ WetForestAfterRain должен давать большую среднюю влажность")

if not (fire_cross < dense_cross):
    raise SystemExit("❌ ForestWithFirebreak должен ослаблять межобластные связи")

if abs(hilly_elev - dense_elev) < 0.5:
    raise SystemExit("❌ HillyTerrain должен отличаться по рельефу")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $OUT_DIR"
echo "============================================================"