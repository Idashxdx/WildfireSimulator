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
    "01_infrastructure.sh"
    "04_kafka_streams.sh"
    "10_step_duration_invariant.sh"
    "14_absolute_growth_limit.sh"
    "17_graph_types_factor_comparison.sh"
    "19_wind_direction_bias.sh"
    "20_wind_strength_effect.sh"
    "22_ClusteredGraph_degree_profile.sh"
    "26_structure_comparison_same_conditions.sh"
    "33_ClusteredGraph_nonjump_connectivity.sh"
    "34_structure_modes_regression.sh"
    "35_ClusteredGraph_regional_cohesion.sh"
    "test_fire_spread_modifier_effect.sh"
    "test_fire_spread_modifier_probability.sh"
    "test_metrics_history_api.sh"
    "test_precipitation_reduces_spread.sh"
    "test_precipitation_status.sh"
    "test_random_seed_reproducibility.sh"
    "test_slope_physics.sh"
    "test_water_bare_do_not_ignite.sh"
    "test_wind_direction_bias.sh"
    "38_region_bridge_later_than_local_growth.sh"
    "39_graph_edge_strength_order.sh"
    "40_region_bridge_weaker_than_local.sh"
    "42_region_internal_growth_rate.sh"

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
