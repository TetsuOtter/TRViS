#!/usr/bin/env bash
# ================================================================
# TRViS UI Test Local Runner
# Usage: ./run-ui-tests.sh [platform] [options]
#   platform: mac (default)  – Mac Catalyst (no signing needed)
#             ios             – iOS Simulator (no signing needed)
#             device [UDID]   – Real iOS device (requires valid Apple
#                               Developer certificate + provisioning)
#   options:  --skip-build   Skip the build step
#             --skip-install Skip Appium driver installation
#             --device-class=<class>
#                            (iOS only) which simulator to target.
#                            One of: iphone (default), ipad-mini-5,
#                            ipad-mini-a17.
# ================================================================

set -euo pipefail

# ── Helpers (defined early so they are available everywhere) ─────
log()  { printf '[%s] %b\n' "$(date '+%H:%M:%S')" "$*"; }
err()  { printf '[%s] ERROR: %b\n' "$(date '+%H:%M:%S')" "$*" >&2; }
die()  { err "$*"; exit 1; }

# ── Portable timeout ─────────────────────────────────────────────
# macOS ships GNU coreutils' 'timeout' as 'gtimeout'; create a wrapper
# so the rest of the script can use 'timeout' uniformly.
if ! command -v timeout >/dev/null 2>&1; then
  if command -v gtimeout >/dev/null 2>&1; then
    timeout() { gtimeout "$@"; }
  else
    die "'timeout' command not found. Install GNU coreutils (brew install coreutils)."
  fi
fi

# ── Defaults ────────────────────────────────────────────────────
PLATFORM="${1:-mac}"
DEVICE_UDID_OVERRIDE=""  # Optional explicit device UDID (for real device)
SKIP_BUILD=false
SKIP_INSTALL=false
# iOS simulator device class. Affects iPad-vs-iPhone selection only.
# "iphone"        -> iPhone 16 (default; matches the historical behavior)
# "ipad-mini-5"   -> iPad mini (5th generation)
# "ipad-mini-a17" -> iPad mini (A17 Pro)
DEVICE_CLASS="iphone"

# Consume positional device UDID if provided after "device"
if [[ "$PLATFORM" == "device" && -n "${2:-}" && "${2}" != --* ]]; then
  DEVICE_UDID_OVERRIDE="$2"
fi

# Optional VSTest filter expression passed straight to `dotnet test
# --filter` (e.g. "FullyQualifiedName~ScreenshotRegressionTests" to run
# only the screenshot-regression fixture for a baseline refresh).
TEST_FILTER=""

for arg in "$@"; do
  case "$arg" in
    --skip-build)   SKIP_BUILD=true ;;
    --skip-install) SKIP_INSTALL=true ;;
    --device-class=*) DEVICE_CLASS="${arg#*=}" ;;
    --filter=*)     TEST_FILTER="${arg#*=}" ;;
  esac
done

# Map device class to a friendly name suffix used for the result file and logs.
case "$DEVICE_CLASS" in
  iphone)        DEVICE_CLASS_SUFFIX="iphone" ;;
  ipad-mini-5)   DEVICE_CLASS_SUFFIX="ipad-mini-5" ;;
  ipad-mini-a17) DEVICE_CLASS_SUFFIX="ipad-mini-a17" ;;
  *)             die "Unknown --device-class: '$DEVICE_CLASS' (allowed: iphone, ipad-mini-5, ipad-mini-a17)" ;;
esac

# ── Constants ───────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ_PATH="$SCRIPT_DIR/TRViS/TRViS.csproj"
UITESTS_CSPROJ_PATH="$SCRIPT_DIR/TRViS.UITests/TRViS.UITests.csproj"
APPIUM_URL="http://localhost:4723"
APPIUM_PID=""

cleanup() {
  local exit_code=$?
  if [[ -n "$APPIUM_PID" ]]; then
    log "Stopping Appium (PID $APPIUM_PID)..."
    kill "$APPIUM_PID" 2>/dev/null || true
  fi
  if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" && -n "${DEVICE_ID:-}" ]]; then
    log "Shutting down simulator $DEVICE_ID..."
    xcrun simctl shutdown "$DEVICE_ID" 2>/dev/null || true
  fi
  exit "$exit_code"
}
trap cleanup EXIT

# ── Validate platform ───────────────────────────────────────────
case "$PLATFORM" in
  mac|maccatalyst)
    PLATFORM_VALUE="mac"
    TARGET_FRAMEWORK="net10.0-maccatalyst"
    APPIUM_DRIVER="mac2"
    IS_SIMULATOR=false
    ;;
  ios)
    PLATFORM_VALUE="ios"
    TARGET_FRAMEWORK="net10.0-ios"
    TARGET_RUNTIME="iossimulator-arm64"
    APPIUM_DRIVER="xcuitest"
    IS_SIMULATOR=true
    ;;
  device)
    # Real iOS device (arm64, not simulator)
    PLATFORM_VALUE="ios"
    TARGET_FRAMEWORK="net10.0-ios"
    TARGET_RUNTIME="ios-arm64"
    APPIUM_DRIVER="xcuitest"
    IS_SIMULATOR=false
    # Real device testing requires a valid Apple Developer certificate.
    # Verify one is available before proceeding.
    if ! security find-identity -p codesigning -v 2>/dev/null | grep -q "^[[:space:]]*[1-9][0-9]* valid identit"; then
      die "No valid iOS code-signing identity found.\n" \
          "Real-device testing requires:\n" \
          "  1. An Apple Developer account with a valid certificate in Keychain\n" \
          "  2. A provisioning profile for bundle ID 'dev.t0r.trvis'\n" \
          "  3. The device UDID registered with the provisioning profile\n" \
          "Install a certificate via Xcode > Settings > Accounts."
    fi
    # Resolve device UDID
    if [[ -n "$DEVICE_UDID_OVERRIDE" ]]; then
      DEVICE_ID="$DEVICE_UDID_OVERRIDE"
    else
      DEVICE_ID=$(xcrun xctrace list devices 2>&1 \
        | grep -v "(Simulator)" \
        | grep -oE '[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{40}' \
        | head -1)
    fi
    [[ -n "$DEVICE_ID" ]] || die "No connected iOS device found. Connect a device via USB and unlock it."
    log "Real device UDID: $DEVICE_ID"
    ;;
  *)
    die "Unsupported platform: '$PLATFORM'. Supported: mac, ios"
    ;;
esac

log "Platform: $PLATFORM_VALUE | Framework: $TARGET_FRAMEWORK"

# ── Select and pre-boot iOS Simulator (before build) ───────────
# Starting the boot early lets it overlap with the app build, reducing
# wall-clock time when the runtime is already cached on the runner.
# Logging runtimes here also helps diagnose slow-boot issues in CI.
if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" ]]; then
  if ! command -v jq >/dev/null 2>&1; then
    die "'jq' is required to select an iOS simulator. Install it with: brew install jq"
  fi

  # Xcode 26+ may not have the iOS simulator runtime pre-installed.
  # Download it if no iOS runtimes are available.
  if ! xcrun simctl list runtimes | grep -qi ios; then
    log "No iOS simulator runtime found. Downloading (this may take several minutes)..."
    xcodebuild -downloadPlatform iOS
    log "Download complete."
  fi

  log "Selecting iOS simulator (class: $DEVICE_CLASS)..."

  # Map device class -> simctl device-type identifier prefix and a friendly name regex.
  # 5th-gen iPad mini caps at iPadOS 17, so it requires an iOS 17 runtime to be installed.
  case "$DEVICE_CLASS" in
    iphone)
      SIM_DEVICE_TYPE="com.apple.CoreSimulator.SimDeviceType.iPhone-16"
      SIM_NAME_REGEX="^iPhone 16$"
      SIM_DEVICE_NAME="iPhone 16"
      ;;
    ipad-mini-5)
      SIM_DEVICE_TYPE="com.apple.CoreSimulator.SimDeviceType.iPad-mini--5th-generation-"
      SIM_NAME_REGEX="^iPad mini \(5th generation\)$"
      SIM_DEVICE_NAME="iPad mini (5th generation)"
      ;;
    ipad-mini-a17)
      SIM_DEVICE_TYPE="com.apple.CoreSimulator.SimDeviceType.iPad-mini-A17-Pro"
      SIM_NAME_REGEX="^iPad mini \(A17 Pro\)$"
      SIM_DEVICE_NAME="iPad mini (A17 Pro)"
      ;;
  esac

  # Reuse order:
  #   1) A simulator this script created on a previous run, named
  #      "trvis-<device-class>". Checked first so repeated local runs do
  #      not accumulate one new device per invocation — previously the
  #      lookup only matched the default Xcode names (e.g. "iPhone 16")
  #      and silently bypassed every "trvis-*" device the script itself
  #      had created, calling `simctl create` again each time.
  #   2) An Xcode-installed default simulator matching SIM_NAME_REGEX
  #      (the previous behavior; preserved so fresh machines without
  #      any "trvis-*" device keep working as they did before).
  # If neither exists, create a new "trvis-<device-class>".
  SIM_REUSE_NAME="trvis-$DEVICE_CLASS"
  # Track which simulator name actually matched so reuse vs create vs default-
  # name fallback is obvious in the log. SIM_NAME_REGEX is an anchored exact
  # match for SIM_DEVICE_NAME above, so the path-2 match implies that name.
  FOUND_SIM_NAME=""
  DEVICE_ID=$(xcrun simctl list devices available --json \
    | jq -r --arg name "$SIM_REUSE_NAME" \
        '.devices | to_entries[] | select(.key | contains("iOS") or contains("iPadOS")) | .value[] | select(.name == $name) | .udid' \
    | head -1)
  if [[ -n "$DEVICE_ID" && "$DEVICE_ID" != "null" ]]; then
    FOUND_SIM_NAME="$SIM_REUSE_NAME"
  fi

  if [[ -z "$DEVICE_ID" || "$DEVICE_ID" == "null" ]]; then
    DEVICE_ID=$(xcrun simctl list devices available --json \
      | jq -r --arg pat "$SIM_NAME_REGEX" \
          '.devices | to_entries[] | select(.key | contains("iOS") or contains("iPadOS")) | .value[] | select(.name | test($pat)) | .udid' \
      | head -1)
    if [[ -n "$DEVICE_ID" && "$DEVICE_ID" != "null" ]]; then
      FOUND_SIM_NAME="$SIM_DEVICE_NAME"
    fi
  fi

  if [[ -z "$DEVICE_ID" || "$DEVICE_ID" == "null" ]]; then
    log "No existing simulator for '$SIM_DEVICE_NAME' — creating '$SIM_REUSE_NAME'."
    # Pick the highest available iOS runtime first (works for iPhone 16, iPad mini 6, iPad mini A17).
    SIM_RUNTIME=$(xcrun simctl list runtimes --json \
      | jq -r '[.runtimes[] | select(.platform == "iOS" or (.identifier | contains("iOS")))] | sort_by(.version) | reverse | .[0].identifier')
    if [[ -z "$SIM_RUNTIME" || "$SIM_RUNTIME" == "null" ]]; then
      die "No iOS runtime available to create simulator '$SIM_DEVICE_NAME'."
    fi
    log "Creating simulator '$SIM_REUSE_NAME' on runtime '$SIM_RUNTIME'..."
    if ! DEVICE_ID=$(xcrun simctl create "$SIM_REUSE_NAME" "$SIM_DEVICE_TYPE" "$SIM_RUNTIME" 2>&1); then
      log "create failed: $DEVICE_ID"
      die "Failed to create simulator for $DEVICE_CLASS. The device type may require an older iOS runtime that is not installed (e.g. iPad mini 5th gen → iOS 17)."
    fi
    FOUND_SIM_NAME="$SIM_REUSE_NAME"
    log "Created simulator: $DEVICE_ID ($FOUND_SIM_NAME)"
  else
    log "Reusing existing simulator: $DEVICE_ID ($FOUND_SIM_NAME)"
  fi

  [[ -n "$DEVICE_ID" && "$DEVICE_ID" != "null" ]] || die "No available iOS simulator found"
  log "Selected simulator: $DEVICE_ID ($FOUND_SIM_NAME)"

  log "Available simulator runtimes (for diagnostics):"
  xcrun simctl list runtimes | grep -i ios || log "(no iOS runtimes listed)"

  log "Booting simulator: $DEVICE_ID"
  BOOT_OUTPUT=$(xcrun simctl boot "$DEVICE_ID" 2>&1)
  BOOT_EXIT=$?
  if [[ $BOOT_EXIT -ne 0 ]]; then
    # Non-zero is expected if the device is already in Booted state.
    log "xcrun simctl boot exited $BOOT_EXIT: $BOOT_OUTPUT"
  fi
fi

# ── Ensure GoogleService-Info.plist exists ──────────────────────
PLIST_PATH="$SCRIPT_DIR/TRViS/Platforms/iOS/GoogleService-Info.plist"
if [[ ! -f "$PLIST_PATH" ]]; then
  log "Creating placeholder GoogleService-Info.plist..."
  cat > "$PLIST_PATH" << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CLIENT_ID</key>
  <string>000000000000-placeholder.apps.googleusercontent.com</string>
  <key>REVERSED_CLIENT_ID</key>
  <string>com.googleusercontent.apps.000000000000-placeholder</string>
  <key>API_KEY</key>
  <string>placeholder_api_key</string>
  <key>GCM_SENDER_ID</key>
  <string>000000000000</string>
  <key>PLIST_VERSION</key>
  <string>1</string>
  <key>BUNDLE_ID</key>
  <string>dev.t0r.trvis</string>
  <key>PROJECT_ID</key>
  <string>placeholder-project-id</string>
  <key>STORAGE_BUCKET</key>
  <string>placeholder-project-id.appspot.com</string>
  <key>IS_ADS_ENABLED</key>
  <false/>
  <key>IS_ANALYTICS_ENABLED</key>
  <false/>
  <key>IS_GCM_ENABLED</key>
  <true/>
  <key>IS_SIGNIN_ENABLED</key>
  <true/>
  <key>GOOGLE_APP_ID</key>
  <string>1:000000000000:ios:0000000000000000</string>
</dict>
</plist>
EOF
fi

# ── Build ───────────────────────────────────────────────────────
if [[ "$SKIP_BUILD" == true ]]; then
  log "Skipping build (--skip-build specified)"
else
  log "Building app for $TARGET_FRAMEWORK..."
  if [[ "$PLATFORM_VALUE" == "ios" && "$IS_SIMULATOR" == true ]]; then
    # Simulator: ad-hoc signing is sufficient
    dotnet build "$CSPROJ_PATH" \
      -f "$TARGET_FRAMEWORK" \
      -r "$TARGET_RUNTIME" \
      -c Debug \
      /p:DefineConstants="DISABLE_FIREBASE%3BUI_TEST" \
      /p:_RequireCodeSigning=false \
      /p:CodesignKey="-"
  elif [[ "$PLATFORM_VALUE" == "ios" && "$IS_SIMULATOR" == false ]]; then
    # Real device: use the developer certificate from Keychain (no overrides)
    dotnet build "$CSPROJ_PATH" \
      -f "$TARGET_FRAMEWORK" \
      -r "$TARGET_RUNTIME" \
      -c Debug \
      /p:DefineConstants="DISABLE_FIREBASE%3BUI_TEST"
  else
    dotnet build "$CSPROJ_PATH" \
      -f "$TARGET_FRAMEWORK" \
      -c Debug \
      /p:DefineConstants="DISABLE_FIREBASE%3BUI_TEST"
  fi
  log "Build complete."
fi

# ── Find app ─────────────────────────────────────────────────────
if [[ "$PLATFORM_VALUE" == "ios" ]]; then
  APP_PATH=$(find "$SCRIPT_DIR/TRViS/bin/Debug/$TARGET_FRAMEWORK/$TARGET_RUNTIME" \
    -name "*.app" -type d 2>/dev/null | head -1)
else
  APP_PATH=$(find "$SCRIPT_DIR/TRViS/bin/Debug/$TARGET_FRAMEWORK" \
    -name "*.app" -type d 2>/dev/null | head -1)
fi

[[ -n "$APP_PATH" ]] || die "Could not find built .app under TRViS/bin/Debug/$TARGET_FRAMEWORK"
log "App: $APP_PATH"

# ── Re-sign iOS Simulator bundle ───────────────────────────────
# iOS 26.3+ simulator requires all binaries in the bundle to share the same
# signing identity. .NET runtime dylibs are signed by Microsoft (UBF8T346G9)
# but the app shell is ad-hoc; re-signing everything with "-" fixes the mismatch.
# (Real devices must use proper developer signing; skip this step for them.)
if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" ]]; then
  log "Re-signing all bundle binaries with ad-hoc identity..."
  find "$APP_PATH" -type f \( -name "*.dylib" -o -name "*.so" \) | while read -r lib; do
    codesign --force --sign - "$lib" 2>/dev/null || true
  done
  codesign --force --sign - "$APP_PATH" 2>/dev/null || true
  log "Re-signing complete."
fi

# ── Wait for iOS Simulator boot ────────────────────────────────
# Boot was started before the build to overlap boot time with compile time.
# Do NOT erase the simulator: a full erase forces an OS-image reinstall
# that takes >10 min in CI. The app is pre-installed once (below) and
# AppiumConfig uses noReset:true so the xcuitest driver only terminates
# and relaunches the app between sessions instead of uninstalling it.
# Per-test app state is reset in BaseUITest.ResetAppState via simctl.
#
# NOTE: `xcrun simctl bootstatus -b` can hang indefinitely in some CI
# environments (e.g., GitHub Actions macos-26 runners with Xcode 26).
# Instead, we poll the simulator state directly for robustness.
if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" ]]; then
  log "Waiting for simulator to finish booting (up to 20 minutes)..."
  MAX_WAIT=1200  # 20 minutes
  POLL_INTERVAL=5
  ELAPSED=0

  while [[ $ELAPSED -lt $MAX_WAIT ]]; do
    # Check the simulator state via simctl list
    SIM_STATE=$(xcrun simctl list devices -j 2>/dev/null \
      | jq -r --arg udid "$DEVICE_ID" \
          '.devices | to_entries[] | .value[] | select(.udid == $udid) | .state' \
      2>/dev/null || echo "Unknown")

    if [[ "$SIM_STATE" == "Booted" ]]; then
      log "Simulator is booted (state: $SIM_STATE)"
      break
    fi

    log "Simulator state: $SIM_STATE (elapsed: ${ELAPSED}s, waiting...)"
    sleep $POLL_INTERVAL
    ELAPSED=$((ELAPSED + POLL_INTERVAL))
  done

  if [[ "$SIM_STATE" != "Booted" ]]; then
    # Dump simulator list for diagnostics
    log "Simulator state after ${ELAPSED}s: $SIM_STATE"
    xcrun simctl list devices | grep -A2 -B2 "$DEVICE_ID" || true
    die "Simulator $DEVICE_ID failed to boot within 20 minutes (state: $SIM_STATE)"
  fi

  # ── Freeze the iOS status bar for screenshot regression ──────────
  # ScreenshotRegressionTests captures Driver.GetScreenshot(), which on
  # iOS includes the device status bar. Without an override the clock /
  # battery / signal pixels change every run and every baseline diff
  # fails. Pin them to Apple's marketing values (time 9:41, full battery,
  # full wifi). Applied once after boot and before the first Appium
  # session — the override sticks until the simulator shuts down, so it
  # does not need re-applying per test. Best-effort: a failure here only
  # affects the screenshot fixture, not the functional suite.
  log "Applying status-bar override (time 9:41, full battery/wifi)..."
  # Plain "9:41" (Apple marketing time). An ISO string with a timezone
  # offset is rejected by simctl on Xcode 26 ("Invalid, non-ISO
  # date/time string"); the bare HH:MM form is what reliably sets the
  # status-bar clock without also trying to set the device date.
  xcrun simctl status_bar "$DEVICE_ID" override \
    --time "9:41" \
    --dataNetwork wifi \
    --wifiMode active \
    --wifiBars 3 \
    --cellularMode active \
    --cellularBars 4 \
    --batteryState charged \
    --batteryLevel 100 \
    || log "WARN: status_bar override failed (screenshot baselines may be flaky)."
fi

# ── Pre-install iOS app on simulator ───────────────────────────
# Register the app with FrontBoard once before Appium starts.
# AppiumConfig uses noReset:true so the xcuitest driver will not
# uninstall/reinstall the app between sessions — it only terminates and
# relaunches. This avoids the repeated uninstall/reinstall cycle that
# leaves FrontBoard in an inconsistent state and causes
# "Application is unknown to FrontBoard" session-creation failures.
# Per-test app state (NSUserDefaults) is cleared by BaseUITest.ResetAppState.
if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" ]]; then
  log "Pre-installing app on simulator $DEVICE_ID..."
  xcrun simctl install "$DEVICE_ID" "$APP_PATH"
  log "App pre-installed."
fi

# ── Pre-build WebDriverAgent for usePrebuiltWDA ─────────────────
# Without this, Appium's xcuitest driver invokes xcodebuild once per
# session to verify WDA is built — ~28 s per session even when the
# binary is already cached. Pre-building into a known derivedDataPath
# and passing `usePrebuiltWDA: true` skips that check entirely (article
# reports 37 s → 9 s per session on iOS Simulator).
#
# Use `generic/platform=iOS Simulator` so the binary is reusable across
# simulator UDIDs — no need to rebuild when the matrix swaps device class.
WDA_DERIVED_DATA=""
if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" ]]; then
  WDA_DERIVED_DATA="$HOME/Library/Developer/Xcode/DerivedData/trvis-wda-prebuilt"
  WDA_PROJ="$HOME/.appium/node_modules/appium-xcuitest-driver/node_modules/appium-webdriveragent/WebDriverAgent.xcodeproj"
  if [[ ! -d "$WDA_PROJ" ]]; then
    log "WDA project not found at $WDA_PROJ — skipping pre-build (usePrebuiltWDA disabled)."
    WDA_DERIVED_DATA=""
  else
    log "Pre-building WebDriverAgent into $WDA_DERIVED_DATA..."
    # `build-for-testing` produces the WDA Runner app plus the xctestrun
    # bundle that Appium's usePrebuiltWDA path consumes. xcodebuild is
    # incremental — subsequent invocations after the first complete in
    # seconds when sources haven't changed.
    if xcodebuild build-for-testing \
        -project "$WDA_PROJ" \
        -scheme WebDriverAgentRunner \
        -destination 'generic/platform=iOS Simulator' \
        -derivedDataPath "$WDA_DERIVED_DATA" \
        CODE_SIGNING_ALLOWED=NO \
        >/tmp/wda-prebuild.log 2>&1; then
      log "WDA pre-built. derivedDataPath=$WDA_DERIVED_DATA"
    else
      log "WDA pre-build failed — tail of log:"
      tail -30 /tmp/wda-prebuild.log || true
      log "Continuing without usePrebuiltWDA (Appium will build WDA on first session)."
      WDA_DERIVED_DATA=""
    fi
  fi
fi

# ── Install Mac Catalyst app ────────────────────────────────────
# The mac2 / WDA driver uses XCUIApplication(bundleId:) which requires the app
# to be registered with Launch Services.  A .app built by dotnet but never run
# is invisible to the system; install it to ~/Applications/ first.
if [[ "$PLATFORM_VALUE" == "mac" ]]; then
  log "Installing Mac Catalyst app to ~/Applications/..."
  mkdir -p "$HOME/Applications"
  rm -rf "$HOME/Applications/TRViS.app"
  cp -r "$APP_PATH" "$HOME/Applications/TRViS.app"
  # Strip quarantine so Gatekeeper does not block unsigned CI builds
  xattr -rd com.apple.quarantine "$HOME/Applications/TRViS.app" 2>/dev/null || true
  # Register with Launch Services: run the app briefly so the OS records the bundle.
  # mac2 / WDA uses XCUIApplication(bundleId:) which requires system registration.
  open "$HOME/Applications/TRViS.app" 2>/dev/null || true
  sleep 3
  pkill -f "dev.t0r.trvis" 2>/dev/null || true
  sleep 1
  # Point subsequent steps at the installed copy
  APP_PATH="$HOME/Applications/TRViS.app"
  log "App installed at: $APP_PATH"
fi

# ── Reset app data (ensure Firebase consent page appears) ──────
if [[ "$PLATFORM_VALUE" == "mac" ]]; then
  log "Resetting app data for a clean test run..."
  # Kill any running TRViS instance first
  pkill -f "dev.t0r.trvis" 2>/dev/null || true
  sleep 1
  # Use 'defaults delete' to properly clear NSUserDefaults (includes daemon cache flush)
  defaults delete dev.t0r.trvis 2>/dev/null || true
  log "Cleared NSUserDefaults for dev.t0r.trvis"
  # Also remove saved window state so the app opens at default (full) size
  SAVED_STATE_DIR="$HOME/Library/Containers/dev.t0r.trvis/Data/Library/Saved Application State/dev.t0r.trvis~iosmac.savedState"
  if [[ -d "$SAVED_STATE_DIR" ]]; then
    rm -rf "$SAVED_STATE_DIR"
    log "Deleted saved application state"
  fi
fi

# ── Appium setup ────────────────────────────────────────────────
if ! command -v appium &>/dev/null; then
  die "Appium not found. Install it with: npm install -g appium"
fi

if [[ "$SKIP_INSTALL" == true ]]; then
  log "Skipping Appium driver install (--skip-install specified)"
else
  log "Installing Appium driver: $APPIUM_DRIVER..."
  appium driver install "$APPIUM_DRIVER" 2>/dev/null || \
    log "Driver already installed or install skipped (this is OK)"
fi

# ── Start Appium server ─────────────────────────────────────────
# A previous run that died uncleanly can leave an orphaned Appium holding
# port 4723. The fresh `appium &` below would then fail with EADDRINUSE,
# but the readiness probe would still pass (it talks to the zombie), and
# the zombie gets SIGTERM'd mid-test → "session is either terminated or
# not started" failures. Free the port before binding.
APPIUM_PORT="${APPIUM_URL##*:}"
STALE_APPIUM_PIDS="$(lsof -nP -tiTCP:"$APPIUM_PORT" -sTCP:LISTEN 2>/dev/null || true)"
if [[ -n "$STALE_APPIUM_PIDS" ]]; then
  log "Port $APPIUM_PORT busy (PIDs: $(echo "$STALE_APPIUM_PIDS" | tr '\n' ' ')) — killing stale Appium..."
  # shellcheck disable=SC2086
  kill $STALE_APPIUM_PIDS 2>/dev/null || true
  for _ in {1..10}; do
    lsof -nP -tiTCP:"$APPIUM_PORT" -sTCP:LISTEN >/dev/null 2>&1 || break
    sleep 1
  done
  # shellcheck disable=SC2046
  kill -9 $(lsof -nP -tiTCP:"$APPIUM_PORT" -sTCP:LISTEN 2>/dev/null) 2>/dev/null || true
fi

log "Starting Appium server..."
appium &
APPIUM_PID=$!
log "Appium PID: $APPIUM_PID"

# Wait for Appium to be ready
log "Waiting for Appium server to start..."
for i in {1..30}; do
  if curl -s "$APPIUM_URL/status" > /dev/null 2>&1; then
    log "Appium server is ready!"
    break
  fi
  if [[ $i -eq 30 ]]; then
    err "Appium server did not start within 30 seconds"
    exit 1
  fi
  sleep 1
done

# ── Run tests ───────────────────────────────────────────────────
# Distinct file per device class so iPad/iPhone matrix runs don't overwrite each other.
if [[ "$PLATFORM_VALUE" == "ios" && "$IS_SIMULATOR" == true ]]; then
  LOG_FILE="${PLATFORM_VALUE}-${DEVICE_CLASS_SUFFIX}-results.trx"
else
  LOG_FILE="${PLATFORM_VALUE}-results.trx"
fi
log "Running UI tests... (results -> $LOG_FILE)"

# Build the dotnet test argument list incrementally to avoid empty-array
# expansion issues with 'set -u' in bash 4.4+ (unbound variable for [@]).
DOTNET_TEST_ARGS=(
  "$UITESTS_CSPROJ_PATH"
  --configuration Debug
  --logger "trx;LogFileName=$LOG_FILE"
)
if [[ -n "$TEST_FILTER" ]]; then
  log "Applying test filter: $TEST_FILTER"
  DOTNET_TEST_ARGS+=(--filter "$TEST_FILTER")
fi
DOTNET_TEST_ARGS+=(
  --
  "TestRunParameters.Parameter(name=\"platform\",value=\"$PLATFORM_VALUE\")"
  "TestRunParameters.Parameter(name=\"appPath\",value=\"$APP_PATH\")"
  "TestRunParameters.Parameter(name=\"appiumUrl\",value=\"$APPIUM_URL\")"
  # Selects the screenshot-regression baseline directory
  # (TRViS.UITests/Screenshots/<deviceClass>/...) and the pixel-diff gate.
  "TestRunParameters.Parameter(name=\"deviceClass\",value=\"$DEVICE_CLASS\")"
)
if [[ -n "${DEVICE_ID:-}" ]]; then
  DOTNET_TEST_ARGS+=("TestRunParameters.Parameter(name=\"deviceUdid\",value=\"$DEVICE_ID\")")
fi
if [[ -n "${WDA_DERIVED_DATA:-}" ]]; then
  DOTNET_TEST_ARGS+=("TestRunParameters.Parameter(name=\"wdaDerivedDataPath\",value=\"$WDA_DERIVED_DATA\")")
fi

# Run tests with timeout (40 minutes = 2400 seconds).
# iOS on macos-26: iPhone simulator cold start ~7 min + WDA install on first
# session + ~30 tests × ~60 s/session (noReset:true creates a new session per
# test) ≈ 37–40 min on iPhone, ~25 min on iPad mini A17. Older comment said
# "6 tests × 60 s" — stale; the suite has grown since then.
# Android: ~30 tests × ~60 s ≈ 30 min with the OOM-resilient AVD config.
timeout 2400 dotnet test "${DOTNET_TEST_ARGS[@]}"

log "All tests passed!"
