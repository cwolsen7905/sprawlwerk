#!/usr/bin/env bash
# Fails if a file under game/assets/ is not listed in ASSETS.md, or is listed
# with a licence that is not allowed. Godot's .import sidecars are skipped.
# Run from anywhere; CI runs it on every push. Usage: tools/check-assets.sh
set -euo pipefail

cd "$(dirname "$0")/.."
allowed='CC0-1.0|CC-BY-3.0|CC-BY-4.0'
fail=0

if [ ! -d game/assets ]; then
  echo "check-assets: ok (no game/assets)"
  exit 0
fi

while IFS= read -r -d '' file; do
  row=$(grep -F "| \`$file\` |" ASSETS.md || true)
  if [ -z "$row" ]; then
    echo "check-assets: FAILED: $file is not listed in ASSETS.md" >&2
    fail=1
    continue
  fi
  # Columns: File | Title | Author | Source | Licence | Notes
  licence=$(printf '%s\n' "$row" | awk -F'|' '{gsub(/^ +| +$/, "", $6); print $6}')
  if ! printf '%s\n' "$licence" | grep -qxE "$allowed"; then
    echo "check-assets: FAILED: $file has licence '$licence'; allowed: ${allowed//|/, }" >&2
    fail=1
  fi
done < <(find game/assets -type f ! -name '*.import' ! -name '.DS_Store' -print0 | sort -z)

# Listed files that no longer exist.
while IFS= read -r listed; do
  [ -f "$listed" ] || { echo "check-assets: FAILED: ASSETS.md lists $listed, which does not exist" >&2; fail=1; }
done < <(grep -oE '^\| `game/assets/[^`]+`' ASSETS.md | sed -E 's/^\| `//; s/`$//')

if [ "$fail" -eq 0 ]; then
  echo "check-assets: ok ($(find game/assets -type f ! -name '*.import' ! -name '.DS_Store' | wc -l | tr -d ' ') files listed with allowed licences)"
fi
exit "$fail"
