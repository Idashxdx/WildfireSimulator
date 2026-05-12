#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "============================================================"
echo " ЗАПУСК ВСЕХ ТЕСТОВ WILDFIRE SIMULATOR"
echo "============================================================"
echo ""

echo "[1/2] Unit-тесты формул и физики"
echo ""

dotnet test WildfireSimulator.Tests/WildfireSimulator.Tests.csproj \
  --logger "console;verbosity=detailed" \
  --verbosity normal \
  --no-restore

echo ""
echo "✅ Unit-тесты пройдены"
echo ""

echo "[2/2] Runtime/API integration tests"

TESTS=(
  "10_grid_time_and_growth.sh"
  "20_grid_weather_effects.sh"
  "21_wind_direction_bias.sh"
  "30_burnout_lifecycle_runtime.sh"
  "40_water_bare_barrier.sh"
  "50_precipitation_front_runtime.sh"
  "60_metrics_history_api.sh"
  "70_graph_topology_profiles.sh"
  "80_graph_modifier_memory_runtime.sh"
  "90_graph_corridor_runtime.sh"
)

FAILED=0

for test in "${TESTS[@]}"; do
  echo ""
  echo "▶️ Запуск: $test"

  if bash "tests/$test"; then
    echo "✅ $test пройден"
  else
    echo "❌ $test провален"
    FAILED=$((FAILED + 1))
  fi
done

echo ""
echo "============================================================"
if [[ "$FAILED" -eq 0 ]]; then
  echo "✅ ВСЕ ТЕСТЫ ПРОЙДЕНЫ"
else
  echo "❌ ПРОВАЛЕНО ТЕСТОВ: $FAILED"
fi
echo "============================================================"

exit "$FAILED"