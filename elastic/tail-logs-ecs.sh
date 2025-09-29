#!/usr/bin/env bash
set -euo pipefail

# Tail ECS Serilog logs from Elasticsearch with compact formatting.
# Now prints 3-letter level abbreviations (INF, DBG, VRB, WRN, ERR, FTL):
# 2025-09-27T01:53:03 [INF] Namespace.Class::Method | Message

URL="http://localhost:9200"
USER=""; PASS=""; INSECURE=0
PREFIX="app-logs"; WINDOW="5m"; INTERVAL=3; SIZE=20
LEVEL=""; ONCE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --url) URL="$2"; shift 2;;
    --user) USER="$2"; shift 2;;
    --pass|--password) PASS="$2"; shift 2;;
    --insecure) INSECURE=1; shift;;
    --prefix) PREFIX="$2"; shift 2;;
    --window) WINDOW="$2"; shift 2;;
    --interval) INTERVAL="$2"; shift 2;;
    --size) SIZE="$2"; shift 2;;
    --level) LEVEL="$2"; shift 2;;
    --once) ONCE=1; shift;;
    -h|--help) grep '^#' "$0" | sed 's/^#\s\{0,1\}//'; exit 0;;
    *) echo "Unknown arg: $1" >&2; exit 2;;
  esac
done

AUTH_ARGS=()
[[ -n "$USER" ]] && AUTH_ARGS+=("-u" "$USER:$PASS")
[[ "$INSECURE" == 1 ]] && AUTH_ARGS+=("-k")

if [[ "$PREFIX" == *"*"* ]]; then
  INDEX_PATTERN="$PREFIX"
else
  INDEX_PATTERN="$PREFIX*"
fi

build_query(){
  printf '{ "size": %s, "sort":[{"@timestamp":{"order":"desc"}}], "query":{"range":{"@timestamp":{"gte":"now-%s"}}}, "_source":["@timestamp","log.level","level","labels.level_text","log.logger","SourceContext","labels.SourceContext","origin.function","log.origin.function","CallerMemberName","labels.CallerMemberName","message"] }' "$SIZE" "$WINDOW"
}

poll_once(){
  local body resp newest_id newest_ts
  body=$(build_query)
  resp=$(curl -sS "${AUTH_ARGS[@]}" -H 'Content-Type: application/json' -X POST "$URL/$INDEX_PATTERN/_search" -d "$body")
  newest_id=$(jq -r '.hits.hits[0]?._id // empty' <<<"$resp")
  newest_ts=$(jq -r '.hits.hits[0]?._source["@timestamp"] // empty' <<<"$resp")
  [[ -z "$newest_id" ]] && return 0
  [[ "${LAST_ID:-}" == "$newest_id" ]] && return 0

  jq -r --arg last_ts "${LAST_TS:-}" --arg last_id "${LAST_ID:-}" --arg level "$LEVEL" '
    def abbr(l):
      (l // "Information" | tostring | ascii_upcase) as $L |
      if   ($L|startswith("VERB")) then "VRB"
      elif ($L|startswith("DEBU")) then "DBG"
      elif ($L|startswith("INFO")) then "INF"
      elif ($L|startswith("WARN")) then "WRN"
      elif ($L|startswith("ERRO")) then "ERR"
      elif ($L|startswith("FATA")) then "FTL"
      else ($L[0:3]) end;
    def want_level(lvl):
      ($level|length)==0 or (lvl == $level) or (abbr(lvl) == ($level|ascii_upcase));
    .hits.hits
    | reverse
    | map(select(((._source["@timestamp"] // "") > $last_ts) or (((._source["@timestamp"] // "") == $last_ts) and ((._id // "") != $last_id))))
    | .[]
    | ._source as $s
    | ($s["@timestamp"] | sub("Z$"; "") | gsub("T"; " ")) as $iso
    | (
        ($iso | capture("^(?<base>\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2})\\.(?<frac>\\d+)")? ) as $cap
        | if $cap != null then ($cap.base + "." + ($cap.frac[0:3])) else ($iso) end
      ) as $ts
    | ($s.demoted // false) as $dem
    | ($s["log.level"] // $s.level // $s.labels.level_text // "Information") as $lvlraw
    | (if $dem == true then "Verbose" else $lvlraw end) as $lvl
    | select(want_level($lvl))
    | ($s["log.logger"] // $s.SourceContext // $s.labels.SourceContext // "-") as $mod
    | ($s["log.origin"]["function"] // $s.origin.function // $s.CallerMemberName // $s.labels.CallerMemberName // null) as $fun
    | ($s.user.name // $s["user.name"] // $s.labels["user.name"] // null) as $usr
    | (if $fun then ($mod + "::" + $fun) else $mod end) as $where
    | (if $usr then (" ["+$usr+"]") else " []" end) as $userfrag
    | "\($ts) [\(abbr($lvl))] \($where)\($userfrag) | \($s.message)"
  ' <<<"$resp"

  LAST_ID="$newest_id"; LAST_TS="$newest_ts"
}

[[ "$ONCE" == 1 ]] && { poll_once; exit 0; }
poll_once || true
while true; do poll_once || true; sleep "$INTERVAL"; done
