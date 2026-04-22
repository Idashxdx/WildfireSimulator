#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/test_corridor_runtime_spread_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: corridor должен усиливать runtime spread"
echo "============================================================"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"
NODE_D="44444444-4444-4444-4444-444444444444"

EDGE_AB="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"
EDGE_CD="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"
EDGE_BC="cccccccc-cccc-cccc-cccc-ccccccccccc3"

STEP_COUNT=4

create_simulation() {
  local mode="$1"
  local create_json="$2"

  local bc_edge_json=""
  if [[ "$mode" == "with_corridor" ]]; then
    bc_edge_json=",
        {
          \"id\": \"${EDGE_BC}\",
          \"fromNodeId\": \"${NODE_B}\",
          \"toNodeId\": \"${NODE_C}\",
          \"distanceOverride\": 8.0,
          \"fireSpreadModifier\": 1.60
        }"
  fi

  curl -s -X POST "$API_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"Corridor runtime spread test (${mode})\",
      \"description\": \"Сравнение динамики распространения с corridor и без него\",
      \"graphType\": 1,
      \"graphScaleType\": 2,
      \"gridWidth\": 24,
      \"gridHeight\": 12,
      \"initialMoistureMin\": 0.10,
      \"initialMoistureMax\": 0.18,
      \"elevationVariation\": 8.0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 10,
      \"stepDurationSeconds\": 1800,
      \"randomSeed\": 20260422,
      \"mapCreationMode\": 2,
      \"clusteredBlueprint\": {
        \"canvasWidth\": 24,
        \"canvasHeight\": 12,
        \"candidates\": [],
        \"nodes\": [
          {
            \"id\": \"${NODE_A}\",
            \"x\": 3,
            \"y\": 5,
            \"clusterId\": \"west\",
            \"vegetation\": 3,
            \"moisture\": 0.10,
            \"elevation\": 2.0
          },
          {
            \"id\": \"${NODE_B}\",
            \"x\": 5,
            \"y\": 5,
            \"clusterId\": \"west\",
            \"vegetation\": 3,
            \"moisture\": 0.12,
            \"elevation\": 2.5
          },
          {
            \"id\": \"${NODE_C}\",
            \"x\": 19,
            \"y\": 5,
            \"clusterId\": \"east\",
            \"vegetation\": 3,
            \"moisture\": 0.10,
            \"elevation\": 2.0
          },
          {
            \"id\": \"${NODE_D}\",
            \"x\": 21,
            \"y\": 5,
            \"clusterId\": \"east\",
            \"vegetation\": 3,
            \"moisture\": 0.12,
            \"elevation\": 2.5
          }
        ],
        \"edges\": [
          {
            \"id\": \"${EDGE_AB}\",
            \"fromNodeId\": \"${NODE_A}\",
            \"toNodeId\": \"${NODE_B}\",
            \"distanceOverride\": 2.0,
            \"fireSpreadModifier\": 1.20
          },
          {
            \"id\": \"${EDGE_CD}\",
            \"fromNodeId\": \"${NODE_C}\",
            \"toNodeId\": \"${NODE_D}\",
            \"distanceOverride\": 2.0,
            \"fireSpreadModifier\": 1.20
          }
          ${bc_edge_json}
        ]
      },
      \"initialFirePositions\": [
        { \"x\": 3, \"y\": 5 }
      ],
      \"temperature\": 34.0,
      \"humidity\": 22.0,
      \"windSpeed\": 12.0,
      \"windDirection\": 90.0,
      \"precipitation\": 0.0
    }" > "$create_json"
}

run_steps() {
  local sim_id="$1"
  local prefix="$2"

  local start_json="$OUT_DIR/${prefix}_start.json"
  curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d "{
      \"ignitionMode\": \"manual\",
      \"initialFirePositions\": [
        { \"x\": 3, \"y\": 5 }
      ]
    }" > "$start_json"

  local start_success
  start_success=$(jq -r '.success // false' "$start_json" 2>/dev/null || echo "false")
  if [[ "$start_success" != "true" ]]; then
    echo "❌ Не удалось запустить симуляцию $sim_id" >&2
    cat "$start_json" >&2
    exit 1
  fi

  echo "✅ Запущена симуляция $prefix: $sim_id" >&2

  local last_step_json=""

  for step in $(seq 1 "$STEP_COUNT"); do
    last_step_json="$OUT_DIR/${prefix}_step_${step}.json"

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/step" > "$last_step_json"

    if [[ ! -s "$last_step_json" ]]; then
      echo "❌ Step endpoint вернул пустой ответ для $prefix step=$step" >&2
      exit 1
    fi

    local step_success
    step_success=$(jq -r '.success // false' "$last_step_json" 2>/dev/null || echo "false")
    if [[ "$step_success" != "true" ]]; then
      echo "❌ Не удалось выполнить шаг для $prefix step=$step" >&2
      cat "$last_step_json" >&2
      exit 1
    fi
  done

  echo "$last_step_json"
}

WITH_CREATE_JSON="$OUT_DIR/with_corridor_create.json"
WITHOUT_CREATE_JSON="$OUT_DIR/without_corridor_create.json"

create_simulation "with_corridor" "$WITH_CREATE_JSON"
create_simulation "without_corridor" "$WITHOUT_CREATE_JSON"

SIM_WITH=$(jq -r '.id // empty' "$WITH_CREATE_JSON")
SIM_WITHOUT=$(jq -r '.id // empty' "$WITHOUT_CREATE_JSON")

if [[ -z "$SIM_WITH" || "$SIM_WITH" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию with_corridor"
  cat "$WITH_CREATE_JSON"
  exit 1
fi

if [[ -z "$SIM_WITHOUT" || "$SIM_WITHOUT" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию without_corridor"
  cat "$WITHOUT_CREATE_JSON"
  exit 1
fi

echo "sim_with_corridor    = $SIM_WITH"
echo "sim_without_corridor = $SIM_WITHOUT"

WITH_GRAPH_JSON="$OUT_DIR/with_corridor_graph.json"
WITHOUT_GRAPH_JSON="$OUT_DIR/without_corridor_graph.json"

curl -s "$API_URL/api/SimulationManager/$SIM_WITH/graph" > "$WITH_GRAPH_JSON"
curl -s "$API_URL/api/SimulationManager/$SIM_WITHOUT/graph" > "$WITHOUT_GRAPH_JSON"

python3 - "$WITH_GRAPH_JSON" "$WITHOUT_GRAPH_JSON" "$EDGE_BC" <<'PY'
import json
import sys

with_graph = sys.argv[1]
without_graph = sys.argv[2]
edge_bc = sys.argv[3]

with open(with_graph, "r", encoding="utf-8") as f:
    with_root = json.load(f)

with open(without_graph, "r", encoding="utf-8") as f:
    without_root = json.load(f)

with_edges = with_root["graph"]["edges"]
without_edges = without_root["graph"]["edges"]

with_edge_ids = {e["id"] for e in with_edges}
without_edge_ids = {e["id"] for e in without_edges}

if edge_bc not in with_edge_ids:
    print("❌ В варианте with_corridor отсутствует corridor edge")
    sys.exit(1)

if edge_bc in without_edge_ids:
    print("❌ В варианте without_corridor corridor edge не должен существовать")
    sys.exit(1)

corridor = next(e for e in with_edges if e["id"] == edge_bc)
print(
    f"corridor_edge = from:{corridor['fromCellId']} "
    f"to:{corridor['toCellId']} "
    f"distance:{corridor['distance']} "
    f"modifier:{corridor['fireSpreadModifier']} "
    f"isCorridor:{corridor.get('isCorridor')}"
)

if corridor.get("isCorridor") is not True:
    print("❌ corridor edge должен приходить как isCorridor=true")
    sys.exit(1)

print("✅ Structural corridor pipeline корректен")
PY

WITH_LAST_STEP_JSON=$(run_steps "$SIM_WITH" "with_corridor")
WITHOUT_LAST_STEP_JSON=$(run_steps "$SIM_WITHOUT" "without_corridor")

python3 - "$WITH_LAST_STEP_JSON" "$WITHOUT_LAST_STEP_JSON" "$NODE_C" "$NODE_D" <<'PY'
import json
import sys

with_step = sys.argv[1]
without_step = sys.argv[2]
node_c = sys.argv[3]
node_d = sys.argv[4]

def read_cells(path):
    with open(path, "r", encoding="utf-8") as f:
        root = json.load(f)
    cells = root.get("cells", [])
    return {c["id"]: c for c in cells}

def east_signal(cell_by_id):
    c = cell_by_id[node_c]
    d = cell_by_id[node_d]

    probs = [
        float(c.get("burnProbability") or 0.0),
        float(d.get("burnProbability") or 0.0),
    ]

    affected = 0
    for cell in (c, d):
        if cell.get("state") != "Normal" or float(cell.get("burnProbability") or 0.0) > 0.0:
            affected += 1

    return {
        "c_prob": probs[0],
        "d_prob": probs[1],
        "sum_prob": sum(probs),
        "affected": affected,
        "c_state": c.get("state"),
        "d_state": d.get("state"),
    }

with_cells = read_cells(with_step)
without_cells = read_cells(without_step)

for required in [node_c, node_d]:
    if required not in with_cells:
        print(f"❌ with_corridor: отсутствует узел {required} в step response")
        sys.exit(1)
    if required not in without_cells:
        print(f"❌ without_corridor: отсутствует узел {required} в step response")
        sys.exit(1)

with_sig = east_signal(with_cells)
without_sig = east_signal(without_cells)

print(f"with_corridor: c_prob={with_sig['c_prob']:.6f}, d_prob={with_sig['d_prob']:.6f}, affected={with_sig['affected']}, states=({with_sig['c_state']},{with_sig['d_state']})")
print(f"without_corridor: c_prob={without_sig['c_prob']:.6f}, d_prob={without_sig['d_prob']:.6f}, affected={without_sig['affected']}, states=({without_sig['c_state']},{without_sig['d_state']})")

if with_sig["sum_prob"] <= without_sig["sum_prob"] and with_sig["affected"] <= without_sig["affected"]:
    print("❌ Corridor не усилил runtime spread по сравнению с графом без corridor")
    sys.exit(1)

print("✅ Corridor усиливает runtime spread по сравнению с графом без corridor")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"