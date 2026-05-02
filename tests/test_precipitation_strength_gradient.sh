#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: сила осадков должна давать градацию эффекта"
echo "============================================================"
echo "BASE_URL=$BASE_URL"
echo "TMP_DIR=$TMP_DIR"

run_case() {
  local label="$1"
  local precipitation="$2"

  local sim_json="$TMP_DIR/${label}_simulation.json"
  local start_json="$TMP_DIR/${label}_start.json"
  local graph_json="$TMP_DIR/${label}_graph.json"

  curl -sS -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"precipitation-strength-${label}\",
      \"description\": \"Проверка градации силы осадков\",
      \"gridWidth\": 30,
      \"gridHeight\": 30,
      \"graphType\": 0,
      \"initialMoistureMin\": 0.20,
      \"initialMoistureMax\": 0.20,
      \"elevationVariation\": 0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 25,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 424242,
      \"temperature\": 30,
      \"humidity\": 30,
      \"windSpeed\": 8,
      \"windDirection\": 270,
      \"precipitation\": ${precipitation}
    }" > "$sim_json"

  local sim_id
  sim_id="$(jq -r '.id // empty' "$sim_json")"

  if [[ -z "$sim_id" || "$sim_id" == "null" ]]; then
    echo "❌ Не удалось создать симуляцию $label"
    cat "$sim_json"
    exit 1
  fi

  echo "${label}_simulation_id = $sim_id" >&2

  curl -sS -X POST "$BASE_URL/api/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d '{
      "ignitionMode": "manual",
      "initialFirePositions": [
        { "x": 5, "y": 15 }
      ]
    }' > "$start_json"

  local start_success
  start_success="$(jq -r '.success // false' "$start_json")"

  if [[ "$start_success" != "true" ]]; then
    echo "❌ Не удалось запустить симуляцию $label"
    cat "$start_json"
    exit 1
  fi

  for step in $(seq 1 16); do
    curl -sS -X POST "$BASE_URL/api/SimulationManager/$sim_id/step" > "$TMP_DIR/${label}_step_${step}.json"
  done

  curl -sS "$BASE_URL/api/SimulationManager/$sim_id/graph" > "$graph_json"

  echo "$graph_json"
}

DRY_GRAPH="$(run_case dry 0)"
LIGHT_GRAPH="$(run_case light 30)"
MEDIUM_GRAPH="$(run_case medium 70)"
HEAVY_GRAPH="$(run_case heavy 100)"

python3 - "$DRY_GRAPH" "$LIGHT_GRAPH" "$MEDIUM_GRAPH" "$HEAVY_GRAPH" <<'PY'
import json
import sys

dry_path, light_path, medium_path, heavy_path = sys.argv[1:5]

def load_nodes(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)["graph"]["nodes"]

def metrics(path):
    nodes = load_nodes(path)

    # Зона, через которую за 16 шагов должен пройти фронт.
    zone = [
        n for n in nodes
        if 8 <= n["x"] <= 17 and 10 <= n["y"] <= 20
    ]

    if not zone:
        raise RuntimeError("empty zone")

    avg_moisture = sum(float(n.get("moisture") or 0.0) for n in zone) / len(zone)
    avg_heat = sum(float(n.get("accumulatedHeatJ") or 0.0) for n in zone) / len(zone)

    burning = sum(1 for n in nodes if n.get("state") == "Burning")
    burned = sum(1 for n in nodes if n.get("state") == "Burned")
    affected = burning + burned

    return {
        "moisture": avg_moisture,
        "heat": avg_heat,
        "affected": affected,
        "burning": burning,
        "burned": burned,
    }

dry = metrics(dry_path)
light = metrics(light_path)
medium = metrics(medium_path)
heavy = metrics(heavy_path)

print(f"dry_moisture    = {dry['moisture']:.6f}")
print(f"light_moisture  = {light['moisture']:.6f}")
print(f"medium_moisture = {medium['moisture']:.6f}")
print(f"heavy_moisture  = {heavy['moisture']:.6f}")

print(f"dry_affected    = {dry['affected']}")
print(f"light_affected  = {light['affected']}")
print(f"medium_affected = {medium['affected']}")
print(f"heavy_affected  = {heavy['affected']}")

if not (dry["moisture"] < light["moisture"] < medium["moisture"] <= heavy["moisture"]):
    print("❌ Влажность не растёт с увеличением силы осадков")
    sys.exit(1)

if heavy["affected"] > dry["affected"]:
    print("❌ Сильные осадки увеличили площадь пожара относительно сухого сценария")
    sys.exit(1)

if medium["affected"] > light["affected"] + 3:
    print("❌ Средние осадки неожиданно дали хуже результат, чем слабые")
    sys.exit(1)

if heavy["affected"] > medium["affected"] + 3:
    print("❌ Сильные осадки неожиданно дали хуже результат, чем средние")
    sys.exit(1)

print("✅ Чем сильнее осадки, тем выше влажность и тем слабее развитие пожара")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $TMP_DIR"
echo "============================================================"