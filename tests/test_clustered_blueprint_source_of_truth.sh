#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_clustered_blueprint_source_of_truth_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: clustered blueprint должен быть source of truth"
echo "============================================================"

CREATE_JSON="$OUT_DIR/create.json"
GRAPH_JSON="$OUT_DIR/graph.json"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"
EDGE_AB="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"
EDGE_BC="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"

curl -s -X POST "$API_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Blueprint source of truth test\",
    \"description\": \"Проверка сохранения ручной структуры clustered blueprint\",
    \"graphType\": 1,
    \"graphScaleType\": 1,
    \"gridWidth\": 30,
    \"gridHeight\": 30,
    \"initialMoistureMin\": 0.20,
    \"initialMoistureMax\": 0.20,
    \"elevationVariation\": 20.0,
    \"initialFireCellsCount\": 1,
    \"simulationSteps\": 30,
    \"stepDurationSeconds\": 900,
    \"randomSeed\": 111222,
    \"mapCreationMode\": 2,
    \"clusteredBlueprint\": {
      \"canvasWidth\": 30,
      \"canvasHeight\": 30,
      \"candidates\": [],
      \"nodes\": [
        {
          \"id\": \"${NODE_A}\",
          \"x\": 3,
          \"y\": 4,
          \"clusterId\": \"alpha\",
          \"vegetation\": 3,
          \"moisture\": 0.12,
          \"elevation\": 2.0
        },
        {
          \"id\": \"${NODE_B}\",
          \"x\": 9,
          \"y\": 5,
          \"clusterId\": \"alpha\",
          \"vegetation\": 4,
          \"moisture\": 0.18,
          \"elevation\": 4.0
        },
        {
          \"id\": \"${NODE_C}\",
          \"x\": 15,
          \"y\": 8,
          \"clusterId\": \"beta\",
          \"vegetation\": 2,
          \"moisture\": 0.27,
          \"elevation\": 7.0
        }
      ],
      \"edges\": [
        {
          \"id\": \"${EDGE_AB}\",
          \"fromNodeId\": \"${NODE_A}\",
          \"toNodeId\": \"${NODE_B}\",
          \"distanceOverride\": 2.50,
          \"fireSpreadModifier\": 1.25
        },
        {
          \"id\": \"${EDGE_BC}\",
          \"fromNodeId\": \"${NODE_B}\",
          \"toNodeId\": \"${NODE_C}\",
          \"distanceOverride\": 3.75,
          \"fireSpreadModifier\": 0.55
        }
      ]
    },
    \"temperature\": 25.0,
    \"humidity\": 40.0,
    \"windSpeed\": 5.0,
    \"windDirection\": 45.0,
    \"precipitation\": 0.0
  }" > "$CREATE_JSON"

SIM_ID=$(jq -r '.id // empty' "$CREATE_JSON")

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$CREATE_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -s "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

if [[ ! -s "$GRAPH_JSON" ]]; then
  echo "❌ Graph endpoint вернул пустой ответ"
  exit 1
fi

GRAPH_SUCCESS=$(jq -r '.success // false' "$GRAPH_JSON" 2>/dev/null || echo "false")
if [[ "$GRAPH_SUCCESS" != "true" ]]; then
  echo "❌ Не удалось получить graph JSON"
  cat "$GRAPH_JSON"
  exit 1
fi

python3 - "$GRAPH_JSON" "$NODE_A" "$NODE_B" "$NODE_C" "$EDGE_AB" "$EDGE_BC" <<'PY'
import json
import math
import sys

graph_path = sys.argv[1]
node_a = sys.argv[2]
node_b = sys.argv[3]
node_c = sys.argv[4]
edge_ab_id = sys.argv[5]
edge_bc_id = sys.argv[6]

with open(graph_path, "r", encoding="utf-8") as f:
    root = json.load(f)

graph = root["graph"]
nodes = graph["nodes"]
edges = graph["edges"]

node_by_id = {n["id"]: n for n in nodes}
edge_by_id = {e["id"]: e for e in edges}

print(f"node_count = {len(nodes)}")
print(f"edge_count = {len(edges)}")

errors = []

if len(nodes) != 3:
    errors.append(f"ожидалось ровно 3 узла, получено {len(nodes)}")

if len(edges) != 2:
    errors.append(f"ожидалось ровно 2 ребра без soft-completion, получено {len(edges)}")

for required_node in [node_a, node_b, node_c]:
    if required_node not in node_by_id:
        errors.append(f"узел {required_node} отсутствует в результате")

for required_edge in [edge_ab_id, edge_bc_id]:
    if required_edge not in edge_by_id:
        errors.append(f"ребро {required_edge} отсутствует в результате")

if errors:
    print("❌ Базовая структура не сохранена")
    for err in errors:
        print("   -", err)
    sys.exit(1)

a = node_by_id[node_a]
b = node_by_id[node_b]
c = node_by_id[node_c]

print(f"node_a = x:{a['x']} y:{a['y']} group:{a.get('groupKey')} vegetation:{a.get('vegetation')} moisture:{a.get('moisture')} elevation:{a.get('elevation')}")
print(f"node_b = x:{b['x']} y:{b['y']} group:{b.get('groupKey')} vegetation:{b.get('vegetation')} moisture:{b.get('moisture')} elevation:{b.get('elevation')}")
print(f"node_c = x:{c['x']} y:{c['y']} group:{c.get('groupKey')} vegetation:{c.get('vegetation')} moisture:{c.get('moisture')} elevation:{c.get('elevation')}")

def approx_equal(x, y, eps=1e-6):
    return abs(float(x) - float(y)) <= eps

if a["x"] != 3 or a["y"] != 4:
    errors.append(f"координаты node A не сохранены: ({a['x']},{a['y']})")

if b["x"] != 9 or b["y"] != 5:
    errors.append(f"координаты node B не сохранены: ({b['x']},{b['y']})")

if c["x"] != 15 or c["y"] != 8:
    errors.append(f"координаты node C не сохранены: ({c['x']},{c['y']})")

if (a.get("groupKey") or "") != "alpha":
    errors.append(f"groupKey node A должен быть alpha, сейчас {a.get('groupKey')}")

if (b.get("groupKey") or "") != "alpha":
    errors.append(f"groupKey node B должен быть alpha, сейчас {b.get('groupKey')}")

if (c.get("groupKey") or "") != "beta":
    errors.append(f"groupKey node C должен быть beta, сейчас {c.get('groupKey')}")

if a.get("vegetation") != "Coniferous":
    errors.append(f"vegetation node A должен быть Coniferous, сейчас {a.get('vegetation')}")

if b.get("vegetation") != "Mixed":
    errors.append(f"vegetation node B должен быть Mixed, сейчас {b.get('vegetation')}")

if c.get("vegetation") != "Deciduous":
    errors.append(f"vegetation node C должен быть Deciduous, сейчас {c.get('vegetation')}")

if not approx_equal(a.get("moisture"), 0.12):
    errors.append(f"moisture node A должен быть 0.12, сейчас {a.get('moisture')}")

if not approx_equal(b.get("moisture"), 0.18):
    errors.append(f"moisture node B должен быть 0.18, сейчас {b.get('moisture')}")

if not approx_equal(c.get("moisture"), 0.27):
    errors.append(f"moisture node C должен быть 0.27, сейчас {c.get('moisture')}")

if not approx_equal(a.get("elevation"), 2.0):
    errors.append(f"elevation node A должен быть 2.0, сейчас {a.get('elevation')}")

if not approx_equal(b.get("elevation"), 4.0):
    errors.append(f"elevation node B должен быть 4.0, сейчас {b.get('elevation')}")

if not approx_equal(c.get("elevation"), 7.0):
    errors.append(f"elevation node C должен быть 7.0, сейчас {c.get('elevation')}")

ab = edge_by_id[edge_ab_id]
bc = edge_by_id[edge_bc_id]

print(f"edge_ab = from:{ab['fromCellId']} to:{ab['toCellId']} distance:{ab['distance']} modifier:{ab['fireSpreadModifier']}")
print(f"edge_bc = from:{bc['fromCellId']} to:{bc['toCellId']} distance:{bc['distance']} modifier:{bc['fireSpreadModifier']}")

ab_pair = {ab["fromCellId"], ab["toCellId"]}
bc_pair = {bc["fromCellId"], bc["toCellId"]}

if ab_pair != {node_a, node_b}:
    errors.append("ребро AB соединяет не те узлы")

if bc_pair != {node_b, node_c}:
    errors.append("ребро BC соединяет не те узлы")

if not approx_equal(ab["distance"], 2.5):
    errors.append(f"distanceOverride для AB должен сохраниться как 2.5, сейчас {ab['distance']}")

if not approx_equal(bc["distance"], 3.75):
    errors.append(f"distanceOverride для BC должен сохраниться как 3.75, сейчас {bc['distance']}")

if not approx_equal(ab["fireSpreadModifier"], 1.25):
    errors.append(f"fireSpreadModifier для AB должен сохраниться как 1.25, сейчас {ab['fireSpreadModifier']}")

if not approx_equal(bc["fireSpreadModifier"], 0.55):
    errors.append(f"fireSpreadModifier для BC должен сохраниться как 0.55, сейчас {bc['fireSpreadModifier']}")

if errors:
    print("❌ Blueprint не является source of truth")
    for err in errors:
        print("   -", err)
    sys.exit(1)

print("✅ Blueprint сохраняет ручную структуру без неожиданных изменений")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"