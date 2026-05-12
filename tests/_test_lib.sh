#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
API_URL="${API_URL:-$BASE_URL/api}"

require_tools() {
  for tool in curl jq python3 bc dotnet; do
    if ! command -v "$tool" >/dev/null 2>&1; then
      echo "❌ Не найден инструмент: $tool"
      exit 1
    fi
  done
}

create_grid_sim() {
  local out_file="$1"
  local name="$2"
  local width="$3"
  local height="$4"
  local moisture_min="$5"
  local moisture_max="$6"
  local elevation="$7"
  local steps="$8"
  local dt="$9"
  local seed="${10}"
  local temperature="${11}"
  local humidity="${12}"
  local wind_speed="${13}"
  local wind_direction="${14}"
  local precipitation="${15}"

  curl -sS -X POST "$API_URL/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"runtime integration test\",
      \"graphType\": 0,
      \"gridWidth\": $width,
      \"gridHeight\": $height,
      \"initialMoistureMin\": $moisture_min,
      \"initialMoistureMax\": $moisture_max,
      \"elevationVariation\": $elevation,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": $steps,
      \"stepDurationSeconds\": $dt,
      \"randomSeed\": $seed,
      \"temperature\": $temperature,
      \"humidity\": $humidity,
      \"windSpeed\": $wind_speed,
      \"windDirection\": $wind_direction,
      \"precipitation\": $precipitation
    }" > "$out_file"
}

create_graph_sim() {
  local out_file="$1"
  local name="$2"
  local scale="$3"
  local width="$4"
  local height="$5"
  local moisture_min="$6"
  local moisture_max="$7"
  local elevation="$8"
  local steps="$9"
  local dt="${10}"
  local seed="${11}"
  local temperature="${12}"
  local humidity="${13}"
  local wind_speed="${14}"
  local wind_direction="${15}"
  local precipitation="${16}"

  curl -sS -X POST "$API_URL/simulations" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$name\",
      \"description\": \"graph runtime integration test\",
      \"graphType\": 1,
      \"graphScaleType\": $scale,
      \"gridWidth\": $width,
      \"gridHeight\": $height,
      \"initialMoistureMin\": $moisture_min,
      \"initialMoistureMax\": $moisture_max,
      \"elevationVariation\": $elevation,
      \"initialFireCellsCount\": 1,
      \"simulationSteps\": $steps,
      \"stepDurationSeconds\": $dt,
      \"randomSeed\": $seed,
      \"temperature\": $temperature,
      \"humidity\": $humidity,
      \"windSpeed\": $wind_speed,
      \"windDirection\": $wind_direction,
      \"precipitation\": $precipitation
    }" > "$out_file"
}

get_sim_id() {
  local file="$1"
  local id
  id="$(jq -r '.id // empty' "$file")"

  if [[ -z "$id" || "$id" == "null" ]]; then
    echo "❌ Не удалось получить simulation id"
    cat "$file"
    exit 1
  fi

  echo "$id"
}

start_manual() {
  local sim_id="$1"
  local out_file="$2"
  local x="$3"
  local y="$4"

  curl -sS -X POST "$API_URL/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d "{
      \"ignitionMode\": \"manual\",
      \"initialFirePositions\": [
        { \"x\": $x, \"y\": $y }
      ]
    }" > "$out_file"

  local ok
  ok="$(jq -r '.success // false' "$out_file")"
  if [[ "$ok" != "true" ]]; then
    echo "❌ Не удалось запустить simulation=$sim_id"
    cat "$out_file"
    exit 1
  fi
}

start_saved_or_random() {
  local sim_id="$1"
  local out_file="$2"

  curl -sS -X POST "$API_URL/SimulationManager/$sim_id/start" \
    -H "Content-Type: application/json" \
    -d '{"ignitionMode":"saved-or-random"}' > "$out_file"

  local ok
  ok="$(jq -r '.success // false' "$out_file")"
  if [[ "$ok" != "true" ]]; then
    echo "❌ Не удалось запустить simulation=$sim_id"
    cat "$out_file"
    exit 1
  fi
}

run_steps() {
  local sim_id="$1"
  local count="$2"
  local out_dir="$3"
  local prefix="$4"

  local last_file=""

  for step in $(seq 1 "$count"); do
    last_file="$out_dir/${prefix}_step_${step}.json"
    curl -sS -X POST "$API_URL/SimulationManager/$sim_id/step" > "$last_file"

    local ok
    ok="$(jq -r '.success // false' "$last_file")"
    if [[ "$ok" != "true" ]]; then
      echo "❌ Ошибка step=$step simulation=$sim_id"
      cat "$last_file"
      exit 1
    fi

    printf "."
  done

  echo "" >&2
  echo "$last_file"
}

fetch_status() {
  local sim_id="$1"
  local out_file="$2"
  curl -sS "$API_URL/SimulationManager/$sim_id/status" > "$out_file"
}

fetch_graph() {
  local sim_id="$1"
  local out_file="$2"
  curl -sS "$API_URL/SimulationManager/$sim_id/graph" > "$out_file"

  local ok
  ok="$(jq -r '.success // false' "$out_file" 2>/dev/null || echo false)"
  if [[ "$ok" != "true" ]]; then
    echo "❌ Не удалось получить graph simulation=$sim_id"
    cat "$out_file"
    exit 1
  fi
}

status_area() {
  jq -r '.simulation.fireArea // 0' "$1"
}

status_burning() {
  jq -r '.simulation.totalBurningCells // 0' "$1"
}

status_burned() {
  jq -r '.simulation.totalBurnedCells // 0' "$1"
}

assert_ge() {
  local actual="$1"
  local expected="$2"
  local message="$3"

  if (( $(echo "$actual >= $expected" | bc -l) )); then
    echo "✅ $message: $actual >= $expected"
  else
    echo "❌ $message: $actual < $expected"
    exit 1
  fi
}

assert_le() {
  local actual="$1"
  local expected="$2"
  local message="$3"

  if (( $(echo "$actual <= $expected" | bc -l) )); then
    echo "✅ $message: $actual <= $expected"
  else
    echo "❌ $message: $actual > $expected"
    exit 1
  fi
}

assert_gt() {
  local actual="$1"
  local expected="$2"
  local message="$3"

  if (( $(echo "$actual > $expected" | bc -l) )); then
    echo "✅ $message: $actual > $expected"
  else
    echo "❌ $message: $actual <= $expected"
    exit 1
  fi
}

assert_between() {
  local actual="$1"
  local min="$2"
  local max="$3"
  local message="$4"

  if (( $(echo "$actual >= $min && $actual <= $max" | bc -l) )); then
    echo "✅ $message: $actual in [$min; $max]"
  else
    echo "❌ $message: $actual not in [$min; $max]"
    exit 1
  fi
}