#!/usr/bin/env bash
# Create (or reuse) a GitHub Projects v2 Kanban board for remaining Arcade work
# and attach open issues from this repo.
#
# The Cursor GitHub App token cannot create org Projects. Run this once locally
# as an org owner/admin with the `project` scope:
#
#   gh auth refresh -s project,read:project
#   ./tools/setup-github-kanban.sh
#
# Optional env:
#   OWNER=gfed-official
#   REPO=gfed-official/performative-mail
#   PROJECT_TITLE="Performative Mail — Arcade Board"
#   SKIP_ISSUE_NUMBERS="5"   # comma-separated issues to leave off the board

set -euo pipefail

OWNER="${OWNER:-gfed-official}"
REPO="${REPO:-gfed-official/performative-mail}"
PROJECT_TITLE="${PROJECT_TITLE:-Performative Mail — Arcade Board}"
SKIP_ISSUE_NUMBERS="${SKIP_ISSUE_NUMBERS:-5}"

need() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "error: required command not found: $1" >&2
    exit 1
  }
}

need gh
need jq

echo "==> Checking gh auth (needs project scope)"
if ! gh auth status -h github.com 2>&1 | grep -qi 'Logged in'; then
  echo "error: not logged in to github.com; run: gh auth login" >&2
  exit 1
fi

echo "==> Looking for existing project titled: ${PROJECT_TITLE}"
PROJECT_JSON="$(
  gh project list --owner "${OWNER}" --limit 100 --format json \
    | jq -c --arg title "${PROJECT_TITLE}" \
      '.projects[] | select(.title == $title) | {number, id, url, title}' \
    | head -n 1
)"

if [[ -z "${PROJECT_JSON}" ]]; then
  echo "==> Creating project"
  PROJECT_JSON="$(
    gh project create --owner "${OWNER}" --title "${PROJECT_TITLE}" --format json \
      | jq -c '{number, id, url, title}'
  )"
else
  echo "==> Reusing existing project"
fi

PROJECT_NUMBER="$(jq -r '.number' <<<"${PROJECT_JSON}")"
PROJECT_ID="$(jq -r '.id' <<<"${PROJECT_JSON}")"
PROJECT_URL="$(jq -r '.url' <<<"${PROJECT_JSON}")"

echo "    number=${PROJECT_NUMBER}"
echo "    id=${PROJECT_ID}"
echo "    url=${PROJECT_URL}"

echo "==> Linking project to ${REPO}"
gh project link "${PROJECT_NUMBER}" --owner "${OWNER}" --repo "${REPO}" >/dev/null || true

echo "==> Resolving Status field options"
FIELDS_JSON="$(gh project field-list "${PROJECT_NUMBER}" --owner "${OWNER}" --format json)"
STATUS_FIELD_ID="$(jq -r '.fields[] | select(.name == "Status") | .id' <<<"${FIELDS_JSON}")"
TODO_OPTION_ID="$(
  jq -r '
    .fields[]
    | select(.name == "Status")
    | .options[]
    | select(.name == "Todo" or .name == "To Do" or .name == "Backlog")
    | .id
  ' <<<"${FIELDS_JSON}" | head -n 1
)"

if [[ -z "${STATUS_FIELD_ID}" || "${STATUS_FIELD_ID}" == "null" ]]; then
  echo "error: project has no Status field; open the project UI once to initialize the board view" >&2
  exit 1
fi

if [[ -z "${TODO_OPTION_ID}" || "${TODO_OPTION_ID}" == "null" ]]; then
  echo "warn: could not find Todo/Backlog Status option; items will be added without Status" >&2
  TODO_OPTION_ID=""
fi

echo "==> Collecting open issues from ${REPO}"
mapfile -t ISSUE_NUMBERS < <(
  gh issue list --repo "${REPO}" --state open --limit 200 --json number,title \
    | jq -r --arg skip "${SKIP_ISSUE_NUMBERS}" '
        ($skip | split(",") | map(tonumber? // empty)) as $skip_nums
        | .[]
        | select((.number as $n | ($skip_nums | index($n)) | not))
        | .number
      ' | sort -n
)

if [[ "${#ISSUE_NUMBERS[@]}" -eq 0 ]]; then
  echo "No open issues to add (after skip filter)."
  echo "Board URL: ${PROJECT_URL}"
  exit 0
fi

EXISTING_ITEM_ISSUE_NUMBERS="$(
  gh project item-list "${PROJECT_NUMBER}" --owner "${OWNER}" --limit 500 --format json \
    | jq -r '[.items[] | select(.content.number != null) | .content.number] | unique | .[]' \
    || true
)"

added=0
skipped=0
for num in "${ISSUE_NUMBERS[@]}"; do
  if grep -qx "${num}" <<<"${EXISTING_ITEM_ISSUE_NUMBERS}"; then
    echo "    skip #${num} (already on board)"
    skipped=$((skipped + 1))
    continue
  fi

  echo "    add #${num}"
  ITEM_JSON="$(
    gh project item-add "${PROJECT_NUMBER}" --owner "${OWNER}" \
      --url "https://github.com/${REPO}/issues/${num}" --format json
  )"
  ITEM_ID="$(jq -r '.id' <<<"${ITEM_JSON}")"

  if [[ -n "${TODO_OPTION_ID}" && -n "${ITEM_ID}" && "${ITEM_ID}" != "null" ]]; then
    gh project item-edit \
      --project-id "${PROJECT_ID}" \
      --id "${ITEM_ID}" \
      --field-id "${STATUS_FIELD_ID}" \
      --single-select-option-id "${TODO_OPTION_ID}" >/dev/null
  fi
  added=$((added + 1))
done

echo
echo "Done."
echo "  added=${added} skipped=${skipped}"
echo "  board=${PROJECT_URL}"
echo
echo "In the project UI: ensure the Board view uses the Status field"
echo "(Todo / In Progress / Done). Close leftover probe issue #5 if still open."
