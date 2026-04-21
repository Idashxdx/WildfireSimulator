#!/usr/bin/env bash
set -e

echo "Reset Docker networking..."

docker compose down -v --remove-orphans || true
docker network prune -f || true

sudo systemctl stop docker || true
sudo iptables -F || true
sudo iptables -t nat -F || true
sudo iptables -t mangle -F || true
sudo iptables -X || true
sudo nft flush ruleset || true

sudo systemctl start docker

echo "Docker network reset done."
docker info | grep -i "Firewall Backend" || true