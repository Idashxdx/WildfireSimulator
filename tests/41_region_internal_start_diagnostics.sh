#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"

echo "============================================================"
echo " ТЕСТ: диагностика внутреннего старта RegionClusterGraph"
echo "============================================================"

TMP_DIR="$(mktemp -d)"
GRAPH_JSON="$TMP_DIR/graph.json"
STATUS_JSON="$TMP_DIR/status.json"

SIM_ID=$(
curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"region-internal-diagnostics",
    "description":"diagnostic",
    "gridWidth":20,
    "gridHeight":20,
    "graphType":2,
    "initialMoistureMin":0.15,
    "initialMoistureMax":0.25,
    "elevationVariation":50,
    "initialFireCellsCount":1,
    "simulationSteps":5,
    "stepDurationSeconds":900,
    "randomSeed":123456,
    "temperature":30,
    "humidity":30,
    "windSpeed":0,
    "windDirection":0,
    "precipitation":0
  }' | jq -r '.id'
)

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

START_INFO=$(
python3 - "$GRAPH_JSON" << 'PY'
import json, sys

with open(sys.argv[1], "r", encoding="utf-8") as f:
    graph = json.load(f)["graph"]

nodes = graph["nodes"]
edges = graph["edges"]
by_id = {n["id"]: n for n in nodes}

def ignitable(n):
    veg = (n.get("vegetation") or "").strip().lower()
    return veg not in ("water", "bare")

best = None
best_score = None

for n in nodes:
    if not ignitable(n):
        continue

    incident = [e for e in edges if e["fromCellId"] == n["id"] or e["toCellId"] == n["id"]]
    same = []
    inter = []

    for e in incident:
        oid = e["toCellId"] if e["fromCellId"] == n["id"] else e["fromCellId"]
        other = by_id[oid]
        if other["groupKey"] == n["groupKey"]:
            same.append((other, e))
        else:
            inter.append((other, e))

    ign_same = [(o,e) for o,e in same if ignitable(o)]

    if len(inter) != 0:
        continue
    if len(ign_same) < 3:
        continue

    avg_mod = sum(e["fireSpreadModifier"] for _,e in ign_same) / len(ign_same)
    score = (-len(ign_same), -avg_mod, n["y"], n["x"])

    if best is None or score < best_score:
        best = (n, ign_same, inter)
        best_score = score

if best is None:
    sys.exit(1)

n, ign_same, inter = best
print(f'{n["x"]},{n["y"]},{n["groupKey"]},{len(ign_same)}')
PY
) || {
  echo "❌ Не удалось выбрать диагностический внутренний узел"
  exit 1
}

START_X="$(echo "$START_INFO" | cut -d',' -f1)"
START_Y="$(echo "$START_INFO" | cut -d',' -f2)"
START_CLUSTER="$(echo "$START_INFO" | cut -d',' -f3)"
START_LOCAL_COUNT="$(echo "$START_INFO" | cut -d',' -f4)"

echo "start = ($START_X,$START_Y)"
echo "start_cluster = $START_CLUSTER"
echo "local_ignitable_neighbors = $START_LOCAL_COUNT"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{
    \"ignitionMode\":\"manual\",
    \"initialFirePositions\":[{\"x\":$START_X,\"y\":$START_Y}]
  }" >/dev/null

for i in 1 2 3; do
  curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" >/dev/null
  curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

  echo "---- step=$i ----"
  python3 - "$GRAPH_JSON" "$START_X" "$START_Y" "$START_CLUSTER" << 'PY'
import json, sys

graph_file, sx, sy, cluster = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]

with open(graph_file, "r", encoding="utf-8") as f:
    graph = json.load(f)["graph"]

nodes = graph["nodes"]
edges = graph["edges"]

start = next(n for n in nodes if n["x"] == sx and n["y"] == sy)
by_id = {n["id"]: n for n in nodes}

incident = [e for e in edges if e["fromCellId"] == start["id"] or e["toCellId"] == start["id"]]

for e in sorted(incident, key=lambda x: -x["fireSpreadModifier"]):
    oid = e["toCellId"] if e["fromCellId"] == start["id"] else e["fromCellId"]
    n = by_id[oid]
    kind = "LOCAL" if n["groupKey"] == cluster else "BRIDGE"
    print(
        f'{kind} neighbor=({n["x"]},{n["y"]}) '
        f'state={n["state"]} '
        f'mod={e["fireSpreadModifier"]:.4f} '
        f'dist={e["distance"]:.4f} '
        f'prob={n["burnProbability"]:.6f}'
    )
PY
done