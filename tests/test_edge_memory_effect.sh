#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_edge_memory_effect_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: edge memory должен реально накапливаться на рёбрах"
echo "============================================================"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"

EDGE_AB="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"
EDGE_BC="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"

CREATE_JSON="$OUT_DIR/create.json"
START_JSON="$OUT_DIR/start.json"
STEP1_JSON="$OUT_DIR/step1.json"
STEP2_JSON="$OUT_DIR/step2.json"
GRAPH0_JSON="$OUT_DIR/graph0.json"
GRAPH1_JSON="$OUT_DIR/graph1.json"
GRAPH2_JSON="$OUT_DIR/graph2.json"

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Edge memory effect test\",
    \"description\": \"Проверка накопления тепла на рёбрах по шагам\",
    \"graphType\": 1,
    \"graphScaleType\": 2,
    \"gridWidth\": 20,
    \"gridHeight\": 10,
    \"initialMoistureMin\": 0.12,
    \"initialMoistureMax\": 0.12,
    \"elevationVariation\": 4.0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 8,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 20260422,
    \"mapCreationMode\": 2,
    \"clusteredBlueprint\": {
      \"canvasWidth\": 20,
      \"canvasHeight\": 10,
      \"candidates\": [],
      \"nodes\": [
        {
          \"id\": \"${NODE_A}\",
          \"x\": 3,
          \"y\": 5,
          \"clusterId\": \"alpha\",
          \"vegetation\": 3,
          \"moisture\": 0.12,
          \"elevation\": 2.0
        },
        {
          \"id\": \"${NODE_B}\",
          \"x\": 8,
          \"y\": 5,
          \"clusterId\": \"alpha\",
          \"vegetation\": 3,
          \"moisture\": 0.12,
          \"elevation\": 2.0
        },
        {
          \"id\": \"${NODE_C}\",
          \"x\": 13,
          \"y\": 5,
          \"clusterId\": \"alpha\",
          \"vegetation\": 3,
          \"moisture\": 0.12,
          \"elevation\": 2.0
        }
      ],
      \"edges\": [
        {
          \"id\": \"${EDGE_AB}\",
          \"fromNodeId\": \"${NODE_A}\",
          \"toNodeId\": \"${NODE_B}\",
          \"distanceOverride\": 3.0,
          \"fireSpreadModifier\": 0.95
        },
        {
          \"id\": \"${EDGE_BC}\",
          \"fromNodeId\": \"${NODE_B}\",
          \"toNodeId\": \"${NODE_C}\",
          \"distanceOverride\": 3.0,
          \"fireSpreadModifier\": 0.95
        }
      ]
    },
    \"initialFirePositions\": [
      { \"x\": 3, \"y\": 5 }
    ],
    \"temperature\": 30.0,
    \"humidity\": 28.0,
    \"windSpeed\": 6.0,
    \"windDirection\": 90.0,
    \"precipitation\": 0.0
  }" > "$CREATE_JSON"

SIM_ID=$(jq -r '.id // empty' "$CREATE_JSON")

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$CREATE_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH0_JSON"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{
    \"ignitionMode\": \"manual\",
    \"initialFirePositions\": [
      { \"x\": 3, \"y\": 5 }
    ]
  }" > "$START_JSON"

START_SUCCESS=$(jq -r '.success // false' "$START_JSON" 2>/dev/null || echo "false")
if [[ "$START_SUCCESS" != "true" ]]; then
  echo "❌ Не удалось запустить симуляцию"
  cat "$START_JSON"
  exit 1
fi

echo "✅ Симуляция запущена"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP1_JSON"
curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH1_JSON"

curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP2_JSON"
curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH2_JSON"

python3 - "$STEP1_JSON" "$STEP2_JSON" "$GRAPH0_JSON" "$GRAPH1_JSON" "$GRAPH2_JSON" "$NODE_B" "$NODE_C" "$EDGE_AB" "$EDGE_BC" <<'PY'
import json
import sys

step1_path, step2_path, graph0_path, graph1_path, graph2_path = sys.argv[1:6]
node_b, node_c, edge_ab, edge_bc = sys.argv[6:10]

def read_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def read_cells(root):
    return {c["id"]: c for c in root.get("cells", [])}

def read_graph(root):
    graph = root["graph"]
    nodes = {n["id"]: n for n in graph["nodes"]}
    edges = {e["id"]: e for e in graph["edges"]}
    return nodes, edges

step1 = read_json(step1_path)
step2 = read_json(step2_path)

nodes0, edges0 = read_graph(read_json(graph0_path))
nodes1, edges1 = read_graph(read_json(graph1_path))
nodes2, edges2 = read_graph(read_json(graph2_path))

cells1 = read_cells(step1)
cells2 = read_cells(step2)

errors = []

for node_id in [node_b, node_c]:
    if node_id not in cells1:
        errors.append(f"шаг 1: отсутствует узел {node_id}")
    if node_id not in cells2:
        errors.append(f"шаг 2: отсутствует узел {node_id}")
    if node_id not in nodes1:
        errors.append(f"graph1: отсутствует узел {node_id}")
    if node_id not in nodes2:
        errors.append(f"graph2: отсутствует узел {node_id}")

for edge_id in [edge_ab, edge_bc]:
    if edge_id not in edges0:
        errors.append(f"graph0: отсутствует ребро {edge_id}")
    if edge_id not in edges1:
        errors.append(f"graph1: отсутствует ребро {edge_id}")
    if edge_id not in edges2:
        errors.append(f"graph2: отсутствует ребро {edge_id}")

if errors:
    print("❌ Базовая структура теста нарушена")
    for err in errors:
        print("   -", err)
    sys.exit(1)

b1 = cells1[node_b]
c1 = cells1[node_c]
b2 = cells2[node_b]
c2 = cells2[node_c]

ab0 = float(edges0[edge_ab].get("accumulatedHeat") or 0.0)
ab1 = float(edges1[edge_ab].get("accumulatedHeat") or 0.0)
ab2 = float(edges2[edge_ab].get("accumulatedHeat") or 0.0)

bc1 = float(edges1[edge_bc].get("accumulatedHeat") or 0.0)
bc2 = float(edges2[edge_bc].get("accumulatedHeat") or 0.0)

b_prob_1 = float(b1.get("burnProbability") or 0.0)
b_prob_2 = float(b2.get("burnProbability") or 0.0)
c_prob_1 = float(c1.get("burnProbability") or 0.0)
c_prob_2 = float(c2.get("burnProbability") or 0.0)

b_heat_1 = float(nodes1[node_b].get("accumulatedHeatJ") or 0.0)
b_heat_2 = float(nodes2[node_b].get("accumulatedHeatJ") or 0.0)

print(f"step1: B_prob={b_prob_1:.6f}, C_prob={c_prob_1:.6f}, B_state={b1.get('state')}, C_state={c1.get('state')}")
print(f"step2: B_prob={b_prob_2:.6f}, C_prob={c_prob_2:.6f}, B_state={b2.get('state')}, C_state={c2.get('state')}")
print(f"edge AB heat: before={ab0:.6f}, after_step1={ab1:.6f}, after_step2={ab2:.6f}")
print(f"edge BC heat: after_step1={bc1:.6f}, after_step2={bc2:.6f}")
print(f"node B heat: step1={b_heat_1:.6f}, step2={b_heat_2:.6f}")

if not (ab1 > ab0):
    print("❌ После первого шага на ребре AB должно появиться накопленное тепло")
    sys.exit(1)

if not (ab2 >= ab1):
    print("❌ Накопленное тепло на ребре AB не должно уменьшаться на раннем этапе")
    sys.exit(1)

if not (b_prob_1 > 0.0 or b_prob_2 > 0.0 or b_heat_1 > 0.0 or b_heat_2 > b_heat_1):
    print("❌ Узел B не показывает runtime-след от накопления тепла")
    sys.exit(1)

if c_prob_2 > c_prob_1 or bc2 > bc1:
    print("✅ Виден вторичный downstream-эффект на направлении к узлу C")
else:
    print("ℹ️ До узла C эффект за 2 шага ещё не дошёл — это допустимо")

print("✅ Edge memory реально накапливается и влияет на локальную динамику")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"