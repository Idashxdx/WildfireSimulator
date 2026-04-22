#!/bin/bash
set -e

echo "============================================================"
echo " ЗАПУСК ВСЕХ ТЕСТОВ WILDFIRE SIMULATOR"
echo "============================================================"
echo ""

echo "[1/2] Запуск unit-тестов..."
dotnet test --filter "FullyQualifiedName~ComprehensivePhysicsTests" --no-build
echo "✅ Unit-тесты пройдены"
echo ""

echo "[2/2] Запуск интеграционных тестов..."

TESTS=(
    "10_step_duration_invariant.sh"
    "14_absolute_growth_limit.sh"
    "20_wind_strength_effect.sh"
    "test_fire_spread_modifier_effect.sh"
    "test_fire_spread_modifier_probability.sh"
    "test_metrics_history_api.sh"
    "test_precipitation_reduces_spread.sh"
    "test_precipitation_status.sh"
    "test_slope_physics.sh"
    "test_water_bare_do_not_ignite.sh"
    "test_wind_direction_bias.sh"
    "test_graph_scale_distinction.sh"
    "test_graph_scale_reproducibility.sh"
    "test_large_graph_corridor_logic.sh"
    "test_medium_graph_cluster_cohesion.sh"
    "test_small_graph_topology_profile.sh"
    "test_large_graph_macro_corridor_scenario.sh"
    "test_medium_graph_barrier_scenario.sh"
    "test_small_graph_bridge_critical_scenario.sh"
    "test_graph_scenario_distinction.sh"
    "test_clustered_blueprint_validation.sh"
    "test_clustered_blueprint_source_of_truth.sh"
    "test_corridor_runtime_spread.sh"
    "test_edge_memory_effect.sh"
    "test_baseline_not_dying_too_fast.sh"
    "test_humidity_runtime_effect.sh"
    "test_temperature_runtime_effect.sh"

)

cd tests

FAILED=0
for test in "${TESTS[@]}"; do
    if [ -f "$test" ]; then
        echo ""
        echo "▶️ Запуск: $test"
        if bash "$test"; then
            echo "✅ $test пройден"
        else
            echo "❌ $test провален"
            FAILED=$((FAILED + 1))
        fi
    else
        echo "⚠️ Тест не найден: $test"
    fi
done

cd ..

echo ""
echo "============================================================"
if [ $FAILED -eq 0 ]; then
    echo "✅ ВСЕ ТЕСТЫ ПРОЙДЕНЫ"
else
    echo "❌ ПРОВАЛЕНО ТЕСТОВ: $FAILED"
fi
echo "============================================================"

exit $FAILED
