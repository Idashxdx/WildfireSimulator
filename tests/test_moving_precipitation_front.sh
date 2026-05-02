#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"

echo "============================================================"
echo " ТЕСТ: движущийся фронт осадков должен ослаблять пожар"
echo "============================================================"
echo "BASE_URL=$BASE_URL"
echo "TMP_DIR=$TMP_DIR"

create_simulation() {
  local name="$1"
  local precipitation="$2"

  curl -s -X POST "$BASE_URL/api/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"moving precipitation front test\",
      \"gridWidth\": 35,
      \"gridHeight\": 35,
      \"graphType\": 0,
      \"initialMoistureMin\": 0.12,
      \"initialMoistureMax\": 0.28,
      \"elevationVariation\": 40,
      \"initialFireCellsCount\": 3,
      \"simulationSteps\": 30,
      \"stepDurationSeconds\": 900,
      \"randomSeed\": 424242,
      \"temperature\": 31,
      \"humidity\": 25,
      \"windSpeed\": 7,
      \"windDirection\": 45,
      \"precipitation\": $precipitation,
      \"mapDrynessFactor\": 1.2,
      \"fuelDensityFactor\": 1.15,
      \"reliefStrengthFactor\": 1.0,
      \"mapNoiseStrength\": 0.08,
      \"vegetationDistributions\": [
        { \"vegetationType\": 3, \"probability\": 0.65 },
        { \"vegetationType\": 4, \"probability\": 0.15 },
        { \"vegetationType\": 0, \"probability\": 0.10 },
        { \"vegetationType\": 2, \"probability\": 0.05 },
        { \"vegetationType\": 1, \"probability\": 0.03 },
        { \"vegetationType\": 5, \"probability\": 0.01 },
        { \"vegetationType\": 6, \"probability\": 0.01 }
      ]
    }" | python3 -c 'import sys,json; print(json.load(sys.stdin)["id"])'
}

start_simulation() {
  local id="$1"

  curl -s -X POST "$BASE_URL/api/SimulationManager/$id/start" \
    -H "Content-Type: application/json" \
    -d '{ "ignitionMode": "saved-or-random" }' >/dev/null
}

step_simulation() {
  local id="$1"

  curl -s -X POST "$BASE_URL/api/SimulationManager/$id/step" >/dev/null
}

status_json() {
  local id="$1"

  curl -s "$BASE_URL/api/SimulationManager/$id/status"
}

get_area() {
  python3 -c '
import sys,json
d=json.load(sys.stdin)
print(d["simulation"]["fireArea"])
'
}

get_burning() {
  python3 -c '
import sys,json
d=json.load(sys.stdin)
print(d["simulation"]["totalBurningCells"])
'
}

get_burned() {
  python3 -c '
import sys,json
d=json.load(sys.stdin)
print(d["simulation"]["totalBurnedCells"])
'
}

run_case() {
  local label="$1"
  local precipitation="$2"
  local steps="$3"

  echo
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo "CASE: $label precipitation=$precipitation"
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

  local id
  id="$(create_simulation "$label" "$precipitation")"
  echo "simulation_id=$id"

  start_simulation "$id"

  for i in $(seq 1 "$steps"); do
    step_simulation "$id"

    local status
    status="$(status_json "$id")"

    local area burning burned
    area="$(echo "$status" | get_area)"
    burning="$(echo "$status" | get_burning)"
    burned="$(echo "$status" | get_burned)"

    echo "step=$i area=$area burning=$burning burned=$burned"
  done

  local final_status
  final_status="$(status_json "$id")"

  local final_area final_burning final_burned
  final_area="$(echo "$final_status" | get_area)"
  final_burning="$(echo "$final_status" | get_burning)"
  final_burned="$(echo "$final_status" | get_burned)"

  echo "$final_status" > "$TMP_DIR/${label}.json"

  echo "$final_area;$final_burning;$final_burned"
}

DRY_RESULT="$(run_case dry 0 12 | tee "$TMP_DIR/dry.log" | tail -n 1)"
RAIN_RESULT="$(run_case rain_front 12 12 | tee "$TMP_DIR/rain.log" | tail -n 1)"
HEAVY_RAIN_RESULT="$(run_case heavy_rain_front 20 12 | tee "$TMP_DIR/heavy_rain.log" | tail -n 1)"

DRY_AREA="$(echo "$DRY_RESULT" | cut -d ';' -f 1)"
RAIN_AREA="$(echo "$RAIN_RESULT" | cut -d ';' -f 1)"
HEAVY_RAIN_AREA="$(echo "$HEAVY_RAIN_RESULT" | cut -d ';' -f 1)"

echo
echo "============================================================"
echo "RESULT"
echo "============================================================"
echo "dry_area        = $DRY_AREA"
echo "rain_area       = $RAIN_AREA"
echo "heavy_rain_area = $HEAVY_RAIN_AREA"

python3 - "$DRY_AREA" "$RAIN_AREA" "$HEAVY_RAIN_AREA" <<'PY'
import sys

dry = float(sys.argv[1])
rain = float(sys.argv[2])
heavy = float(sys.argv[3])

ok = True

if rain > dry:
    print(f"❌ rain_area больше dry_area: {rain} > {dry}")
    ok = False
else:
    print(f"✅ rain_area не больше dry_area: {rain} <= {dry}")

if heavy > rain:
    print(f"❌ heavy_rain_area больше rain_area: {heavy} > {rain}")
    ok = False
else:
    print(f"✅ heavy_rain_area не больше rain_area: {heavy} <= {rain}")

if dry - heavy < 1:
    print(f"❌ эффект сильного фронта осадков слишком слабый: dry-heavy = {dry-heavy}")
    ok = False
else:
    print(f"✅ сильный фронт заметно ослабил пожар: dry-heavy = {dry-heavy}")

if not ok:
    sys.exit(1)
PY

echo
echo "✅ ТЕСТ ПРОЙДЕН"
echo "📁 Логи: $TMP_DIR"
