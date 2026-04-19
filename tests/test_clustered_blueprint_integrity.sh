#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5198}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

CREATE_JSON="$TMP_DIR/create.json"
CREATE_RESPONSE="$TMP_DIR/create_response.json"
GRAPH_RESPONSE="$TMP_DIR/graph_response.json"

NODE_A="11111111-1111-1111-1111-111111111111"
NODE_B="22222222-2222-2222-2222-222222222222"
NODE_C="33333333-3333-3333-3333-333333333333"
NODE_D="44444444-4444-4444-4444-444444444444"
NODE_E="55555555-5555-5555-5555-555555555555"
NODE_F="66666666-6666-6666-6666-666666666666"

EDGE_1="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"
EDGE_2="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"
EDGE_3="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"
EDGE_4="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"
EDGE_5="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"

cat > "$CREATE_JSON" <<JSON
{
  "name": "clustered-blueprint-integrity",
  "description": "Strict blueprint integrity test",
  "gridWidth": 20,
  "gridHeight": 20,
  "graphType": 1,
  "initialMoistureMin": 0.20,
  "initialMoistureMax": 0.80,
  "elevationVariation": 50.0,
  "initialFireCellsCount": 1,
  "simulationSteps": 10,
  "stepDurationSeconds": 60,
  "randomSeed": 12345,
  "mapCreationMode": 2,
  "temperature": 25.0,
  "humidity": 40.0,
  "windSpeed": 4.0,
  "windDirection": 45.0,
  "precipitation": 0.0,
  "clusteredBlueprint": {
    "canvasWidth": 20,
    "canvasHeight": 20,
    "candidates": [
      { "id": "90000000-0000-0000-0000-000000000001", "x": 2,  "y": 2  },
      { "id": "90000000-0000-0000-0000-000000000002", "x": 5,  "y": 3  },
      { "id": "90000000-0000-0000-0000-000000000003", "x": 8,  "y": 4  },
      { "id": "90000000-0000-0000-0000-000000000004", "x": 12, "y": 6  },
      { "id": "90000000-0000-0000-0000-000000000005", "x": 14, "y": 8  },
      { "id": "90000000-0000-0000-0000-000000000006", "x": 16, "y": 10 }
    ],
    "nodes": [
      { "id": "$NODE_A", "x": 2,  "y": 2,  "clusterId": "A", "vegetation": 3, "moisture": 0.22, "elevation": 2.0  },
      { "id": "$NODE_B", "x": 5,  "y": 3,  "clusterId": "A", "vegetation": 4, "moisture": 0.25, "elevation": 3.0  },
      { "id": "$NODE_C", "x": 8,  "y": 4,  "clusterId": "A", "vegetation": 5, "moisture": 1.00, "elevation": 1.0  },
      { "id": "$NODE_D", "x": 12, "y": 6,  "clusterId": "B", "vegetation": 6, "moisture": 0.10, "elevation": 4.0  },
      { "id": "$NODE_E", "x": 14, "y": 8,  "clusterId": "B", "vegetation": 2, "moisture": 0.32, "elevation": 6.0  },
      { "id": "$NODE_F", "x": 16, "y": 10, "clusterId": "B", "vegetation": 1, "moisture": 0.41, "elevation": 7.0  }
    ],
    "edges": [
      { "id": "$EDGE_1", "fromNodeId": "$NODE_A", "toNodeId": "$NODE_B", "distanceOverride": 3.2, "fireSpreadModifier": 0.95 },
      { "id": "$EDGE_2", "fromNodeId": "$NODE_B", "toNodeId": "$NODE_C", "distanceOverride": 3.1, "fireSpreadModifier": 0.40 },
      { "id": "$EDGE_3", "fromNodeId": "$NODE_B", "toNodeId": "$NODE_D", "distanceOverride": 7.4, "fireSpreadModifier": 0.22 },
      { "id": "$EDGE_4", "fromNodeId": "$NODE_D", "toNodeId": "$NODE_E", "distanceOverride": 2.7, "fireSpreadModifier": 0.88 },
      { "id": "$EDGE_5", "fromNodeId": "$NODE_E", "toNodeId": "$NODE_F", "distanceOverride": 2.8, "fireSpreadModifier": 0.91 }
    ]
  }
}
JSON

echo "[INFO] creating clustered simulation..."
curl -sS -X POST \
  "$BASE_URL/api/simulations" \
  -H "Content-Type: application/json" \
  --data @"$CREATE_JSON" \
  > "$CREATE_RESPONSE"

SIM_ID="$(jq -r '.id' "$CREATE_RESPONSE")"

if [[ -z "$SIM_ID" || "$SIM_ID" == "null" ]]; then
  echo "[FAIL] simulation id not returned"
  cat "$CREATE_RESPONSE"
  exit 1
fi

echo "[INFO] simulation created: $SIM_ID"

echo "[INFO] requesting graph..."
curl -sS \
  "$BASE_URL/api/simulationmanager/$SIM_ID/graph" \
  > "$GRAPH_RESPONSE"

SUCCESS="$(jq -r '.success' "$GRAPH_RESPONSE")"
if [[ "$SUCCESS" != "true" ]]; then
  echo "[FAIL] graph endpoint returned failure"
  cat "$GRAPH_RESPONSE"
  exit 1
fi

NODE_COUNT="$(jq '.graph.nodes | length' "$GRAPH_RESPONSE")"
EDGE_COUNT="$(jq '.graph.edges | length' "$GRAPH_RESPONSE")"

[[ "$NODE_COUNT" -eq 6 ]] || { echo "[FAIL] expected 6 nodes, got $NODE_COUNT"; exit 1; }
[[ "$EDGE_COUNT" -eq 5 ]] || { echo "[FAIL] expected 5 edges, got $EDGE_COUNT"; exit 1; }

echo "[INFO] checking exact node ids..."
jq -e --arg a "$NODE_A" --arg b "$NODE_B" --arg c "$NODE_C" --arg d "$NODE_D" --arg e "$NODE_E" --arg f "$NODE_F" '
  (.graph.nodes | map(.id) | sort) ==
  ([$a,$b,$c,$d,$e,$f] | sort)
' "$GRAPH_RESPONSE" > /dev/null || {
  echo "[FAIL] node ids were not preserved"
  exit 1
}

echo "[INFO] checking exact edge ids..."
jq -e --arg e1 "$EDGE_1" --arg e2 "$EDGE_2" --arg e3 "$EDGE_3" --arg e4 "$EDGE_4" --arg e5 "$EDGE_5" '
  (.graph.edges | map(.id) | sort) ==
  ([$e1,$e2,$e3,$e4,$e5] | sort)
' "$GRAPH_RESPONSE" > /dev/null || {
  echo "[FAIL] edge ids were not preserved"
  exit 1
}

echo "[INFO] checking cluster ids..."
jq -e --arg a "$NODE_A" --arg b "$NODE_B" --arg c "$NODE_C" --arg d "$NODE_D" --arg e "$NODE_E" --arg f "$NODE_F" '
  [
    (.graph.nodes[] | select(.id == $a) | .groupKey == "A"),
    (.graph.nodes[] | select(.id == $b) | .groupKey == "A"),
    (.graph.nodes[] | select(.id == $c) | .groupKey == "A"),
    (.graph.nodes[] | select(.id == $d) | .groupKey == "B"),
    (.graph.nodes[] | select(.id == $e) | .groupKey == "B"),
    (.graph.nodes[] | select(.id == $f) | .groupKey == "B")
  ] | all
' "$GRAPH_RESPONSE" > /dev/null || {
  echo "[FAIL] cluster ids were not preserved"
  exit 1
}

echo "[INFO] checking vegetation preservation..."
jq -e --arg c "$NODE_C" --arg d "$NODE_D" '
  [
    (.graph.nodes[] | select(.id == $c) | .vegetation == "Water"),
    (.graph.nodes[] | select(.id == $d) | .vegetation == "Bare")
  ] | all
' "$GRAPH_RESPONSE" > /dev/null || {
  echo "[FAIL] water/bare vegetation were not preserved"
  exit 1
}

echo "[INFO] checking no extra edges..."
jq -e '
  def canon(a;b):
    if a < b then (a + "|" + b) else (b + "|" + a) end;

  (.graph.edges | map(canon(.fromCellId; .toCellId)) | sort) ==
  ([
    canon("'"$NODE_A"'"; "'"$NODE_B"'"),
    canon("'"$NODE_B"'"; "'"$NODE_C"'"),
    canon("'"$NODE_B"'"; "'"$NODE_D"'"),
    canon("'"$NODE_D"'"; "'"$NODE_E"'"),
    canon("'"$NODE_E"'"; "'"$NODE_F"'")
  ] | sort)
' "$GRAPH_RESPONSE" > /dev/null || {
  echo "[FAIL] edge topology changed or extra edges appeared"
  exit 1
}

echo "[PASS] clustered blueprint integrity preserved"