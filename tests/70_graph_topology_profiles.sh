#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/_test_lib.sh"

require_tools

OUT_DIR="/tmp/test_graph_topology_profiles_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: Small / Medium / Large graph topology profiles"
echo "============================================================"

make_graph() {
  local label="$1"
  local scale="$2"
  local width="$3"
  local height="$4"

  local create_json="$OUT_DIR/${label}_create.json"
  local graph_json="$OUT_DIR/${label}_graph.json"

  create_graph_sim "$create_json" \
    "$label" \
    "$scale" \
    "$width" "$height" \
    0.20 0.60 \
    45 \
    10 900 \
    424242 \
    26 40 5 45 0

  local sim_id
  sim_id="$(get_sim_id "$create_json")"
  fetch_graph "$sim_id" "$graph_json"

  echo "$graph_json"
}

SMALL_GRAPH="$(make_graph small 0 14 14)"
MEDIUM_GRAPH="$(make_graph medium 1 24 24)"
LARGE_GRAPH="$(make_graph large 2 34 34)"

python3 - "$SMALL_GRAPH" "$MEDIUM_GRAPH" "$LARGE_GRAPH" <<'PY'
import json
import sys
from collections import Counter, defaultdict, deque

small_path, medium_path, large_path = sys.argv[1:4]

def load(path):
    with open(path, "r", encoding="utf-8") as f:
        g = json.load(f)["graph"]
    nodes = g["nodes"]
    edges = g["edges"]
    ids = {n["id"]: n for n in nodes}
    groups = Counter((n.get("groupKey") or "ungrouped") for n in nodes)

    cross = []
    same = []
    adj = defaultdict(set)

    for e in edges:
        a = ids[e["fromCellId"]]
        b = ids[e["toCellId"]]
        adj[a["id"]].add(b["id"])
        adj[b["id"]].add(a["id"])
        if (a.get("groupKey") or "") == (b.get("groupKey") or ""):
            same.append(e)
        else:
            cross.append(e)

    visited = set()
    if nodes:
        q = deque([nodes[0]["id"]])
        visited.add(nodes[0]["id"])
        while q:
            v = q.popleft()
            for to in adj[v]:
                if to not in visited:
                    visited.add(to)
                    q.append(to)

    avg_degree = 2 * len(edges) / max(1, len(nodes))
    avg_same_dist = sum(e["distance"] for e in same) / max(1, len(same))
    avg_cross_dist = sum(e["distance"] for e in cross) / max(1, len(cross))

    return {
        "nodes": len(nodes),
        "edges": len(edges),
        "groups": len(groups),
        "cross": len(cross),
        "same": len(same),
        "connected": len(visited) == len(nodes),
        "avg_degree": avg_degree,
        "avg_same_dist": avg_same_dist,
        "avg_cross_dist": avg_cross_dist,
    }

small = load(small_path)
medium = load(medium_path)
large = load(large_path)

print("small =", small)
print("medium =", medium)
print("large =", large)

if not (8 <= small["nodes"] <= 24):
    print("❌ SmallGraph node count вне диапазона")
    sys.exit(1)

if small["groups"] != 1:
    print("❌ SmallGraph должен быть одной областью")
    sys.exit(1)

if small["cross"] != 0:
    print("❌ SmallGraph не должен иметь межобластные связи")
    sys.exit(1)

if not small["connected"]:
    print("❌ SmallGraph не связен")
    sys.exit(1)

if not (45 <= medium["nodes"] <= 90):
    print("❌ MediumGraph node count вне диапазона")
    sys.exit(1)

if medium["groups"] < 2:
    print("❌ MediumGraph должен иметь несколько областей")
    sys.exit(1)

if medium["cross"] < 1:
    print("❌ MediumGraph должен иметь мосты")
    sys.exit(1)

if medium["avg_cross_dist"] <= medium["avg_same_dist"]:
    print("❌ MediumGraph мосты должны быть длиннее локальных связей")
    sys.exit(1)

if not medium["connected"]:
    print("❌ MediumGraph не связен")
    sys.exit(1)

if large["nodes"] < 80:
    print("❌ LargeGraph слишком мал")
    sys.exit(1)

if large["groups"] < 4:
    print("❌ LargeGraph должен иметь макрозоны")
    sys.exit(1)

if large["cross"] < 4:
    print("❌ LargeGraph должен иметь несколько corridor-like связей")
    sys.exit(1)

if large["avg_cross_dist"] <= large["avg_same_dist"]:
    print("❌ LargeGraph межзонные связи должны быть длиннее локальных")
    sys.exit(1)

if not large["connected"]:
    print("❌ LargeGraph не связен")
    sys.exit(1)

print("✅ Small/Medium/Large имеют разные и корректные структуры")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "============================================================"