#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"
START_JSON="$TMP_DIR/start.json"
STEP_JSON="$TMP_DIR/step.json"

echo "============================================================"
echo " ТЕСТ 11.4: сильные рёбра должны давать больший burnProbability"
echo "============================================================"

create_payload='{
  "name": "test-fire-spread-modifier-probability",
  "description": "edge modifier probability test",
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
    if len(node_edges) < 6:
        continue

    modifiers = sorted(e["fireSpreadModifier"] for e in node_edges)
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
  echo "❌ Не удалось подобрать стартовую вершину"
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
    print("❌ В ответе шага нет cells")
    sys.exit(1)

cell_by_xy = {(c["x"], c["y"]): c for c in cells}

incident = defaultdict(list)
for e in edges:
    incident[e["fromCellId"]].append(e)
    incident[e["toCellId"]].append(e)

candidate = None
best_score = None

for node_id, node_edges in incident.items():
    if len(node_edges) < 6:
        continue

    modifiers = sorted(e["fireSpreadModifier"] for e in node_edges)
    spread = modifiers[-1] - modifiers[0]
    avg = sum(modifiers) / len(modifiers)

    score = (spread, avg, len(node_edges))
    if best_score is None or score > best_score:
        best_score = score
        candidate = node_id

if candidate is None:
    print("❌ Не нашли тестовую вершину")
    sys.exit(1)

source = nodes[candidate]

neighbors = []
for e in incident[candidate]:
    other_id = e["toCellId"] if e["fromCellId"] == candidate else e["fromCellId"]
    other = nodes[other_id]
    step_cell = cell_by_xy.get((other["x"], other["y"]))
    if step_cell is None:
        continue

    neighbors.append({
        "x": other["x"],
        "y": other["y"],
        "modifier": e["fireSpreadModifier"],
        "burnProbability": step_cell.get("burnProbability", 0.0)
    })

neighbors.sort(key=lambda n: n["modifier"])

if len(neighbors) < 6:
    print("❌ Недостаточно соседей после шага")
    sys.exit(1)

half = len(neighbors) // 2
weak = neighbors[:half]
strong = neighbors[half:]

weak_avg_modifier = sum(n["modifier"] for n in weak) / len(weak)
strong_avg_modifier = sum(n["modifier"] for n in strong) / len(strong)

weak_avg_prob = sum(n["burnProbability"] for n in weak) / len(weak)
strong_avg_prob = sum(n["burnProbability"] for n in strong) / len(strong)

weak_max_prob = max(n["burnProbability"] for n in weak)
strong_max_prob = max(n["burnProbability"] for n in strong)

weak_positive = sum(1 for n in weak if n["burnProbability"] > 0.0)
strong_positive = sum(1 for n in strong if n["burnProbability"] > 0.0)

print("source =", (source["x"], source["y"]))
print("neighbor_count =", len(neighbors))
print("weak_avg_modifier =", round(weak_avg_modifier, 4))
print("strong_avg_modifier =", round(strong_avg_modifier, 4))
print("weak_avg_burn_probability =", round(weak_avg_prob, 6))
print("strong_avg_burn_probability =", round(strong_avg_prob, 6))
print("weak_max_burn_probability =", round(weak_max_prob, 6))
print("strong_max_burn_probability =", round(strong_max_prob, 6))
print("weak_positive_probability_count =", weak_positive)
print("strong_positive_probability_count =", strong_positive)

if strong_avg_modifier <= weak_avg_modifier:
    print("❌ Некорректное разделение weak/strong")
    sys.exit(1)

if strong_avg_prob <= weak_avg_prob:
    print("❌ Сильные рёбра не дали больший средний burnProbability")
    sys.exit(1)

if strong_max_prob < weak_max_prob:
    print("❌ Максимальный burnProbability по сильным рёбрам хуже, чем по слабым")
    sys.exit(1)

if strong_positive < weak_positive:
    print("❌ Сильные рёбра затронули меньше соседей с ненулевой вероятностью")
    sys.exit(1)

print("✅ FireSpreadModifier реально усиливает распространение по сильным рёбрам")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.4 ПРОЙДЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"