#!/bin/bash

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo "=========================================="
echo "ТЕСТ 1: ИНФРАСТРУКТУРА"
echo "PostgreSQL + Kafka + API"
echo "=========================================="

echo -e "\n[1/5] Проверка Docker контейнеров..."
if docker ps | grep -q "postgres"; then
    echo -e "${GREEN}✅ PostgreSQL запущен${NC}"
else
    echo -e "${RED}❌ PostgreSQL не запущен${NC}"
    exit 1
fi

if docker ps | grep -q "kafka"; then
    echo -e "${GREEN}✅ Kafka запущен${NC}"
else
    echo -e "${RED}❌ Kafka не запущен${NC}"
    exit 1
fi

if docker ps | grep -q "zookeeper"; then
    echo -e "${GREEN}✅ Zookeeper запущен${NC}"
fi

echo -e "\n[2/5] Проверка PostgreSQL..."
if docker exec wildfiresimulator-postgres-1 pg_isready -U wildfire_user > /dev/null 2>&1; then
    echo -e "${GREEN}✅ PostgreSQL доступен${NC}"

    TABLES=$(docker exec wildfiresimulator-postgres-1 psql -U wildfire_user -d wildfire_db -t -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';" | xargs)
    echo "   Таблиц в БД: $TABLES"
else
    echo -e "${RED}❌ PostgreSQL недоступен${NC}"
fi

echo -e "\n[3/5] Проверка Kafka топиков..."
TOPICS=$(docker exec wildfiresimulator-kafka-1 kafka-topics --bootstrap-server localhost:9092 --list 2>/dev/null)
EXPECTED_TOPICS=("fire-events" "fire-metrics" "fire-moving-averages" "fire-trends" "fire-anomalies")

for topic in "${EXPECTED_TOPICS[@]}"; do
    if echo "$TOPICS" | grep -q "$topic"; then
        echo -e "${GREEN}  ✅ $topic${NC}"
    else
        echo -e "${RED}  ❌ $topic${NC}"
    fi
done

echo -e "\n[4/5] Проверка Consumer Groups..."
GROUPS=$(docker exec wildfiresimulator-kafka-1 kafka-consumer-groups --bootstrap-server localhost:9092 --list 2>/dev/null)
EXPECTED_GROUPS=("kafka-streams-processor")

for group in "${EXPECTED_GROUPS[@]}"; do
    if echo "$GROUPS" | grep -q "$group"; then
        echo -e "${GREEN}  ✅ $group${NC}"

        docker exec wildfiresimulator-kafka-1 kafka-consumer-groups --bootstrap-server localhost:9092 --group $group --describe 2>/dev/null | tail -n +2 | while read line; do
            if [ ! -z "$line" ]; then
                echo "     $line"
            fi
        done
    else
        echo -e "${YELLOW}  ⚠️ $group не найдена (создастся при первом запуске)${NC}"
    fi
done

echo -e "\n[5/5] Проверка API..."
HEALTH=$(curl -s http://localhost:5198/health)
if [ $? -eq 0 ]; then
    STATUS=$(echo "$HEALTH" | grep -o '"status":"[^"]*"' | cut -d'"' -f4)
    echo -e "${GREEN}✅ API доступен, статус: $STATUS${NC}"

    KAFKA_STATUS=$(curl -s http://localhost:5198/api/Health/kafka)
    PRODUCER_TYPE=$(echo "$KAFKA_STATUS" | grep -o '"producerType":"[^"]*"' | cut -d'"' -f4)
    echo "   Kafka producer: $PRODUCER_TYPE"
else
    echo -e "${RED}❌ API недоступен${NC}"
fi

echo -e "\n=========================================="
echo "✅ ТЕСТ 1 ЗАВЕРШЕН"
echo "=========================================="