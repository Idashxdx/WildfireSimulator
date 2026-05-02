#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: осадки должны сокращать активное горение"
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
      \"name\": \"precipitation-burnout-${label}\",
      \"description\": \"Проверка влияния осадков на длительность горения\",
      \"gridWidth\": 30,
      \"gridHeight\": 30,
      \"graphType\": 0,
      \"initialMoistureMin\": 0.20,
      \"initialMoistureMax\": 0.20,
      \"elevationVariation\": 0,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": 30,
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

  if [[ "$(jq -r '.success // false' "$start_json")" != "true" ]]; then
    echo "❌ Не удалось запустить симуляцию $label"
    cat "$start_json"
    exit 1
  fi

  for step in $(seq 1 16); do
    curl -sS -X POST "$BASE_URL/api/SimulationManager/$sim_id/step" \
      > "$TMP_DIR/${label}_step_${step}.json"
  done

  curl -sS "$BASE_URL/api/SimulationManager/$sim_id/graph" > "$graph_json"

  echo "$graph_json"
}

DRY_GRAPH="$(run_case dry 0)"
RAIN_GRAPH="$(run_case rain 100)"

python3 - "$DRY_GRAPH" "$RAIN_GRAPH" <<'PY'
import json
import sys

dry_path, rain_path = sys.argv[1:3]

def load_nodes(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)["graph"]["nodes"]

def metrics(path):
    nodes = load_nodes(path)

    burning = [n for n in nodes if n.get("state") == "Burning"]
    burned = [n for n in nodes if n.get("state") == "Burned"]
    affected = burning + burned

    avg_fuel = (
        sum(float(n.get("currentFuelLoad") or 0.0) for n in affected) / len(affected)
        if affected else 0.0
    )

    avg_elapsed = (
        sum(float(n.get("burningElapsedSeconds") or 0.0) for n in affected) / len(affected)
        if affected else 0.0
    )

    avg_intensity = (
        sum(float(n.get("fireIntensity") or 0.0) for n in burning) / len(burning)
        if burning else 0.0
    )

    return {
        "burning": len(burning),
        "burned": len(burned),
        "affected": len(affected),
        "avg_fuel": avg_fuel,
        "avg_elapsed": avg_elapsed,
        "avg_intensity": avg_intensity,
    }

dry = metrics(dry_path)
rain = metrics(rain_path)

print(f"dry_burning        = {dry['burning']}")
print(f"rain_burning       = {rain['burning']}")
print(f"dry_burned         = {dry['burned']}")
print(f"rain_burned        = {rain['burned']}")
print(f"dry_affected       = {dry['affected']}")
print(f"rain_affected      = {rain['affected']}")
print(f"dry_avg_fuel       = {dry['avg_fuel']:.6f}")
print(f"rain_avg_fuel      = {rain['avg_fuel']:.6f}")
print(f"dry_avg_elapsed    = {dry['avg_elapsed']:.3f}")
print(f"rain_avg_elapsed   = {rain['avg_elapsed']:.3f}")
print(f"dry_avg_intensity  = {dry['avg_intensity']:.6f}")
print(f"rain_avg_intensity = {rain['avg_intensity']:.6f}")

if rain["affected"] > dry["affected"]:
    print("❌ Осадки увеличили число затронутых клеток")
    sys.exit(1)

if rain["burning"] > dry["burning"]:
    print("❌ Под сильными осадками активных горящих клеток стало больше")
    sys.exit(1)

if rain["avg_fuel"] >= dry["avg_fuel"] and rain["burned"] <= dry["burned"]:
    print("❌ Не видно ускоренного расхода топлива или выгорания под осадками")
    sys.exit(1)

print("✅ Сильные осадки сокращают активное горение и не усиливают пожар")
PY

echo "============================================================"
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $TMP_DIR"
echo "============================================================"