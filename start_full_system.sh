#!/bin/bash
set -euo pipefail

echo "=========================================="
echo "ЗАПУСК WILDFIRE SIMULATOR"
echo "=========================================="
echo ""

API_PID=""

cleanup() {
    if [[ -n "${API_PID}" ]]; then
        echo ""
        echo "- Остановка API (PID: ${API_PID})..."
        kill "${API_PID}" 2>/dev/null || true
    fi
}

trap cleanup EXIT

echo "[1/10] Очистка старых данных..."
docker compose down -v 2>/dev/null || true
sudo rm -rf /tmp/kafka-logs /tmp/zookeeper 2>/dev/null || true
echo "✅ Очистка завершена"
echo ""

echo "[2/10] Запуск контейнеров (PostgreSQL, Kafka, Zookeeper)..."
docker compose up -d
echo "✅ Контейнеры запущены"
echo ""

echo "[3/10] Ожидание запуска контейнеров..."
sleep 25
echo "✅ Базовое ожидание завершено"
echo ""

echo "[4/10] Проверка статуса контейнеров..."
docker compose ps
echo ""

echo "[5/10] Проверка готовности Kafka..."
KAFKA_READY=0
for i in {1..40}; do
    if docker compose exec -T kafka kafka-broker-api-versions --bootstrap-server localhost:9092 >/dev/null 2>&1; then
        KAFKA_READY=1
        echo "✅ Kafka готова!"
        break
    fi

    echo "Ожидание Kafka... ($i/40)"
    sleep 3
done

if [[ "$KAFKA_READY" -ne 1 ]]; then
    echo "❌ Kafka не стала готова вовремя"
    exit 1
fi
echo ""

echo "[6/9] Создание Kafka топиков..."
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic fire-events --partitions 1 --replication-factor 1 --if-not-exists
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic fire-metrics --partitions 1 --replication-factor 1 --if-not-exists
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic fire-moving-averages --partitions 1 --replication-factor 1 --if-not-exists
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic fire-trends --partitions 1 --replication-factor 1 --if-not-exists
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic fire-anomalies --partitions 1 --replication-factor 1 --if-not-exists
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --create --topic fire-forecasts --partitions 1 --replication-factor 1 --if-not-exists
echo "✅ Топики созданы"
echo ""

echo "[7/10] Проверка созданных топиков..."
docker compose exec -T kafka kafka-topics --bootstrap-server localhost:9092 --list | sort
echo ""

echo "[8/10] Проверка, что forecast topics точно существуют..."
docker compose exec kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic fire-forecasts
echo "✅ Проверка forecast topic завершена"
echo ""

echo "[9/10] Запуск API..."
dotnet run --project WildfireSimulator.API &
API_PID=$!
echo "✅ API запущен (PID: $API_PID)"
echo ""

echo "[10/10] Ожидание готовности API..."
API_READY=0
for i in {1..60}; do
    if curl -s http://localhost:5198/health >/dev/null 2>&1; then
        API_READY=1
        echo "✅ API готов!"
        break
    fi

    echo "Ожидание API... ($i/60)"
    sleep 2
done

if [[ "$API_READY" -ne 1 ]]; then
    echo "❌ API не стало готово вовремя"
    exit 1
fi
echo ""

echo "=========================================="
echo "✅ СИСТЕМА ЗАПУЩЕНА!"
echo "=========================================="
echo ""
echo "- Доступные сервисы:"
echo "   API: http://localhost:5198"
echo "   Health Check: http://localhost:5198/health"
echo "   Swagger: http://localhost:5198/api-docs"
echo "   Web UI: http://localhost:5198"
echo "   SignalR Hub: http://localhost:5198/fireHub"
echo ""
echo "- Запуск тестов:"
echo "   ./tests/run_all_tests.sh"
echo ""
echo "- Запуск клиента (в новом терминале):"
echo "   cd WildfireSimulator.Client && dotnet run"
echo ""
echo "- Остановка системы:"
echo "   kill $API_PID && docker compose down"
echo ""
echo " Чтобы этот терминал не завершился и trap не убил API, оставь его открытым."
wait "$API_PID"