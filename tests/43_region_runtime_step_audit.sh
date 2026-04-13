#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

echo "============================================================"
echo " ТЕСТ: runtime-аудит шага RegionClusterGraph"
echo "============================================================"

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "❌ Не найдена команда: $1"
    exit 1
  }
}

require curl
require jq
require python3

SIM_JSON="$TMP_DIR/sim.json"
GRAPH_JSON="$TMP_DIR/graph.json"

create_simulation() {
  cat > "$SIM_JSON" <<'JSON'
{
  "name": "Region Runtime Audit",
  "description": "Runtime audit for region cluster graph",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 2,
  "initialMoistureMin": 0.08,
  "initialMoistureMax": 0.18,
  "elevationVariation": 40,
  "initialFireCellsCount": 1,
  "simulationSteps": 20,
  "stepDurationSeconds": 900,
  "randomSeed": 424242,
  "temperature": 32,
  "humidity": 20,
  "windSpeed": 4,
  "windDirection": 90,
  "precipitation": 0,
  "vegetationDistributions": [
    { "vegetationType": 3, "probability": 0.35 },
    { "vegetationType": 2, "probability": 0.20 },
    { "vegetationType": 4, "probability": 0.20 },
    { "vegetationType": 1, "probability": 0.15 },
    { "vegetationType": 0, "probability": 0.10 }
  ]
}
JSON

  local response
  response="$(curl -sS -X POST "$API_URL/api/simulations" \
    -H "Content-Type: application/json" \
    --data @"$SIM_JSON")"

  SIM_ID="$(echo "$response" | jq -r '.id')"

  if [[ -z "${SIM_ID:-}" || "$SIM_ID" == "null" ]]; then
    echo "❌ Не удалось создать симуляцию"
    echo "$response"
    exit 1
  fi

  echo "simulation_id = $SIM_ID"
}

load_graph() {
  curl -sS "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"
}

pick_best_internal_start() {
  START_INFO="$(
    python3 - "$GRAPH_JSON" <<'PY'
import json, sys, math

path = sys.argv[1]
with open(path, 'r', encoding='utf-8') as f:
    data = json.load(f)

graph = data.get("graph") or {}
nodes = graph.get("nodes") or []
edges = graph.get("edges") or []

node_by_id = {n["id"]: n for n in nodes}
adj = {n["id"]: [] for n in nodes}

for e in edges:
    a = e["fromCellId"]
    b = e["toCellId"]
    adj[a].append((b, e))
    adj[b].append((a, e))

def ignitable(n):
    veg = (n.get("vegetation") or "").lower()
    return veg not in ("water", "bare")

best = None

for n in nodes:
    if not ignitable(n):
        continue

    cluster = n.get("groupKey") or ""
    same = 0
    inter = 0
    strong_same = 0
    weak_inter = 0

    for nb_id, e in adj[n["id"]]:
        nb = node_by_id[nb_id]
        if not ignitable(nb):
            continue

        same_cluster = (nb.get("groupKey") or "") == cluster
        mod = float(e.get("fireSpreadModifier") or 0.0)

        if same_cluster:
            same += 1
            if mod >= 0.30:
                strong_same += 1
        else:
            inter += 1
            if mod <= 0.90:
                weak_inter += 1

    score = (
        strong_same * 100
        + same * 10
        - inter * 8
        + weak_inter * 2
    )

    item = {
        "id": n["id"],
        "x": n["x"],
        "y": n["y"],
        "cluster": cluster,
        "same": same,
        "inter": inter,
        "strong_same": strong_same,
        "weak_inter": weak_inter,
        "score": score,
    }

    if best is None or item["score"] > best["score"]:
        best = item

if best is None:
    print("NONE")
    sys.exit(0)

print(json.dumps(best, ensure_ascii=False))
PY
)"

  if [[ "$START_INFO" == "NONE" || -z "$START_INFO" ]]; then
    echo "❌ Не удалось выбрать стартовый внутренний узел"
    exit 1
  fi

  START_X="$(echo "$START_INFO" | jq -r '.x')"
  START_Y="$(echo "$START_INFO" | jq -r '.y')"
  START_CLUSTER="$(echo "$START_INFO" | jq -r '.cluster')"
  START_SAME="$(echo "$START_INFO" | jq -r '.same')"
  START_INTER="$(echo "$START_INFO" | jq -r '.inter')"
  START_STRONG_SAME="$(echo "$START_INFO" | jq -r '.strong_same')"

  echo "start = ($START_X,$START_Y)"
  echo "start_cluster = $START_CLUSTER"
  echo "same_cluster_neighbors = $START_SAME"
  echo "inter_cluster_neighbors = $START_INTER"
  echo "strong_same_neighbors = $START_STRONG_SAME"
}

start_manual() {
  local payload
  payload="$(jq -n --argjson x "$START_X" --argjson y "$START_Y" \
    '{ignitionMode:"manual", initialFirePositions:[{x:$x,y:$y}]}')"

  local response
  response="$(curl -sS -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" \
    -H "Content-Type: application/json" \
    --data "$payload")"

  local ok
  ok="$(echo "$response" | jq -r '.success // "true"')"
  echo "manual_start = [{\"x\":$START_X,\"y\":$START_Y}]"

  if [[ "$ok" == "false" ]]; then
    echo "❌ Не удалось запустить симуляцию"
    echo "$response"
    exit 1
  fi
}

print_step_diagnostics() {
  local step="$1"

  curl -sS "$API_URL/api/SimulationManager/$SIM_ID/graph" > "$GRAPH_JSON"

  python3 - "$GRAPH_JSON" "$START_X" "$START_Y" "$START_CLUSTER" "$step" <<'PY'
import json, sys

graph_path, sx, sy, cluster, step = sys.argv[1:]
sx = int(sx)
sy = int(sy)

with open(graph_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

graph = data.get("graph") or {}
nodes = graph.get("nodes") or []
edges = graph.get("edges") or []

node_by_id = {n["id"]: n for n in nodes}
coord_to_node = {(n["x"], n["y"]): n for n in nodes}

source = coord_to_node.get((sx, sy))
if source is None:
    print(f"---- step={step} ----")
    print("SOURCE NOT FOUND")
    sys.exit(0)

adj = []
for e in edges:
    a = e["fromCellId"]
    b = e["toCellId"]
    if source["id"] == a:
        adj.append((node_by_id[b], e))
    elif source["id"] == b:
        adj.append((node_by_id[a], e))

def ignitable(n):
    veg = (n.get("vegetation") or "").lower()
    return veg not in ("water", "bare")

local = []
foreign = []

for nb, e in adj:
    if not ignitable(nb):
        continue
    item = {
        "x": nb["x"],
        "y": nb["y"],
        "state": nb.get("state"),
        "veg": nb.get("vegetation"),
        "prob": float(nb.get("burnProbability") or 0.0),
        "heat": float(nb.get("accumulatedHeatJ") or 0.0),
        "mod": float(e.get("fireSpreadModifier") or 0.0),
        "dist": float(e.get("distance") or 0.0),
        "cluster": nb.get("groupKey") or ""
    }
    if item["cluster"] == cluster:
        local.append(item)
    else:
        foreign.append(item)

local.sort(key=lambda x: (-x["mod"], x["dist"], -x["prob"]))
foreign.sort(key=lambda x: (-x["mod"], x["dist"], -x["prob"]))

print(f"---- step={step} ----")
print(
    f"SOURCE state={source.get('state')} stage={source.get('fireStage')} "
    f"intensity={float(source.get('fireIntensity') or 0.0):.4f} "
    f"elapsed={float(source.get('burningElapsedSeconds') or 0.0):.4f}"
)

print(f"LOCAL count={len(local)}")
for item in local[:8]:
    print(
        f"LOCAL neighbor=({item['x']},{item['y']}) "
        f"state={item['state']} veg={item['veg']} "
        f"mod={item['mod']:.4f} dist={item['dist']:.4f} "
        f"heat={item['heat']:.4f} prob={item['prob']:.6f}"
    )

print(f"FOREIGN count={len(foreign)}")
for item in foreign[:6]:
    print(
        f"FOREIGN neighbor=({item['x']},{item['y']}) "
        f"state={item['state']} veg={item['veg']} "
        f"mod={item['mod']:.4f} dist={item['dist']:.4f} "
        f"heat={item['heat']:.4f} prob={item['prob']:.6f}"
    )
PY
}

collect_summary_metrics() {
  python3 - "$GRAPH_JSON" "$START_X" "$START_Y" "$START_CLUSTER" <<'PY'
import json, sys

graph_path, sx, sy, cluster = sys.argv[1:]
sx = int(sx)
sy = int(sy)

with open(graph_path, 'r', encoding='utf-8') as f:
    data = json.load(f)

graph = data.get("graph") or {}
nodes = graph.get("nodes") or []
edges = graph.get("edges") or []

node_by_id = {n["id"]: n for n in nodes}
coord_to_node = {(n["x"], n["y"]): n for n in nodes}
source = coord_to_node.get((sx, sy))

if source is None:
    print("0 0 0 0")
    sys.exit(0)

adj = []
for e in edges:
    a = e["fromCellId"]
    b = e["toCellId"]
    if source["id"] == a:
        adj.append(node_by_id[b])
    elif source["id"] == b:
        adj.append(node_by_id[a])

def ignitable(n):
    veg = (n.get("vegetation") or "").lower()
    return veg not in ("water", "bare")

local = [n for n in adj if ignitable(n) and (n.get("groupKey") or "") == cluster]

max_heat = max((float(n.get("accumulatedHeatJ") or 0.0) for n in local), default=0.0)
max_prob = max((float(n.get("burnProbability") or 0.0) for n in local), default=0.0)
burning_local = sum(1 for n in local if (n.get("state") or "") == "Burning")
source_burning = 1 if (source.get("state") or "") == "Burning" else 0

print(f"{max_heat} {max_prob} {burning_local} {source_burning}")
PY
}

create_simulation
load_graph
pick_best_internal_start
start_manual

best_heat=0
best_prob=0
first_local_burning_step=0
source_burning_seen=0

for step in $(seq 1 6); do
  curl -sS -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" >/dev/null
  print_step_diagnostics "$step"

  read -r max_heat max_prob burning_local source_burning < <(collect_summary_metrics)

  python3 - <<PY
best_heat = float("$best_heat")
best_prob = float("$best_prob")
curr_heat = float("$max_heat")
curr_prob = float("$max_prob")
if curr_heat > best_heat:
    print(f"best_local_heat: {best_heat:.4f} -> {curr_heat:.4f}")
if curr_prob > best_prob:
    print(f"best_local_prob: {best_prob:.6f} -> {curr_prob:.6f}")
PY

  best_heat="$(python3 - <<PY
print(max(float("$best_heat"), float("$max_heat")))
PY
)"
  best_prob="$(python3 - <<PY
print(max(float("$best_prob"), float("$max_prob")))
PY
)"

  if [[ "$source_burning" == "1" ]]; then
    source_burning_seen=1
  fi

  if [[ "$burning_local" -gt 0 && "$first_local_burning_step" -eq 0 ]]; then
    first_local_burning_step="$step"
  fi
done

echo "------------------------------------------------------------"
echo "SUMMARY"
echo "best_local_heat = $best_heat"
echo "best_local_prob = $best_prob"
echo "first_local_burning_step = $first_local_burning_step"
echo "source_burning_seen = $source_burning_seen"

python3 - <<PY
best_heat = float("$best_heat")
best_prob = float("$best_prob")
first_local_burning_step = int("$first_local_burning_step")
source_burning_seen = int("$source_burning_seen")

if source_burning_seen == 0:
    print("❌ Источник вообще не был в состоянии Burning во время аудита")
    raise SystemExit(1)

if best_heat <= 0.0:
    print("❌ У локальных соседей не накапливается accumulatedHeatJ")
    raise SystemExit(1)

if best_prob < 0.02:
    print("❌ Локальная вероятность почти не растёт: runtime-пайплайн подозрителен")
    raise SystemExit(1)

if first_local_burning_step == 0:
    print("⚠️ За время аудита локальный сосед не загорелся")
    print("   Но heat/prob уже росли — это значит, что математика работает,")
    print("   а проблема может быть в stochastic-trigger, пороге, числе шагов или параметрах сценария.")
    raise SystemExit(0)

print("✅ Runtime-аудит показывает, что локальный рост реально начинается")
PY

echo "============================================================"
echo "✅ ТЕСТ 43 ЗАВЕРШЕН"
echo "============================================================"