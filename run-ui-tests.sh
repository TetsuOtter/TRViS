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

# Consume positional device UDID if provided after "device"
if [[ "$PLATFORM" == "device" && -n "${2:-}" && "${2}" != --* ]]; then
  DEVICE_UDID_OVERRIDE="$2"
fi

for arg in "$@"; do
  case "$arg" in
    --skip-build)   SKIP_BUILD=true ;;
    --skip-install) SKIP_INSTALL=true ;;
  esac
done

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
  log "Selecting iOS simulator..."
  DEVICE_ID=$(xcrun simctl list devices available --json \
    | jq -r '.devices | to_entries[] | select(.key | contains("iOS")) | .value[] | select(.name == "iPhone 16") | .udid' \
    | head -1)
  if [[ -z "$DEVICE_ID" ]]; then
    DEVICE_ID=$(xcrun simctl list devices available --json \
      | jq -r '[.devices | to_entries[] | select(.key | contains("iOS")) | .value[]] | .[0] | .udid')
  fi
  [[ -n "$DEVICE_ID" && "$DEVICE_ID" != "null" ]] || die "No available iOS simulator found"
  log "Selected simulator: $DEVICE_ID"

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
      /p:DefineConstants=DISABLE_FIREBASE \
      /p:_RequireCodeSigning=false \
      /p:CodesignKey="-"
  elif [[ "$PLATFORM_VALUE" == "ios" && "$IS_SIMULATOR" == false ]]; then
    # Real device: use the developer certificate from Keychain (no overrides)
    dotnet build "$CSPROJ_PATH" \
      -f "$TARGET_FRAMEWORK" \
      -r "$TARGET_RUNTIME" \
      -c Debug \
      /p:DefineConstants=DISABLE_FIREBASE
  else
    dotnet build "$CSPROJ_PATH" \
      -f "$TARGET_FRAMEWORK" \
      -c Debug \
      /p:DefineConstants=DISABLE_FIREBASE
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
# that takes >10 min in CI. Reinstalling the app per-test is sufficient.
if [[ "$IS_SIMULATOR" == true && "$PLATFORM_VALUE" == "ios" ]]; then
  log "Waiting for simulator to finish booting (up to 20 minutes)..."
  timeout 1200 xcrun simctl bootstatus "$DEVICE_ID" -b \
    || die "Simulator $DEVICE_ID failed to boot within 20 minutes"
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
LOG_FILE="${PLATFORM_VALUE}-results.trx"
log "Running UI tests... (results -> $LOG_FILE)"

# Build the dotnet test argument list incrementally to avoid empty-array
# expansion issues with 'set -u' in bash 4.4+ (unbound variable for [@]).
DOTNET_TEST_ARGS=(
  "$UITESTS_CSPROJ_PATH"
  --configuration Debug
  --logger "trx;LogFileName=$LOG_FILE"
  --
  "TestRunParameters.Parameter(name=\"platform\",value=\"$PLATFORM_VALUE\")"
  "TestRunParameters.Parameter(name=\"appPath\",value=\"$APP_PATH\")"
  "TestRunParameters.Parameter(name=\"appiumUrl\",value=\"$APPIUM_URL\")"
)
if [[ -n "${DEVICE_ID:-}" ]]; then
  DOTNET_TEST_ARGS+=("TestRunParameters.Parameter(name=\"deviceUdid\",value=\"$DEVICE_ID\")")
fi

# Run tests with timeout (30 minutes = 1800 seconds).
# iOS: 6 tests × ~60 s each + ~5 min for WDA installation on first run ≈ 11 min.
# Android: 6 tests × ~120 s each (Mono JIT delay on first launch) ≈ 12 min.
timeout 1800 dotnet test "${DOTNET_TEST_ARGS[@]}"

log "All tests passed!"
