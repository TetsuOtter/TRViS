#!/usr/bin/env bash
# ================================================================
# update-screenshots-apple.sh
# Regenerates XCUITest screenshot baselines for both device classes.
#
# Mirrors update-screenshots.sh (Appium) for the Apple-platform test suite.
#
# Usage:
#   ./update-screenshots-apple.sh [options]
#
#   Options forwarded to run-ui-tests-apple.sh:
#     --skip-build        Skip dotnet build (use existing .app)
#     --skip-install      Skip xcrun simctl install
#     --device-class=<dc> Update a single device class instead of both
#                         Allowed: iphone | ipad-mini-a17
#
# What it does:
#   1. Calls run-ui-tests-apple.sh ios --screenshot-matrix --update-screenshots
#      which sets TEST_RUNNER_SCREENSHOT_UPDATE=1 in the xcodebuild invocation,
#      causing ScreenshotBaselineHelper to overwrite baseline PNGs instead of
#      diffing against them.
#   2. Each captured frame is written to:
#        TRViS.UITests.Apple/Screenshots/<deviceClass>/<theme>/<lang>/<screen>.png
#   3. Prints a summary of newly/updated files at the end.
#
# After running, review the changed PNGs with `git diff --stat` then commit:
#   git add TRViS.UITests.Apple/Screenshots
#   git commit -m "chore(screenshots): update XCUITest baselines"
# ================================================================

set -euo pipefail

log()  { printf '[%s] %b\n' "$(date '+%H:%M:%S')" "$*"; }
err()  { printf '[%s] ERROR: %b\n' "$(date '+%H:%M:%S')" "$*" >&2; }
die()  { err "$*"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCREENSHOTS_DIR="$SCRIPT_DIR/TRViS.UITests.Apple/Screenshots"

# Forward unrecognised flags to run-ui-tests-apple.sh
PASSTHROUGH_ARGS=()
SINGLE_DEVICE_CLASS=""

for arg in "$@"; do
  case "$arg" in
    --device-class=*) SINGLE_DEVICE_CLASS="${arg#*=}"; PASSTHROUGH_ARGS+=("$arg") ;;
    *)                PASSTHROUGH_ARGS+=("$arg") ;;
  esac
done

log "=== update-screenshots-apple.sh ==="
log "Baselines will be written to: $SCREENSHOTS_DIR"

if [[ -n "$SINGLE_DEVICE_CLASS" ]]; then
  log "Updating single device class: $SINGLE_DEVICE_CLASS"
  "$SCRIPT_DIR/run-ui-tests-apple.sh" ios \
    --update-screenshots \
    "${PASSTHROUGH_ARGS[@]+"${PASSTHROUGH_ARGS[@]}"}"
else
  log "Updating all device classes: iphone, ipad-mini-a17"
  "$SCRIPT_DIR/run-ui-tests-apple.sh" ios \
    --update-screenshots \
    --screenshot-matrix \
    "${PASSTHROUGH_ARGS[@]+"${PASSTHROUGH_ARGS[@]}"}"
fi

log ""
log "=== Baseline update complete ==="
log "Changed files:"
git -C "$SCRIPT_DIR" diff --stat -- TRViS.UITests.Apple/Screenshots 2>/dev/null || true
git -C "$SCRIPT_DIR" status --short -- TRViS.UITests.Apple/Screenshots 2>/dev/null || true
log ""
log "Review the diffs, then:"
log "  git add TRViS.UITests.Apple/Screenshots"
log "  git commit -m 'chore(screenshots): update XCUITest baselines'"
