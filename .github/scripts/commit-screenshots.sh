#!/usr/bin/env bash
# Commits one platform's screenshot folder. Each platform job runs this, so it retries the push if another job pushed first.
set -euo pipefail
DIR="$1"
PLATFORM="$2"
git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git add "$DIR"
if git diff --cached --quiet; then
  echo "$PLATFORM screenshots unchanged"
  exit 0
fi
git commit -m "docs: update $PLATFORM screenshots [skip ci]"
for attempt in 1 2 3 4 5; do
  git pull --rebase && git push && exit 0
  sleep $((attempt * 5))
done
exit 1
