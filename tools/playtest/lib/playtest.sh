# Shared helpers for tools/playtest/run.sh. Sourced, not executed.

have() {
  command -v "$1" >/dev/null 2>&1
}

iso_now() {
  date -u +%Y-%m-%dT%H:%M:%SZ
}

git_sha() {
  git -C "$ROOT" rev-parse HEAD 2>/dev/null || echo "unknown"
}

git_dirty() {
  [[ -n "$(git -C "$ROOT" status --porcelain 2>/dev/null)" ]]
}

have_godot() {
  have godot
}

have_dotnet() {
  have dotnet
}

display_up() {
  local d="${1:-${DISPLAY:-}}"
  [[ -n "$d" ]] || return 1
  if have xdpyinfo; then
    xdpyinfo -display "$d" >/dev/null 2>&1
    return
  fi
  [[ -e /tmp/.X11-unix/X"${d#:}" ]]
}

tail_detail() {
  local file="$1"
  [[ -f "$file" ]] || return 0
  # Keep the report small; full logs stay under artifacts/playtest/<id>/logs/.
  tail -n 40 "$file" | tr -d '\000-\010\013\014\016-\037' | tail -c 2400
}

append_check() {
  local id="$1" name="$2" status="$3" detail="${4:-}"
  local shots_json="${5:-[]}"
  case "$status" in
    pass|fail|skip) ;;
    *)
      echo "ERROR: invalid check status: $status" >&2
      exit 2
      ;;
  esac
  jq -n \
    --arg id "$id" \
    --arg name "$name" \
    --arg status "$status" \
    --arg detail "$detail" \
    --argjson shots "$shots_json" \
    '{id:$id,name:$name,status:$status,detail:$detail,shots:$shots}' \
    >>"$CHECKS_FILE"
}

shots_json_from_files() {
  if [[ "$#" -eq 0 ]]; then
    echo '[]'
    return
  fi
  jq -n --args '$ARGS.positional' -- "$@"
}

record_shot() {
  local path="$1"
  [[ -f "$path" ]] || return 1
  printf '%s\n' "$path" >>"$SHOTS_FILE"
}

write_report() {
  local ok_json
  if [[ "$1" == "true" ]]; then
    ok_json=true
  else
    ok_json=false
  fi
  local checks shots
  if [[ -s "$CHECKS_FILE" ]]; then
    checks="$(jq -s '.' "$CHECKS_FILE")"
  else
    checks='[]'
  fi
  if [[ -s "$SHOTS_FILE" ]]; then
    shots="$(jq -R -s 'split("\n") | map(select(length>0))' "$SHOTS_FILE")"
  else
    shots='[]'
  fi
  jq -n \
    --argjson ok "$ok_json" \
    --arg sha "$SHA" \
    --arg startedAt "$STARTED_AT" \
    --arg finishedAt "$FINISHED_AT" \
    --argjson checks "$checks" \
    --argjson shots "$shots" \
    '{ok:$ok,sha:$sha,startedAt:$startedAt,finishedAt:$finishedAt,checks:$checks,shots:$shots}' \
    >"$REPORT_FILE"
}

default_route_src() {
  local line
  line="$(ip -4 route get 8.8.8.8 2>/dev/null | head -n1 || true)"
  [[ -n "$line" ]] || return 1
  if [[ "$line" =~ src[[:space:]]+([0-9.]+) ]]; then
    printf '%s\n' "${BASH_REMATCH[1]}"
    return 0
  fi
  return 1
}

advertisable_ipv4() {
  local ip="$1"
  [[ "$ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
  [[ "$ip" != 127.* ]] || return 1
  [[ "$ip" != 0.0.0.0 ]] || return 1
  [[ "$ip" != 169.254.* ]] || return 1
  return 0
}

capture_display() {
  local dest="$1"
  local display="${DISPLAY:-}"
  local geom
  mkdir -p "$(dirname "$dest")"

  if have scrot; then
    if scrot -o "$dest" 2>/dev/null || scrot "$dest"; then
      [[ -s "$dest" ]] && return 0
    fi
  fi

  if have import; then
    if import -silent -window root "$dest"; then
      [[ -s "$dest" ]] && return 0
    fi
  fi

  if have ffmpeg && [[ -n "$display" ]]; then
    geom="$(xdpyinfo -display "$display" 2>/dev/null | awk '/dimensions:/{print $2; exit}')"
    geom="${geom:-1280x720}"
    if ffmpeg -hide_banner -loglevel error -y -f x11grab \
      -video_size "$geom" -i "$display" -frames:v 1 "$dest"; then
      [[ -s "$dest" ]] && return 0
    fi
  fi

  return 1
}

run_logged() {
  local log="$1"
  shift
  mkdir -p "$(dirname "$log")"
  "$@" >"$log" 2>&1
}
