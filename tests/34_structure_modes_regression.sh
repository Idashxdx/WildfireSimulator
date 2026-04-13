#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5198}"
OUT_DIR="/tmp/wildfire_structure_modes_regression_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "============================================================"
echo " ТЕСТ: регрессия новых режимов структур"
echo "============================================================"
echo ""

run_case() {
    local graph_type="$1"
    local graph_name="$2"
    local wind_speed="$3"

    local case_dir="$OUT_DIR/$graph_name"
    mkdir -p "$case_dir"

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " GRAPH TYPE: $graph_name"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    local create_json="$case_dir/create.json"

    curl -s -X POST "$API_URL/api/Simulations" \
      -H "Content-Type: application/json" \
      -d "{
        \"name\": \"Regression ${graph_name}\",
        \"description\": \"Structure mode regression test\",
        \"gridWidth\": 20,
        \"gridHeight\": 20,
        \"graphType\": $graph_type,
        \"initialMoistureMin\": 0.20,
        \"initialMoistureMax\": 0.20,
        \"elevationVariation\": 20.0,
        \"initialFireCellsCount\": 1,
        \"simulationSteps\": 20,
        \"stepDurationSeconds\": 1800,
        \"randomSeed\": 424242,
        \"temperature\": 32,
        \"humidity\": 35,
        \"windSpeed\": $wind_speed,
        \"windDirection\": 45,
        \"precipitation\": 0
      }" > "$create_json"

    local sim_id
    sim_id=$(jq -r '.id' "$create_json")
    [[ -z "$sim_id" || "$sim_id" == "null" ]] && echo "❌ Ошибка создания" && cat "$create_json" && exit 1

    curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/start" > "$case_dir/start.json"

    local first_area=0
    local last_area=0
    local max_area=0
    local first_spread_step=0
    local had_growth=0
    local had_burnout=0
    local finished_normally=0

    local first_multi_cluster_step=0
    local max_affected_clusters=0
    local step3_clusters=0
    local step4_clusters=0

    for step in $(seq 1 12); do
        local step_json="$case_dir/step_${step}.json"
        curl -s -X POST "$API_URL/api/SimulationManager/$sim_id/step" > "$step_json"

        local success
        success=$(jq -r '.success // false' "$step_json")
        if [[ "$success" != "true" ]]; then
            echo "❌ Ошибка шага $step"
            cat "$step_json"
            exit 1
        fi

        local area burning burned
        area=$(jq -r '.step.fireArea // 0' "$step_json")
        burning=$(jq -r '.step.burningCellsCount // 0' "$step_json")
        burned=$(jq -r '.step.burnedCellsCount // 0' "$step_json")

        [[ "$step" -eq 1 ]] && first_area=$area
        last_area=$area

        if (( $(echo "$area > $max_area" | bc -l) )); then
            max_area=$area
        fi

        if (( $(echo "$area > $first_area" | bc -l) )); then
            had_growth=1
            if [[ "$first_spread_step" -eq 0 ]]; then
                first_spread_step=$step
            fi
        fi

        if [[ "$burned" -gt 0 ]]; then
            had_burnout=1
        fi

        if [[ "$graph_name" == "RegionClusterGraph" ]]; then
            local graph_json="$case_dir/graph_${step}.json"
            curl -s "$API_URL/api/SimulationManager/$sim_id/graph" > "$graph_json"

            local graph_success
            graph_success=$(jq -r '.success // false' "$graph_json")
            if [[ "$graph_success" != "true" ]]; then
                echo "❌ Не удалось получить graph на шаге $step"
                cat "$graph_json"
                exit 1
            fi

            local affected_clusters
            affected_clusters=$(jq -r '
              .graph.nodes
              | map(select(.state == "Burning" or .state == "Burned") | .groupKey)
              | unique
              | length
            ' "$graph_json")

            [[ "$step" -eq 3 ]] && step3_clusters=$affected_clusters
            [[ "$step" -eq 4 ]] && step4_clusters=$affected_clusters

            if [[ "$affected_clusters" -gt "$max_affected_clusters" ]]; then
                max_affected_clusters=$affected_clusters
            fi

            if [[ "$affected_clusters" -gt 1 && "$first_multi_cluster_step" -eq 0 ]]; then
                first_multi_cluster_step=$step
            fi

            echo "   step=$step | area=$area | burning=$burning | burned=$burned | clusters=$affected_clusters"
        else
            echo "   step=$step | area=$area | burning=$burning | burned=$burned"
        fi

        if [[ "$burning" -eq 0 ]]; then
            finished_normally=1
            echo "    Активных очагов больше нет"
            break
        fi
    done

    echo ""
    echo "   Итог:"
    echo "   firstArea=$first_area"
    echo "   lastArea=$last_area"
    echo "   maxArea=$max_area"
    echo "   firstSpreadStep=$first_spread_step"
    echo "   hadGrowth=$had_growth"
    echo "   hadBurnout=$had_burnout"
    echo "   finishedNormally=$finished_normally"

    if [[ "$graph_name" == "RegionClusterGraph" ]]; then
        echo "   firstMultiClusterStep=$first_multi_cluster_step"
        echo "   maxAffectedClusters=$max_affected_clusters"
        echo "   step3Clusters=$step3_clusters"
        echo "   step4Clusters=$step4_clusters"
    fi

    if [[ "$had_growth" -ne 1 ]]; then
        echo "❌ $graph_name: нет распространения"
        exit 1
    fi

    if [[ "$had_burnout" -ne 1 ]]; then
        echo "❌ $graph_name: не видно выгорания"
        exit 1
    fi

    if [[ "$graph_name" == "Grid" ]]; then
        if [[ "$first_spread_step" -gt 3 || "$first_spread_step" -eq 0 ]]; then
            echo "❌ Grid: слишком поздний рост"
            exit 1
        fi

        if (( $(echo "$max_area < 8" | bc -l) )); then
            echo "❌ Grid: слишком слабое распространение"
            exit 1
        fi

        if (( $(echo "$max_area > 80" | bc -l) )); then
            echo "❌ Grid: слишком взрывной рост"
            exit 1
        fi
    fi

    if [[ "$graph_name" == "ClusteredGraph" ]]; then
        if [[ "$first_spread_step" -gt 4 || "$first_spread_step" -eq 0 ]]; then
            echo "❌ ClusteredGraph: слишком поздний рост"
            exit 1
        fi

        if (( $(echo "$max_area < 4" | bc -l) )); then
            echo "❌ ClusteredGraph: слишком слабое сетевое распространение"
            exit 1
        fi

        if (( $(echo "$max_area > 40" | bc -l) )); then
            echo "❌ ClusteredGraph: слишком взрывной рост"
            exit 1
        fi
    fi

    if [[ "$graph_name" == "RegionClusterGraph" ]]; then
        if [[ "$step3_clusters" -ne 1 ]]; then
            echo "❌ RegionClusterGraph: к 3 шагу пожар уже не локален"
            exit 1
        fi

        if [[ "$step4_clusters" -ne 1 ]]; then
            echo "❌ RegionClusterGraph: к 4 шагу пожар уже слишком рано вышел из кластера"
            exit 1
        fi

        if [[ "$first_multi_cluster_step" -ne 0 && "$first_multi_cluster_step" -lt 5 ]]; then
            echo "❌ RegionClusterGraph: слишком ранний межкластерный переход"
            exit 1
        fi

        if [[ "$max_affected_clusters" -gt 3 ]]; then
            echo "❌ RegionClusterGraph: слишком широкое межкластерное распространение"
            exit 1
        fi

        if (( $(echo "$max_area < 10" | bc -l) )); then
            echo "❌ RegionClusterGraph: слишком слабый рост внутри кластеров"
            exit 1
        fi

        if [[ "$first_multi_cluster_step" -eq 0 ]]; then
            echo "   ⚠️ RegionClusterGraph: за 12 шагов межкластерный переход не произошёл"
            echo "      Это допустимо для регрессионного теста структуры, если локальный рост устойчив."
        fi
    fi

    echo "   ✅ $graph_name проходит регрессионную проверку"
    echo ""
}

run_case 0 "Grid" 8
run_case 1 "ClusteredGraph" 3
run_case 2 "RegionClusterGraph" 8

echo "============================================================"
echo "✅ Регрессия новых режимов структур пройдена"
echo "📁 Логи: $OUT_DIR"
echo "============================================================"