#!/usr/bin/env bash
#
# Regenerate the committed screenshot-regression baselines.
#
# Runs ONLY the ScreenshotRegressionTests fixture against the iOS
# simulator with SCREENSHOT_UPDATE=1, which makes ScreenshotComparer
# overwrite every baseline under TRViS.UITests/Screenshots/<deviceClass>/
# (instead of diffing) and pass. After it finishes, `git diff` the
# Screenshots/ tree, eyeball the changes, and commit.
#
# Usage:
#   ./update-screenshots.sh [device-class ...] [-- extra run-ui-tests.sh args]
#
#   device-class : iphone | ipad-mini-a17 | ipad-mini-5
#                  (default: iphone ipad-mini-a17 — the two canonical
#                   regression devices; ipad-mini-5 is review-only and is
#                   not part of the pixel gate, but you may still refresh
#                   its images explicitly.)
#
# Examples:
#   ./update-screenshots.sh                       # iphone + ipad-mini-a17
#   ./update-screenshots.sh iphone                # just iPhone 16
#   ./update-screenshots.sh iphone -- --skip-build
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

DEVICE_CLASSES=()
EXTRA_ARGS=()
SEEN_DDASH=false
for arg in "$@"; do
  if [[ "$SEEN_DDASH" == true ]]; then
    EXTRA_ARGS+=("$arg")
  elif [[ "$arg" == "--" ]]; then
    SEEN_DDASH=true
  else
    DEVICE_CLASSES+=("$arg")
  fi
done

if [[ ${#DEVICE_CLASSES[@]} -eq 0 ]]; then
  DEVICE_CLASSES=(iphone ipad-mini-a17)
fi

echo "==> Updating screenshot baselines for: ${DEVICE_CLASSES[*]}"

for dc in "${DEVICE_CLASSES[@]}"; do
  echo
  echo "============================================================"
  echo "  Refreshing baselines: $dc"
  echo "============================================================"
  # ${ARR[@]+"${ARR[@]}"} expands to nothing (not an unbound-variable
  # error) when the array is empty under `set -u` on bash 3.2 (macOS
  # system bash) — the same guard run-ui-tests.sh documents.
  SCREENSHOT_UPDATE=1 "$SCRIPT_DIR/run-ui-tests.sh" ios \
    --skip-install \
    --device-class="$dc" \
    --filter="FullyQualifiedName~ScreenshotRegressionTests" \
    ${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}
done

echo
echo "==> Done. Review the changes:"
echo "      git status TRViS.UITests/Screenshots"
echo "      git diff --stat TRViS.UITests/Screenshots"
echo "    then commit the baselines you accept."
