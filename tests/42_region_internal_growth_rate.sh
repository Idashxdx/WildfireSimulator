#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"

echo "============================================================"
echo " ТЕСТ: доля внутренних стартов, дающих локальный рост"
echo "============================================================"

TMP_DIR="$(mktemp -d)"
GRAPH_JSON="$TMP_DIR/graph.json"

BASE_SIM_ID=$(
curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name":"region-growth-rate-base",
    "description":"growth rate base",
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

curl -s "$API_URL/api/SimulationManager/$BASE_SIM_ID/graph" > "$GRAPH_JSON"

mapfile -t STARTS < <(
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

candidates = []
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
            same.append(other)
        else:
            inter.append(other)
    ign_same = [x for x in same if ignitable(x)]
    if len(inter) == 0 and len(ign_same) >= 3:
        candidates.append((n["x"], n["y"], n["groupKey"], len(ign_same)))

for x,y,g,cnt in candidates[:10]:
    print(f"{x},{y},{g},{cnt}")
PY
)

if [[ "${#STARTS[@]}" -eq 0 ]]; then
  echo "❌ Не найдено внутренних узлов-кандидатов"
  exit 1
fi

SUCCESS=0
TOTAL=0

for ITEM in "${STARTS[@]}"; do
  X="$(echo "$ITEM" | cut -d',' -f1)"
  Y="$(echo "$ITEM" | cut -d',' -f2)"
  CLUSTER="$(echo "$ITEM" | cut -d',' -f3)"

  SIM_ID=$(
  curl -s -X POST "$API_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d '{
      "name":"region-growth-rate-run",
      "description":"growth rate run",
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

  curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
    -H "Content-Type: application/json" \
    -d "{
      \"ignitionMode\":\"manual\",
      \"initialFirePositions\":[{\"x\":$X,\"y\":$Y}]
    }" >/dev/null

  LOCAL_GROWTH=0

  for step in 1 2 3; do
    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" >/dev/null
    curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

    INSIDE=$(jq --arg c "$CLUSTER" '
      [.graph.nodes[]
        | select((.state == "Burning" or .state == "Burned") and .groupKey == $c)
      ] | length
    ' "$GRAPH_JSON")

    if [[ "$INSIDE" -ge 2 ]]; then
      LOCAL_GROWTH=1
      break
    fi
  done

  TOTAL=$((TOTAL + 1))
  if [[ "$LOCAL_GROWTH" -eq 1 ]]; then
    SUCCESS=$((SUCCESS + 1))
  fi

  echo "start=($X,$Y) cluster=$CLUSTER local_growth=$LOCAL_GROWTH"
done

echo "success = $SUCCESS"
echo "total   = $TOTAL"

if [[ "$SUCCESS" -ge $((TOTAL / 2)) ]]; then
  echo "✅ Локальный рост наблюдается хотя бы у значимой части внутренних стартов"
  exit 0
fi

echo "❌ Локальный рост слишком нестабилен даже для внутренних стартов"
exit 1