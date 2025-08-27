#!/usr/bin/env bash
set -Eeuo pipefail

ES_VERSION="${ES_VERSION:-7.17.23}"   # any 7.17.x
ES_PORT="${ES_PORT:-9200}"
ES_HEAP="${ES_HEAP:-256m}"            # 128m/256m/512m are good in Codespaces

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

main() {
  local A; A="$(arch)"; [ "$A" != "unsupported" ] || { log "Unsupported CPU arch"; exit 1; }

  local BASE="$HOME/.local/share/elasticsearch-${ES_VERSION}"
  local TGZ="${BASE}/es.tgz"
  local ES_HOME="${BASE}/elasticsearch-${ES_VERSION}"
  local URL="https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-${ES_VERSION}-${A}.tar.gz"

  mkdir -p "$BASE"
  if [ ! -d "$ES_HOME" ]; then
    log "Downloading ${URL}"
    curl -fL "$URL" -o "$TGZ"
    tar -xzf "$TGZ" -C "$BASE"
  fi

  cd "$ES_HOME"
  mkdir -p data logs run
  chmod -R u+rwX data logs run

  export ES_JAVA_OPTS="-Xms${ES_HEAP} -Xmx${ES_HEAP}"

  # Stop any previous instance
  if [ -f run/es.pid ] && kill -0 "$(cat run/es.pid)" 2>/dev/null; then
    log "Stopping previous ES (PID $(cat run/es.pid))"
    kill "$(cat run/es.pid)" || true
    sleep 3
  fi

  log "Starting Elasticsearch ${ES_VERSION} on 127.0.0.1:${ES_PORT} (heap ${ES_HEAP})"
  nohup ./bin/elasticsearch \
    -d -p run/es.pid \
    -Ediscovery.type=single-node \
    -Expack.security.enabled=false \
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
}

main "$@"
