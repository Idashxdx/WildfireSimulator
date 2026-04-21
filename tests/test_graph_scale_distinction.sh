#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: Small / Medium / Large graph должны реально различаться"
echo "============================================================"

create_sim() {
  local name="$1"
  local scale="$2"
  local out_file="$3"

  curl -sS -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"graph scale distinction test\",
      \"gridWidth\": 24,
      \"gridHeight\": 24,
      \"graphType\": 1,
      \"graphScaleType\": $scale,
      \"initialMoistureMin\": 0.25,
      \"initialMoistureMax\": 0.55,
      \"elevationVariation\": 40,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 8,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 424242,
      \"temperature\": 26,
      \"humidity\": 40,
      \"windSpeed\": 5,
      \"windDirection\": 45,
      \"precipitation\": 0
    }" > "$out_file"
}

fetch_graph() {
  local sim_id="$1"
  local out_file="$2"
  curl -sS "$BASE_URL/api/SimulationManager/$sim_id/graph" > "$out_file"
}

SMALL_JSON="$TMP_DIR/small_sim.json"
MEDIUM_JSON="$TMP_DIR/medium_sim.json"
LARGE_JSON="$TMP_DIR/large_sim.json"

SMALL_GRAPH="$TMP_DIR/small_graph.json"
MEDIUM_GRAPH="$TMP_DIR/medium_graph.json"
LARGE_GRAPH="$TMP_DIR/large_graph.json"

create_sim "scale-small" 0 "$SMALL_JSON"
create_sim "scale-medium" 1 "$MEDIUM_JSON"
create_sim "scale-large" 2 "$LARGE_JSON"

SMALL_ID="$(jq -r '.id' "$SMALL_JSON")"
MEDIUM_ID="$(jq -r '.id' "$MEDIUM_JSON")"
LARGE_ID="$(jq -r '.id' "$LARGE_JSON")"

if [[ -z "$SMALL_ID" || "$SMALL_ID" == "null" ]]; then
  echo "❌ Не удалось создать Small simulation"
  cat "$SMALL_JSON"
  exit 1
fi

if [[ -z "$MEDIUM_ID" || "$MEDIUM_ID" == "null" ]]; then
  echo "❌ Не удалось создать Medium simulation"
  cat "$MEDIUM_JSON"
  exit 1
fi

if [[ -z "$LARGE_ID" || "$LARGE_ID" == "null" ]]; then
  echo "❌ Не удалось создать Large simulation"
  cat "$LARGE_JSON"
  exit 1
fi

fetch_graph "$SMALL_ID" "$SMALL_GRAPH"
fetch_graph "$MEDIUM_ID" "$MEDIUM_GRAPH"
fetch_graph "$LARGE_ID" "$LARGE_GRAPH"

python3 - "$SMALL_GRAPH" "$MEDIUM_GRAPH" "$LARGE_GRAPH" << 'PY'
import json
import sys
from collections import defaultdict

small_path, medium_path, large_path = sys.argv[1], sys.argv[2], sys.argv[3]

def load_graph(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    graph = data["graph"]
    nodes = graph["nodes"]
    edges = graph["edges"]

    id_to_node = {n["id"]: n for n in nodes}
    group_sizes = defaultdict(int)
    for n in nodes:
        group_sizes[n.get("groupKey") or ""] += 1

    cross_edges = 0
    total_edge_distance = 0.0
    total_cross_distance = 0.0
    total_same_distance = 0.0
    same_edges = 0
    degrees = defaultdict(int)

    for e in edges:
        a = id_to_node[e["fromCellId"]]
        b = id_to_node[e["toCellId"]]
        degrees[a["id"]] += 1
        degrees[b["id"]] += 1

        distance = float(e["distance"])
        total_edge_distance += distance

        if (a.get("groupKey") or "") != (b.get("groupKey") or ""):
            cross_edges += 1
            total_cross_distance += distance
        else:
            same_edges += 1
            total_same_distance += distance

    avg_degree = (sum(degrees.values()) / len(nodes)) if nodes else 0.0
    avg_edge_distance = (total_edge_distance / len(edges)) if edges else 0.0
    avg_cross_distance = (total_cross_distance / cross_edges) if cross_edges else 0.0
    avg_same_distance = (total_same_distance / same_edges) if same_edges else 0.0
    avg_group_size = (sum(group_sizes.values()) / len(group_sizes)) if group_sizes else 0.0

    xs = [n["x"] for n in nodes]
    ys = [n["y"] for n in nodes]
    bbox_area = (max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1) if nodes else 0

    return {
        "node_count": len(nodes),
        "edge_count": len(edges),
        "avg_degree": avg_degree,
        "cross_edges": cross_edges,
        "avg_edge_distance": avg_edge_distance,
        "avg_cross_distance": avg_cross_distance,
        "avg_same_distance": avg_same_distance,
        "group_count": len(group_sizes),
        "avg_group_size": avg_group_size,
        "bbox_area": bbox_area
    }

small = load_graph(small_path)
medium = load_graph(medium_path)
large = load_graph(large_path)

print("small  =", small)
print("medium =", medium)
print("large  =", large)

if not (small["node_count"] < medium["node_count"] < large["node_count"]):
    print("❌ Размеры графов не возрастают: Small < Medium < Large")
    sys.exit(1)

if not (small["edge_count"] < medium["edge_count"] < large["edge_count"]):
    print("❌ Число рёбер не возрастает: Small < Medium < Large")
    sys.exit(1)

if not (small["cross_edges"] <= medium["cross_edges"] <= large["cross_edges"]):
    print("❌ Межкластерные связи не показывают роста по масштабу")
    sys.exit(1)

if not (small["bbox_area"] <= medium["bbox_area"] <= large["bbox_area"]):
    print("❌ Пространственный размах графов не возрастает")
    sys.exit(1)

if large["avg_cross_distance"] < medium["avg_cross_distance"]:
    print("❌ Large graph не показывает более дальние межзонные связи, чем Medium")
    sys.exit(1)

if large["avg_degree"] < medium["avg_degree"]:
    print("❌ Large graph не показывает роста средней степени относительно Medium")
    sys.exit(1)

print("✅ Scale-aware различия графов подтверждены")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"