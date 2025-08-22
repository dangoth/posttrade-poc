#!/bin/bash

echo "Kafka Topic Initialization Container Starting..."

echo "Waiting for Kafka to be ready..."
max_attempts=30
attempt=0

until kafka-topics --bootstrap-server kafka:29092 --list > /dev/null 2>&1; do
  attempt=$((attempt + 1))
  if [ $attempt -ge $max_attempts ]; then
    echo "ERROR: Kafka not ready after $max_attempts attempts"
    exit 1
  fi
  echo "Kafka not ready yet, waiting 5 seconds... (attempt $attempt/$max_attempts)"
  sleep 5
done

echo "Kafka is ready! Creating topics..."

create_topic() {
  local topic_name=$1
  local partitions=$2
  local replication_factor=$3
  local config=$4
  
  echo "Creating topic: $topic_name"
  if kafka-topics --create --if-not-exists --bootstrap-server kafka:29092 \
    --topic "$topic_name" --partitions "$partitions" --replication-factor "$replication_factor" \
    --config "$config"; then
    echo "Topic $topic_name created successfully"
  else
    echo "Failed to create topic $topic_name"
    return 1
  fi
}

# Trade topics (retention-based, 7 days)
echo "Creating trade topics with retention policy..."
create_topic "trades.equities" 3 1 "retention.ms=604800000"
create_topic "trades.options" 3 1 "retention.ms=604800000"
create_topic "trades.fx" 3 1 "retention.ms=604800000"

# Reference data topics (compaction-based)
echo "Creating reference data topics with compaction policy..."
create_topic "reference.instruments" 1 1 "cleanup.policy=compact"
create_topic "reference.traders" 1 1 "cleanup.policy=compact"

echo "All topics created successfully!"
echo "Final topic list:"
kafka-topics --list --bootstrap-server kafka:29092

echo "Kafka initialization completed successfully!"