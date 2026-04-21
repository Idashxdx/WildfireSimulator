#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_graph_scenario_distinction_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: сценарии графов должны реально различаться"
echo "============================================================"

run_case() {
    local label="$1"
    local scale="$2"
    local scenario="$3"
    local seed="$4"
    local width="$5"
    local height="$6"
    local moisture_min="$7"
    local moisture_max="$8"
    local elevation="$9"

    local create_json="$OUT_DIR/${label}_create.json"
    local graph_json="$OUT_DIR/${label}_graph.json"
    local metrics_txt="$OUT_DIR/${label}_metrics.txt"

    curl -s -X POST "$API_URL/api/simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"${label}\",
        \"description\": \"Scenario distinction test: ${label}\",
        \"graphType\": 1,
        \"graphScaleType\": ${scale},
        \"gridWidth\": ${width},
        \"gridHeight\": ${height},
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
from collections import Counter, defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

node_by_id = {n["id"]: n for n in nodes}
group_counter = Counter((n.get("groupKey") or "ungrouped") for n in nodes)

cross_edges = []
same_edges = []

for e in edges:
    a = node_by_id[e["fromCellId"]]
    b = node_by_id[e["toCellId"]]
    if (a.get("groupKey") or "ungrouped") != (b.get("groupKey") or "ungrouped"):
        cross_edges.append(e)
    else:
        same_edges.append(e)

degrees = defaultdict(int)
for e in edges:
    degrees[e["fromCellId"]] += 1
    degrees[e["toCellId"]] += 1

node_count = len(nodes)
edge_count = len(edges)
group_count = len([g for g in group_counter if g != "ungrouped"])
water_count = sum(1 for n in nodes if n.get("vegetation") == "Water")
bare_count = sum(1 for n in nodes if n.get("vegetation") == "Bare")

avg_degree = (2.0 * edge_count / node_count) if node_count else 0.0
avg_moisture = (sum(n.get("moisture", 0.0) for n in nodes) / node_count) if node_count else 0.0
avg_elevation = (sum(n.get("elevation", 0.0) for n in nodes) / node_count) if node_count else 0.0

avg_cross_modifier = (
    sum(e["fireSpreadModifier"] for e in cross_edges) / len(cross_edges)
    if cross_edges else 0.0
)
avg_same_modifier = (
    sum(e["fireSpreadModifier"] for e in same_edges) / len(same_edges)
    if same_edges else 0.0
)

avg_cross_distance = (
    sum(e["distance"] for e in cross_edges) / len(cross_edges)
    if cross_edges else 0.0
)
avg_same_distance = (
    sum(e["distance"] for e in same_edges) / len(same_edges)
    if same_edges else 0.0
)

xs = [n.get("renderX", 0.0) for n in nodes]
ys = [n.get("renderY", 0.0) for n in nodes]
bbox_area = (max(xs) - min(xs)) * (max(ys) - min(ys)) if xs and ys else 0.0

print(f"node_count={node_count}")
print(f"edge_count={edge_count}")
print(f"group_count={group_count}")
print(f"water_count={water_count}")
print(f"bare_count={bare_count}")
print(f"cross_edge_count={len(cross_edges)}")
print(f"same_edge_count={len(same_edges)}")
print(f"avg_degree={avg_degree:.6f}")
print(f"avg_moisture={avg_moisture:.6f}")
print(f"avg_elevation={avg_elevation:.6f}")
print(f"avg_cross_modifier={avg_cross_modifier:.6f}")
print(f"avg_same_modifier={avg_same_modifier:.6f}")
print(f"avg_cross_distance={avg_cross_distance:.6f}")
print(f"avg_same_distance={avg_same_distance:.6f}")
print(f"bbox_area={bbox_area:.6f}")
PY
}

extract_metric() {
    local file="$1"
    local key="$2"
    grep "^${key}=" "$file" | head -n1 | cut -d'=' -f2
}

compare_small() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "SMALL GRAPH: сравнение сценариев"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    run_case "small_dense" 0 0 500101 20 20 0.10 0.22 20.0
    run_case "small_water" 0 1 500102 20 20 0.10 0.22 20.0
    run_case "small_firebreak" 0 2 500103 20 20 0.10 0.22 20.0

    local dense_cross water_cross fire_cross
    local dense_water water_water fire_water
    local dense_bare water_bare fire_bare
    local dense_cross_mod water_cross_mod fire_cross_mod

    dense_cross=$(extract_metric "$OUT_DIR/small_dense_metrics.txt" "cross_edge_count")
    water_cross=$(extract_metric "$OUT_DIR/small_water_metrics.txt" "cross_edge_count")
    fire_cross=$(extract_metric "$OUT_DIR/small_firebreak_metrics.txt" "cross_edge_count")

    dense_water=$(extract_metric "$OUT_DIR/small_dense_metrics.txt" "water_count")
    water_water=$(extract_metric "$OUT_DIR/small_water_metrics.txt" "water_count")
    fire_water=$(extract_metric "$OUT_DIR/small_firebreak_metrics.txt" "water_count")

    dense_bare=$(extract_metric "$OUT_DIR/small_dense_metrics.txt" "bare_count")
    water_bare=$(extract_metric "$OUT_DIR/small_water_metrics.txt" "bare_count")
    fire_bare=$(extract_metric "$OUT_DIR/small_firebreak_metrics.txt" "bare_count")

    dense_cross_mod=$(extract_metric "$OUT_DIR/small_dense_metrics.txt" "avg_cross_modifier")
    water_cross_mod=$(extract_metric "$OUT_DIR/small_water_metrics.txt" "avg_cross_modifier")
    fire_cross_mod=$(extract_metric "$OUT_DIR/small_firebreak_metrics.txt" "avg_cross_modifier")

    echo "small_dense:     cross=$dense_cross water=$dense_water bare=$dense_bare crossMod=$dense_cross_mod"
    echo "small_water:     cross=$water_cross water=$water_water bare=$water_bare crossMod=$water_cross_mod"
    echo "small_firebreak: cross=$fire_cross water=$fire_water bare=$fire_bare crossMod=$fire_cross_mod"

    if [[ "$water_water" -le "$dense_water" ]]; then
        echo "❌ WaterBarrier для Small не увеличивает число water-узлов"
        exit 1
    fi

    if [[ "$fire_cross" -gt "$dense_cross" ]]; then
        echo "❌ FirebreakGap для Small не должен иметь больше межгрупповых связей, чем dense bridge-critical"
        exit 1
    fi

    python3 - <<PY
dense = float("$dense_cross_mod")
water = float("$water_cross_mod")
fire = float("$fire_cross_mod")

if not (water < dense):
    raise SystemExit("❌ Small WaterBarrier должен ослаблять межгрупповую связь относительно dense")

if not (fire < dense):
    raise SystemExit("❌ Small FirebreakGap должен ослаблять межгрупповую связь относительно dense")
PY

    echo "✅ SmallGraph сценарии различимы"
}

compare_medium() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "MEDIUM GRAPH: сравнение сценариев"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    run_case "medium_dense" 1 0 600101 24 24 0.18 0.42 35.0
    run_case "medium_water" 1 1 600102 24 24 0.18 0.42 35.0
    run_case "medium_wet"   1 4 600103 24 24 0.18 0.42 35.0

    local dense_moist wet_moist water_water dense_water
    local dense_cross_mod water_cross_mod wet_cross_mod

    dense_moist=$(extract_metric "$OUT_DIR/medium_dense_metrics.txt" "avg_moisture")
    wet_moist=$(extract_metric "$OUT_DIR/medium_wet_metrics.txt" "avg_moisture")

    dense_water=$(extract_metric "$OUT_DIR/medium_dense_metrics.txt" "water_count")
    water_water=$(extract_metric "$OUT_DIR/medium_water_metrics.txt" "water_count")

    dense_cross_mod=$(extract_metric "$OUT_DIR/medium_dense_metrics.txt" "avg_cross_modifier")
    water_cross_mod=$(extract_metric "$OUT_DIR/medium_water_metrics.txt" "avg_cross_modifier")
    wet_cross_mod=$(extract_metric "$OUT_DIR/medium_wet_metrics.txt" "avg_cross_modifier")

    echo "medium_dense: moisture=$dense_moist water=$dense_water crossMod=$dense_cross_mod"
    echo "medium_water: moisture=$(extract_metric "$OUT_DIR/medium_water_metrics.txt" "avg_moisture") water=$water_water crossMod=$water_cross_mod"
    echo "medium_wet:   moisture=$wet_moist water=$(extract_metric "$OUT_DIR/medium_wet_metrics.txt" "water_count") crossMod=$wet_cross_mod"

    if [[ "$water_water" -le "$dense_water" ]]; then
        echo "❌ Medium WaterBarrier должен содержать больше water-узлов"
        exit 1
    fi

    python3 - <<PY
dense_m = float("$dense_moist")
wet_m = float("$wet_moist")
dense_cross = float("$dense_cross_mod")
water_cross = float("$water_cross_mod")

if not (wet_m > dense_m):
    raise SystemExit("❌ Medium WetAfterRain должен давать большую среднюю влажность, чем dense")

if not (water_cross < dense_cross):
    raise SystemExit("❌ Medium WaterBarrier должен ослаблять межкластерные связи относительно dense")
PY

    echo "✅ MediumGraph сценарии различимы"
}

compare_large() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "LARGE GRAPH: сравнение сценариев"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    run_case "large_firebreak" 2 2 700101 34 34 0.10 0.32 55.0
    run_case "large_hilly"     2 3 700102 34 34 0.10 0.32 55.0
    run_case "large_wet"       2 4 700103 34 34 0.10 0.32 55.0

    local fire_cross wet_cross
    local fire_moist wet_moist
    local fire_bare wet_bare
    local hilly_elev fire_elev wet_elev
    local fire_cross_dist hilly_cross_dist wet_cross_dist

    fire_cross=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "cross_edge_count")
    wet_cross=$(extract_metric "$OUT_DIR/large_wet_metrics.txt" "cross_edge_count")

    fire_moist=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "avg_moisture")
    wet_moist=$(extract_metric "$OUT_DIR/large_wet_metrics.txt" "avg_moisture")

    fire_bare=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "bare_count")
    wet_bare=$(extract_metric "$OUT_DIR/large_wet_metrics.txt" "bare_count")

    fire_elev=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "avg_elevation")
    hilly_elev=$(extract_metric "$OUT_DIR/large_hilly_metrics.txt" "avg_elevation")
    wet_elev=$(extract_metric "$OUT_DIR/large_wet_metrics.txt" "avg_elevation")

    fire_cross_dist=$(extract_metric "$OUT_DIR/large_firebreak_metrics.txt" "avg_cross_distance")
    hilly_cross_dist=$(extract_metric "$OUT_DIR/large_hilly_metrics.txt" "avg_cross_distance")
    wet_cross_dist=$(extract_metric "$OUT_DIR/large_wet_metrics.txt" "avg_cross_distance")

    echo "large_firebreak: cross=$fire_cross moisture=$fire_moist bare=$fire_bare avgElevation=$fire_elev crossDist=$fire_cross_dist"
    echo "large_hilly:     cross=$(extract_metric "$OUT_DIR/large_hilly_metrics.txt" "cross_edge_count") moisture=$(extract_metric "$OUT_DIR/large_hilly_metrics.txt" "avg_moisture") bare=$(extract_metric "$OUT_DIR/large_hilly_metrics.txt" "bare_count") avgElevation=$hilly_elev crossDist=$hilly_cross_dist"
    echo "large_wet:       cross=$wet_cross moisture=$wet_moist bare=$wet_bare avgElevation=$wet_elev crossDist=$wet_cross_dist"

    if [[ "$fire_bare" -le 0 ]]; then
        echo "❌ Large FirebreakGap должен содержать bare-узлы"
        exit 1
    fi

    python3 - <<PY
fire_m = float("$fire_moist")
wet_m = float("$wet_moist")
fire_dist = float("$fire_cross_dist")
wet_dist = float("$wet_cross_dist")
hilly_e = float("$hilly_elev")
fire_e = float("$fire_elev")

if not (wet_m > fire_m):
    raise SystemExit("❌ Large WetAfterRain должен давать большую среднюю влажность, чем FirebreakGap")

if abs(hilly_e - fire_e) < 0.5:
    raise SystemExit("❌ Large HillyClusters должен отличаться по среднему рельефу от FirebreakGap")

if abs(fire_dist - wet_dist) < 0.15:
    raise SystemExit("❌ Large сценарии должны отличаться по средней длине межзонных связей")
PY

    echo "✅ LargeGraph сценарии различимы"
}

compare_small
compare_medium
compare_large

echo ""
echo "============================================================"
echo "✅ ВСЕ ПРОВЕРКИ СЦЕНАРНОЙ РАЗЛИЧИМОСТИ ПРОЙДЕНЫ"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"