#!/usr/bin/env bash
set -euo pipefail

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

API_URL="${API_URL:-http://localhost:5198}"
KAFKA_CONTAINER="${KAFKA_CONTAINER:-wildfiresimulator-kafka-1}"
OUT_DIR="/tmp/wildfire_kafka_streams_$(date +%s)"
mkdir -p "$OUT_DIR"

echo "=========================================="
echo "ТЕСТ 4: KAFKA PRODUCERS + STREAMS"
echo "=========================================="

get_topic_count() {
    local topic="$1"

    local raw
    raw=$(docker exec "$KAFKA_CONTAINER" \
        kafka-run-class kafka.tools.GetOffsetShell \
        --bootstrap-server localhost:9092 \
        --topic "$topic" \
        --partitions 0 2>/dev/null || true)

    if [[ -z "$raw" ]]; then
        echo "0"
        return
    fi

    local count
    count=$(echo "$raw" | awk -F':' '{sum += $3} END {print sum+0}')
    echo "${count:-0}"
}

echo -e "\n[1/6] Проверка Kafka producer..."
KAFKA_STATUS_FILE="$OUT_DIR/kafka_health.json"
curl -s "$API_URL/api/Health/kafka" > "$KAFKA_STATUS_FILE"

PRODUCER_TYPE=$(jq -r '.details.producerType // empty' "$KAFKA_STATUS_FILE")
IS_REAL=$(jq -r '.details.usingRealProducer // false' "$KAFKA_STATUS_FILE")

if [[ "$IS_REAL" == "true" ]]; then
    echo -e "${GREEN}✅ Используется RealKafkaProducerService${NC}"
    echo "   producerType = ${PRODUCER_TYPE:-unknown}"
else
    echo -e "${RED}❌ Используется заглушка вместо реального Kafka producer${NC}"
    cat "$KAFKA_STATUS_FILE"
    exit 1
fi

echo -e "\n[2/6] Создание тестовой симуляции..."
CREATE_FILE="$OUT_DIR/create.json"

curl -s -X POST "$API_URL/api/Simulations" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Kafka Streams Test",
    "description": "Testing Kafka processing",
    "gridWidth": 15,
    "gridHeight": 15,
    "graphType": 0,
    "initialMoistureMin": 0.25,
    "initialMoistureMax": 0.25,
    "elevationVariation": 15.0,
    "initialFireCellsCount": 2,
    "simulationSteps": 12,
    "stepDurationSeconds": 1800,
    "randomSeed": 424242,
    "temperature": 30,
    "humidity": 35,
    "windSpeed": 7,
    "windDirection": 45,
    "precipitation": 0
  }' > "$CREATE_FILE"

SIM_ID=$(jq -r '.id // empty' "$CREATE_FILE")

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
    echo -e "${RED}❌ Не удалось создать тестовую симуляцию${NC}"
    cat "$CREATE_FILE"
    exit 1
fi

echo -e "${GREEN}✅ Симуляция создана: $SIM_ID${NC}"

START_FILE="$OUT_DIR/start.json"
curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/start" > "$START_FILE"

START_SUCCESS=$(jq -r '.success // false' "$START_FILE")
if [[ "$START_SUCCESS" != "true" ]]; then
    echo -e "${RED}❌ Не удалось запустить тестовую симуляцию${NC}"
    cat "$START_FILE"
    exit 1
fi

echo -e "${GREEN}✅ Симуляция запущена${NC}"

echo -e "\n[3/6] Выполнение шагов и отправка в Kafka..."
for i in $(seq 1 8); do
    echo "   Шаг $i..."
    STEP_FILE="$OUT_DIR/step_$i.json"
    curl -s -X POST "$API_URL/api/SimulationManager/$SIM_ID/step" > "$STEP_FILE"

    STEP_SUCCESS=$(jq -r '.success // false' "$STEP_FILE")
    if [[ "$STEP_SUCCESS" != "true" ]]; then
        STATUS_CODE=$(jq -r '.status // empty' "$STEP_FILE")
        MESSAGE=$(jq -r '.message // empty' "$STEP_FILE")

        if [[ "$STATUS_CODE" == "2" ]]; then
            echo -e "${YELLOW} Симуляция завершилась раньше ожидаемого на шаге $i${NC}"
            break
        fi

        echo -e "${RED}❌ Ошибка при выполнении шага $i${NC}"
        cat "$STEP_FILE"
        [[ -n "$MESSAGE" ]] && echo "message = $MESSAGE"
        exit 1
    fi

    sleep 1
done

echo "   Ожидание обработки сообщений (4 сек)..."
sleep 4

echo -e "\n[4/6] Проверка fire-metrics топика..."
METRICS_COUNT=$(get_topic_count "fire-metrics")

if [[ "$METRICS_COUNT" -gt 0 ]]; then
    echo -e "${GREEN}✅ Найдено $METRICS_COUNT сообщений в fire-metrics${NC}"
else
    echo -e "${RED}❌ В fire-metrics нет сообщений${NC}"
    exit 1
fi

echo -e "\n[5/6] Проверка выходных топиков Kafka Streams..."

MA_COUNT=$(get_topic_count "fire-moving-averages")
TREND_COUNT=$(get_topic_count "fire-trends")
ANOMALY_COUNT=$(get_topic_count "fire-anomalies")
FORECAST_COUNT=$(get_topic_count "fire-forecasts")

echo -e "   fire-moving-averages: ${BLUE}$MA_COUNT${NC}"
echo -e "   fire-trends:          ${BLUE}$TREND_COUNT${NC}"
echo -e "   fire-anomalies:       ${BLUE}$ANOMALY_COUNT${NC}"
echo -e "   fire-forecasts:       ${BLUE}$FORECAST_COUNT${NC}"

if [[ "$MA_COUNT" -le 0 ]]; then
    echo -e "${RED}❌ fire-moving-averages пуст${NC}"
    exit 1
fi

if [[ "$TREND_COUNT" -le 0 ]]; then
    echo -e "${RED}❌ fire-trends пуст${NC}"
    exit 1
fi

if [[ "$ANOMALY_COUNT" -le 0 ]]; then
    echo -e "${YELLOW}⚠️ fire-anomalies пуст — это допустимо для данного сценария${NC}"
else
    echo -e "${GREEN}✅ fire-anomalies содержит сообщения${NC}"
fi

if [[ "$FORECAST_COUNT" -le 0 ]]; then
    echo -e "${RED}❌ fire-forecasts пуст — прогнозный поток не работает или не публикуется${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Основные выходные потоки Kafka Streams работают${NC}"

echo -e "\n[6/6] Итог"
echo "   simulation_id = $SIM_ID"
echo "   logs_dir      = $OUT_DIR"

echo -e "\n=========================================="
echo -e "${GREEN}✅ ТЕСТ 4 ЗАВЕРШЕН${NC}"
echo "=========================================="