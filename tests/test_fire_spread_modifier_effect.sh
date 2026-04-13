#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"
START_JSON="$TMP_DIR/start.json"
STEP_JSON="$TMP_DIR/step.json"

echo "============================================================"
echo " ТЕСТ 11.3: FireSpreadModifier должен реально влиять на распространение"
echo "============================================================"

create_payload='{
  "name": "test-fire-spread-modifier-effect",
  "description": "edge modifier physics test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 1,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.30,
  "elevationVariation": 0,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 900,
  "randomSeed": 20260408,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 0,
  "windDirection": 45,
  "precipitation": 0
}'

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$create_payload" > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -sS "$BASE_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

MANUAL_POS="$(python3 - "$GRAPH_JSON" << 'PY'
import json, sys
from collections import defaultdict

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

graph = data["graph"]
nodes = {n["id"]: n for n in graph["nodes"]}
edges = graph["edges"]

incident = defaultdict(list)
for e in edges:
    incident[e["fromCellId"]].append(e)
    incident[e["toCellId"]].append(e)

candidate = None
best_score = None

for node_id, node_edges in incident.items():
    if len(node_edges) < 4:
        continue

    modifiers = sorted([e["fireSpreadModifier"] for e in node_edges])
    spread = modifiers[-1] - modifiers[0]
    avg = sum(modifiers) / len(modifiers)

    score = (spread, avg, len(node_edges))
    if best_score is None or score > best_score:
        best_score = score
        candidate = nodes[node_id]

if candidate is None:
    print("")
    sys.exit(0)

print(json.dumps([{"x": candidate["x"], "y": candidate["y"]}]))
PY
)"

if [[ -z "$MANUAL_POS" ]]; then
  echo "❌ Не удалось подобрать стартовую вершину для теста"
  exit 1
fi

echo "manual_start = $MANUAL_POS"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{
    \"ignitionMode\": \"manual\",
    \"initialFirePositions\": $MANUAL_POS
  }" > "$START_JSON"

START_OK="$(jq -r '.success // empty' "$START_JSON")"
if [[ "$START_OK" != "true" ]]; then
  echo "❌ Не удалось запустить симуляцию"
  cat "$START_JSON"
  exit 1
fi

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_JSON"

python3 - "$GRAPH_JSON" "$STEP_JSON" << 'PY'
import json, sys
from collections import defaultdict

graph_path, step_path = sys.argv[1], sys.argv[2]

with open(graph_path, "r", encoding="utf-8") as f:
    graph_data = json.load(f)

with open(step_path, "r", encoding="utf-8") as f:
    step_data = json.load(f)

graph = graph_data["graph"]
nodes = {n["id"]: n for n in graph["nodes"]}
edges = graph["edges"]
cells = step_data.get("cells", [])

if not cells:
    print("❌ В step-ответе нет cells")
    sys.exit(1)

state_by_xy = {(c["x"], c["y"]): c["state"] for c in cells}

burning_nodes = {(c["x"], c["y"]) for c in cells if c["state"] == "Burning"}
burned_nodes = {(c["x"], c["y"]) for c in cells if c["state"] == "Burned"}

affected = burning_nodes | burned_nodes

incident = defaultdict(list)
for e in edges:
    incident[e["fromCellId"]].append(e)
    incident[e["toCellId"]].append(e)

candidate = None
best_score = None

for node_id, node_edges in incident.items():
    if len(node_edges) < 4:
        continue

    modifiers = sorted([e["fireSpreadModifier"] for e in node_edges])
    spread = modifiers[-1] - modifiers[0]
    avg = sum(modifiers) / len(modifiers)
    score = (spread, avg, len(node_edges))

    if best_score is None or score > best_score:
        best_score = score
        candidate = node_id

if candidate is None:
    print("❌ Не нашли подходящую тестовую вершину")
    sys.exit(1)

source = nodes[candidate]

neighbor_info = []
for e in incident[candidate]:
    other_id = e["toCellId"] if e["fromCellId"] == candidate else e["fromCellId"]
    other = nodes[other_id]
    neighbor_info.append({
        "x": other["x"],
        "y": other["y"],
        "modifier": e["fireSpreadModifier"]
    })

neighbor_info.sort(key=lambda x: x["modifier"])

weak_half = neighbor_info[:len(neighbor_info)//2]
strong_half = neighbor_info[len(neighbor_info)//2:]

weak_affected = sum(1 for n in weak_half if (n["x"], n["y"]) in affected)
strong_affected = sum(1 for n in strong_half if (n["x"], n["y"]) in affected)

weak_avg = sum(n["modifier"] for n in weak_half) / len(weak_half) if weak_half else 0
strong_avg = sum(n["modifier"] for n in strong_half) / len(strong_half) if strong_half else 0

print("source =", (source["x"], source["y"]))
print("neighbor_count =", len(neighbor_info))
print("weak_half_count =", len(weak_half))
print("strong_half_count =", len(strong_half))
print("weak_avg_modifier =", round(weak_avg, 4))
print("strong_avg_modifier =", round(strong_avg, 4))
print("weak_affected =", weak_affected)
print("strong_affected =", strong_affected)

if len(weak_half) == 0 or len(strong_half) == 0:
    print("❌ Недостаточно соседей для сравнения")
    sys.exit(1)

if strong_avg <= weak_avg:
    print("❌ Разделение на слабые/сильные связи получилось некорректным")
    sys.exit(1)

if strong_affected < weak_affected:
    print("❌ Сильные связи не показали лучшего или хотя бы не худшего распространения")
    sys.exit(1)

print("✅ Сильные связи влияют на распространение не хуже слабых")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.3 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"