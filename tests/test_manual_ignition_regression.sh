#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_manual_ignition_regression_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: ручной и сохранённый выбор очагов"
echo "============================================================"
echo "API_URL=$API_URL"
echo "OUT_DIR=$OUT_DIR"
echo ""

require_cmd() {
    local cmd="$1"
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "❌ Не найдена команда: $cmd"
        exit 1
    fi
}

require_cmd curl
require_cmd jq

create_grid_simulation() {
    local name="$1"
    local out_file="$2"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"$name\",
        \"description\": \"Manual ignition regression test\",
        \"gridWidth\": 20,
        \"gridHeight\": 20,
        \"graphType\": 0,
        \"initialMoistureMin\": 0.20,
        \"initialMoistureMax\": 0.20,
        \"elevationVariation\": 20.0,
        \"initialFireCellsCount\": 2,
        \"simulationSteps\": 20,
        \"stepDurationSeconds\": 1800,
        \"randomSeed\": 424242,
        \"temperature\": 32,
        \"humidity\": 35,
        \"windSpeed\": 6,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$out_file"

    local sim_id
    sim_id=$(jq -r '.id // empty' "$out_file")

    if [[ -z "$sim_id" || "$sim_id" == "null" ]]; then
        echo "❌ Ошибка создания симуляции"
        cat "$out_file"
        exit 1
    fi

    echo "$sim_id"
}

prepare_map() {
    local sim_id="$1"
    local out_file="$2"

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/prepare-map" > "$out_file"

    local success
    success=$(jq -r '.success // false' "$out_file")
    if [[ "$success" != "true" ]]; then
        echo "❌ prepare-map завершился ошибкой"
        cat "$out_file"
        exit 1
    fi
}

assert_prepare_map_is_clean() {
    local json_file="$1"

    local burning_count
    burning_count=$(jq '[.cells[] | select(.state == "Burning")] | length' "$json_file")

    local selected_count
    selected_count=$(jq '[.cells[] | select(.isSelectedIgnition == true)] | length' "$json_file")

    echo "   burning_count=$burning_count"
    echo "   selected_count=$selected_count"

    if [[ "$burning_count" -ne 0 ]]; then
        echo "❌ После prepare-map на карте уже есть горящие клетки"
        exit 1
    fi

    if [[ "$selected_count" -ne 0 ]]; then
        echo "❌ После prepare-map клетки уже помечены как выбранные очаги"
        exit 1
    fi
}

pick_two_ignitable_cells() {
    local json_file="$1"

    jq -r '
        .cells
        | map(select(.isIgnitable == true and .state == "Normal"))
        | .[:2]
        | .[]
        | "\(.x),\(.y)"
    ' "$json_file"
}

assert_two_positions_found() {
    local positions_text="$1"

    local count
    count=$(echo "$positions_text" | sed '/^\s*$/d' | wc -l | tr -d ' ')

    if [[ "$count" -lt 2 ]]; then
        echo "❌ Не удалось найти две ignitable клетки для ручного старта"
        echo "$positions_text"
        exit 1
    fi
}

manual_start() {
    local sim_id="$1"
    local x1="$2"
    local y1="$3"
    local x2="$4"
    local y2="$5"
    local out_file="$6"

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/start" \
      -H "Content-Type: application/json" \
      -d "{
        \"ignitionMode\": \"manual\",
        \"initialFirePositions\": [
          { \"x\": $x1, \"y\": $y1 },
          { \"x\": $x2, \"y\": $y2 }
        ]
      }" > "$out_file"

    local success
    success=$(jq -r '.success // false' "$out_file")
    if [[ "$success" != "true" ]]; then
        echo "❌ Ошибка ручного старта"
        cat "$out_file"
        exit 1
    fi
}

random_or_saved_start() {
    local sim_id="$1"
    local out_file="$2"

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/start" \
      -H "Content-Type: application/json" \
      -d '{
        "ignitionMode": "saved-or-random",
        "initialFirePositions": []
      }' > "$out_file"

    local success
    success=$(jq -r '.success // false' "$out_file")
    if [[ "$success" != "true" ]]; then
        echo "❌ Ошибка старта saved-or-random"
        cat "$out_file"
        exit 1
    fi
}

extract_burning_positions() {
    local json_file="$1"

    jq -r '
        .cells
        | map(select(.state == "Burning"))
        | sort_by(.x, .y)
        | .[]
        | "\(.x),\(.y)"
    ' "$json_file"
}

assert_exact_manual_positions_burning() {
    local json_file="$1"
    local x1="$2"
    local y1="$3"
    local x2="$4"
    local y2="$5"

    local actual
    actual=$(extract_burning_positions "$json_file" | sed '/^\s*$/d' | sort)

    local expected
    expected=$(printf "%s\n%s\n" "$x1,$y1" "$x2,$y2" | sort)

    local actual_count
    actual_count=$(echo "$actual" | sed '/^\s*$/d' | wc -l | tr -d ' ')

    echo "   expected manual burning:"
    echo "$expected"
    echo "   actual burning:"
    echo "$actual"

    if [[ "$actual_count" -ne 2 ]]; then
        echo "❌ После ручного старта горит не 2 клетки"
        exit 1
    fi

    if [[ "$actual" != "$expected" ]]; then
        echo "❌ После ручного старта загорелись не те клетки"
        exit 1
    fi
}

reset_simulation() {
    local sim_id="$1"
    local out_file="$2"

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/reset" > "$out_file"

    local success
    success=$(jq -r '.success // false' "$out_file")
    if [[ "$success" != "true" ]]; then
        echo "❌ Ошибка reset"
        cat "$out_file"
        exit 1
    fi
}

assert_saved_positions_restored_after_reset() {
    local json_file="$1"
    local x1="$2"
    local y1="$3"
    local x2="$4"
    local y2="$5"

    local actual
    actual=$(extract_burning_positions "$json_file" | sed '/^\s*$/d' | sort)

    local expected
    expected=$(printf "%s\n%s\n" "$x1,$y1" "$x2,$y2" | sort)

    echo "   expected restored burning:"
    echo "$expected"
    echo "   actual restored burning:"
    echo "$actual"

    if [[ "$actual" != "$expected" ]]; then
        echo "❌ После reset/start не восстановились сохранённые ручные очаги"
        exit 1
    fi
}

assert_random_start_has_fire() {
    local json_file="$1"

    local burning_count
    burning_count=$(jq '[.cells[] | select(.state == "Burning")] | length' "$json_file")

    echo "   random burning_count=$burning_count"

    if [[ "$burning_count" -le 0 ]]; then
        echo "❌ В random/saved-or-random старте нет горящих клеток"
        exit 1
    fi
}

run_manual_persistence_case() {
    local case_dir="$OUT_DIR/manual_persistence"
    mkdir -p "$case_dir"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "📍 CASE 1: ручной старт + сохранение очагов после reset"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    local sim_id
    sim_id=$(create_grid_simulation "Manual Ignition Persistence Test" "$case_dir/create.json")
    echo "   sim_id=$sim_id"

    prepare_map "$sim_id" "$case_dir/prepare.json"
    assert_prepare_map_is_clean "$case_dir/prepare.json"

    local positions
    positions=$(pick_two_ignitable_cells "$case_dir/prepare.json")
    assert_two_positions_found "$positions"

    local pos1 pos2
    pos1=$(echo "$positions" | sed -n '1p')
    pos2=$(echo "$positions" | sed -n '2p')

    local x1 y1 x2 y2
    x1="${pos1%,*}"
    y1="${pos1#*,}"
    x2="${pos2%,*}"
    y2="${pos2#*,}"

    echo "   selected manual positions: ($x1,$y1), ($x2,$y2)"

    manual_start "$sim_id" "$x1" "$y1" "$x2" "$y2" "$case_dir/manual_start.json"
    assert_exact_manual_positions_burning "$case_dir/manual_start.json" "$x1" "$y1" "$x2" "$y2"

    reset_simulation "$sim_id" "$case_dir/reset.json"

    random_or_saved_start "$sim_id" "$case_dir/start_after_reset.json"
    assert_saved_positions_restored_after_reset "$case_dir/start_after_reset.json" "$x1" "$y1" "$x2" "$y2"

    echo "   ✅ CASE 1 пройден"
    echo ""
}

run_random_start_case() {
    local case_dir="$OUT_DIR/random_mode"
    mkdir -p "$case_dir"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "🎲 CASE 2: saved-or-random старт на новой симуляции"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    local sim_id
    sim_id=$(create_grid_simulation "Random Ignition Start Test" "$case_dir/create.json")
    echo "   sim_id=$sim_id"

    prepare_map "$sim_id" "$case_dir/prepare.json"
    assert_prepare_map_is_clean "$case_dir/prepare.json"

    random_or_saved_start "$sim_id" "$case_dir/random_start.json"
    assert_random_start_has_fire "$case_dir/random_start.json"

    echo "   ✅ CASE 2 пройден"
    echo ""
}

run_nonignitable_cells_case() {
    local case_dir="$OUT_DIR/nonignitable_check"
    mkdir -p "$case_dir"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "🌊 CASE 3: проверка наличия негорючих клеток на карте"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    local sim_id
    sim_id=$(create_grid_simulation "NonIgnitable Cell Check" "$case_dir/create.json")
    echo "   sim_id=$sim_id"

    prepare_map "$sim_id" "$case_dir/prepare.json"

    local nonignitable_count
    nonignitable_count=$(jq '[.cells[] | select(.isIgnitable == false)] | length' "$case_dir/prepare.json")

    echo "   nonignitable_count=$nonignitable_count"
    echo "    Это не fail-условие: вода/bare могут не выпасть на конкретном seed"

    echo "   ✅ CASE 3 завершён"
    echo ""
}

run_manual_persistence_case
run_random_start_case
run_nonignitable_cells_case

echo "============================================================"
echo "✅ Регрессия ручного/сохранённого выбора очагов пройдена"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"