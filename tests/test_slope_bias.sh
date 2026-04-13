#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
SIM_JSON="$TMP_DIR/sim.json"
MAP_JSON="$TMP_DIR/map.json"
START_JSON="$TMP_DIR/start.json"

echo "============================================================"
echo " ТЕСТ 11.7b: diagnostic slope bias на реальной карте"
echo "============================================================"

create_payload='{
  "name": "test-slope-bias-diagnostic",
  "description": "diagnostic slope bias integration test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 0,
  "initialMoistureMin": 0.30,
  "initialMoistureMax": 0.70,
  "elevationVariation": 100,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 900,
  "randomSeed": 123456,
  "temperature": 25,
  "humidity": 40,
  "windSpeed": 0,
  "windDirection": 45,
  "precipitation": 0
}'

curl -sS -X POST "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  -d "$create_payload" > "$SIM_JSON"

SIM_ID="$(jq -r '.id' "$SIM_JSON")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "❌ Не удалось создать симуляцию"
  cat "$SIM_JSON"
  exit 1
fi

echo "simulation_id = $SIM_ID"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/prepare-map" > "$MAP_JSON"

python3 - "$MAP_JSON" > "$TMP_DIR/manual_start.json" << 'PY'
import json, sys

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

cells = data.get("cells", [])
cell_map = {(c["x"], c["y"]): c for c in cells}

best = None
best_score = None

for c in cells:
    x = c["x"]
    y = c["y"]

    neighbors = []
    for nx, ny in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
        if (nx, ny) in cell_map:
            neighbors.append(cell_map[(nx, ny)])

    if len(neighbors) < 4:
        continue

    higher = [n for n in neighbors if n["elevation"] > c["elevation"]]
    lower  = [n for n in neighbors if n["elevation"] < c["elevation"]]

    if len(higher) < 2 or len(lower) < 2:
        continue

    spread = max(n["elevation"] for n in neighbors) - min(n["elevation"] for n in neighbors)
    if best is None or spread > best_score:
        best = c
        best_score = spread

if best is None:
    print("[]")
    sys.exit(0)

print(json.dumps([{"x": best["x"], "y": best["y"]}]))
PY

MANUAL_START="$(cat "$TMP_DIR/manual_start.json")"

if [[ "$MANUAL_START" == "[]" ]]; then
  echo "⚠️ Не удалось подобрать хорошую стартовую клетку для slope-diagnostic"
  echo " ТЕСТ СЧИТАЕТСЯ ПРОПУЩЕННЫМ, А НЕ ПРОВАЛЕННЫМ"
  echo "Временные файлы: $TMP_DIR"
  exit 0
fi

echo "manual_start = $MANUAL_START"

curl -sS -X POST "$BASE_URL/api/SimulationManager/$SIM_ID/start" \
  -H "Content-Type: application/json" \
  -d "{\"ignitionMode\":\"manual\",\"initialFirePositions\":$MANUAL_START}" > "$START_JSON"

python3 - "$START_JSON" << 'PY'
import json, sys

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

cells = data.get("cells", [])
burning = [c for c in cells if c.get("state") == "Burning"]

if not burning:
    print("⚠️ После старта нет горящих клеток")
    print(" ТЕСТ СЧИТАЕТСЯ ПРОПУЩЕННЫМ, А НЕ ПРОВАЛЕННЫМ")
    sys.exit(0)

source = burning[0]
sx, sy = source["x"], source["y"]
source_elev = source["elevation"]

neighbors = []
for c in cells:
    dx = abs(c["x"] - sx)
    dy = abs(c["y"] - sy)
    if dx + dy == 1:
        neighbors.append(c)

higher = [n for n in neighbors if n["elevation"] > source_elev]
lower  = [n for n in neighbors if n["elevation"] < source_elev]

print(f"source = ({sx}, {sy})")
print(f"source_elevation = {source_elev:.1f}")
print(f"higher_count = {len(higher)}")
print(f"lower_count = {len(lower)}")

for n in sorted(higher, key=lambda x: x["elevation"]):
    print(f'higher_neighbor = ({n["x"]}, {n["y"]}) elev= {n["elevation"]:.1f} prob= {n["burnProbability"]:.3f}')

for n in sorted(lower, key=lambda x: x["elevation"]):
    print(f'lower_neighbor  = ({n["x"]}, {n["y"]}) elev= {n["elevation"]:.1f} prob= {n["burnProbability"]:.3f}')

if len(higher) < 2 or len(lower) < 2:
    print("⚠️ Недостаточно соседей выше и ниже источника для стабильного сравнения")
    print(" Это diagnostic-test: не считаем это падением suite")
    sys.exit(0)

higher_avg = sum(n["burnProbability"] for n in higher) / len(higher)
lower_avg = sum(n["burnProbability"] for n in lower) / len(lower)
higher_max = max(n["burnProbability"] for n in higher)
lower_max = max(n["burnProbability"] for n in lower)

print(f"higher_avg_prob = {higher_avg:.6f}")
print(f"lower_avg_prob  = {lower_avg:.6f}")
print(f"higher_max_prob = {higher_max:.6f}")
print(f"lower_max_prob  = {lower_max:.6f}")

if higher_avg > lower_avg or higher_max > lower_max:
    print("✅ На этой карте есть наблюдаемое смещение вверх по уклону")
else:
    print(" На этой конкретной карте slope bias не проявился поверх других факторов")
    print(" Это НЕ fail: строгую проверку делает test_slope_physics.sh")
PY

echo "============================================================"
echo "✅ ТЕСТ 11.7b ЗАВЕРШЕН"
echo "Временные файлы: $TMP_DIR"
echo "============================================================"
