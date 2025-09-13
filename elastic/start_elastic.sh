#!/usr/bin/env bash
set -Eeuo pipefail

ES_VERSION="${ES_VERSION:-8.19.3}"
ES_PORT="${ES_PORT:-9200}"
ES_HEAP="${ES_HEAP:-256m}"

start_dir=$(pwd)
ES_BASE="$HOME/.local/share/elasticsearch-${ES_VERSION}"
ES_HOME="${ES_BASE}/elasticsearch-${ES_VERSION}"

log(){ printf '[%s] %s\n' "$(date +%H:%M:%S)" "$*"; }

arch() {
  case "$(uname -m)" in
    x86_64|amd64) echo "linux-x86_64" ;;
    aarch64|arm64) echo "linux-aarch64" ;;
    *) echo "unsupported" ;;
  esac
}

waitfor() {
  local url=$1 i
  for i in $(seq 1 120); do
    if curl -fsS --max-time 1 "$url" >/dev/null 2>&1; then return 0; fi
    sleep 1
  done
  return 1
}

# Cleanup function
cleanup() {
  log "Performing cleanup of Elasticsearch artifacts..."

  # Kill any running Elasticsearch process from a previous run
  if [ -f "${ES_HOME}/run/es.pid" ] && kill -0 "$(cat "${ES_HOME}/run/es.pid")" 2>/dev/null; then
    log "Stopping previous ES (PID $(cat "${ES_HOME}/run/es.pid"))"
    kill "$(cat "${ES_HOME}/run/es.pid")" || true
    sleep 3
  fi

  # Remove all installation files, including binaries, logs, and data
  log "Removing old installation directory: ${ES_BASE}"
  rm -rf "$ES_BASE"
  
  # Remove all installation files from old versions
  log "Removing any previous Elasticsearch installations."
  rm -rf "$HOME/.local/share/elasticsearch-*"
}

main() {
  cleanup

  local A; A="$(arch)"; [ "$A" != "unsupported" ] || { log "Unsupported CPU arch"; exit 1; }

  local TGZ="${ES_BASE}/es.tgz"
  local URL="https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-${ES_VERSION}-${A}.tar.gz"
  local CONFIG_FILE="${ES_HOME}/config/elasticsearch.yml"

  mkdir -p "$ES_BASE"
  if [ ! -d "$ES_HOME" ]; then
    log "Downloading ${URL}"
    curl -fL "$URL" -o "$TGZ"
    tar -xzf "$TGZ" -C "$ES_BASE"
    
    # Remove the tarball after extraction
    log "Removing downloaded tarball to free up space."
    rm "$TGZ"
  fi

  cd "$ES_HOME"
  # Use a robust way to ensure data and logs directories are clean
  rm -rf data logs
  mkdir -p data logs run
  chmod -R u+rwX data logs run

  # Add settings directly to elasticsearch.yml before starting Elasticsearch
  log "Adding settings to elasticsearch.yml."
  echo "indices.id_field_data.enabled: true" >> "$CONFIG_FILE"
  echo "cluster.routing.allocation.disk.watermark.low: 95%" >> "$CONFIG_FILE"
  echo "cluster.routing.allocation.disk.watermark.high: 97%" >> "$CONFIG_FILE"
  echo "cluster.routing.allocation.disk.watermark.flood_stage: 99%" >> "$CONFIG_FILE"
  echo "cluster.max_shards_per_node: 10000" >> "$CONFIG_FILE"

  export ES_JAVA_OPTS="-Xms${ES_HEAP} -Xmx${ES_HEAP}"

  log "Starting Elasticsearch ${ES_VERSION} on 127.0.0.1:${ES_PORT} (heap ${ES_HEAP})"
  nohup ./bin/elasticsearch \
    -d -p run/es.pid \
    -Ediscovery.type=single-node \
    -Expack.security.enabled=false \
    -Expack.security.http.ssl.enabled=false \
    -Enetwork.host=127.0.0.1 \
    -Ehttp.port="${ES_PORT}" \
    -Epath.data="$(pwd)/data" \
    -Epath.logs="$(pwd)/logs" \
    >/dev/null 2>&1 || true

  log "Waiting for http://127.0.0.1:${ES_PORT} ..."
  if waitfor "http://127.0.0.1:${ES_PORT}"; then
    log "Elasticsearch is UP ✅"
    curl -fsS "http://127.0.0.1:${ES_PORT}" | sed -n '1,10p' || true
    log "Logs: $(pwd)/logs/elasticsearch.log"
  else
    log "Elasticsearch did not start. Last 100 log lines:"
    tail -n 100 "logs/elasticsearch.log" || true
    exit 1
  fi

  sleep 10
  
  cd "$start_dir"

  log "Loading birthday data into Elasticsearch"
  ./load_birthdays.sh

  log "loading movie data into Elasticsearch"
  ./load_movies.sh

  log "loading supreme data into Elasticsearch"
  ./load_supreme.sh
}

main "$@"
