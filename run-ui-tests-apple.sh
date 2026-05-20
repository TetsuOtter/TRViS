#!/usr/bin/env bash
# ================================================================
# TRViS XCUITest Local Runner  (Phase 1 — AppLaunchTests)
#
# Usage: ./run-ui-tests-apple.sh [platform] [options]
#   platform: ios       – iOS Simulator XCUITest (default)
#             catalyst  – Mac Catalyst XCUITest
#             all       – Both platforms in sequence
#
#   options:
#     --skip-build          Skip dotnet build of the MAUI app
#     --skip-install        Skip xcrun simctl install (iOS only)
#     --sim-udid=<UDID>     Use a specific simulator UDID (iOS only);
#                           defaults to a booted sim named
#                           'xcuitest-poc-iPhone16'
#
# Prerequisites (macOS):
#   brew install xcodegen
#   xcode-select --install  (Command Line Tools)
#   Xcode 15+ installed and active via xcode-select
#
# iOS: the MAUI debug build for iossimulator-arm64 is installed into the
#   booted simulator and XCUITest drives it via xctest bundle.
#
# Catalyst: the MAUI debug build is copied to ~/Applications/TRViS.app,
#   quarantine bits are stripped, and LaunchServices is asked to register
#   it before xcodebuild runs the xctest bundle.
#
# KNOWN ISSUE (Catalyst, local machine only):
#   If /Applications/TRViS.app (App Store build) is installed alongside the
#   debug build at ~/Applications/TRViS.app, LaunchServices resolves
#   dev.t0r.trvis to the App Store copy.  testmanagerd then launches the
#   wrong binary and the xctest runner hangs for 332 s before SIGTERM.
#   CI runners are not affected (no App Store TRViS installed).
#   Local workaround: sudo rm -rf /Applications/TRViS.app  (or move it out).
# ================================================================

set -euo pipefail

# ── Helpers ──────────────────────────────────────────────────────
log()  { printf '[%s] %b\n' "$(date '+%H:%M:%S')" "$*"; }
err()  { printf '[%s] ERROR: %b\n' "$(date '+%H:%M:%S')" "$*" >&2; }
die()  { err "$*"; exit 1; }

# ── Defaults ────────────────────────────────────────────────────
PLATFORM="${1:-ios}"
SKIP_BUILD=false
SKIP_INSTALL=false
SIM_UDID_OVERRIDE=""

for arg in "$@"; do
  case "$arg" in
    --skip-build)     SKIP_BUILD=true ;;
    --skip-install)   SKIP_INSTALL=true ;;
    --sim-udid=*)     SIM_UDID_OVERRIDE="${arg#*=}" ;;
  esac
done

case "$PLATFORM" in
  ios|catalyst|all) ;;
  *)  die "Unknown platform '$PLATFORM' (allowed: ios, catalyst, all)" ;;
esac

# ── Paths ───────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ_PATH="$SCRIPT_DIR/TRViS/TRViS.csproj"
XCUITEST_DIR="$SCRIPT_DIR/TRViS.UITests.Apple"
IOS_PROJ_DIR="$XCUITEST_DIR/ios"
CATALYST_PROJ_DIR="$XCUITEST_DIR/catalyst"

# App bundle ID under test (must match what BaseUITestCase.swift uses)
APP_BUNDLE_ID="dev.t0r.trvis"

# MAUI UI_TEST defines: activates in-app test seed buttons + disables Firebase
UI_TEST_DEFINES="DISABLE_FIREBASE%3BUI_TEST"

# ── Prerequisite checks ─────────────────────────────────────────
check_prereqs() {
  local missing=()
  command -v xcodegen  >/dev/null 2>&1 || missing+=("xcodegen  (brew install xcodegen)")
  command -v xcodebuild >/dev/null 2>&1 || missing+=("xcodebuild  (install Xcode via App Store or xcode-select)")
  command -v xcrun     >/dev/null 2>&1 || missing+=("xcrun  (install Xcode Command Line Tools)")
  if [[ ${#missing[@]} -gt 0 ]]; then
    err "Missing prerequisites:"
    for m in "${missing[@]}"; do err "  - $m"; done
    exit 1
  fi
}

# ── GoogleService-Info.plist placeholder ────────────────────────
ensure_google_plist() {
  local plist="$SCRIPT_DIR/TRViS/GoogleService-Info.plist"
  if [[ ! -f "$plist" ]]; then
    log "Creating placeholder GoogleService-Info.plist"
    cat > "$plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CLIENT_ID</key>      <string>000000000000-placeholder.apps.googleusercontent.com</string>
  <key>REVERSED_CLIENT_ID</key><string>com.googleusercontent.apps.000000000000-placeholder</string>
  <key>API_KEY</key>        <string>placeholder_api_key</string>
  <key>GCM_SENDER_ID</key> <string>000000000000</string>
  <key>PLIST_VERSION</key> <string>1</string>
  <key>BUNDLE_ID</key>     <string>dev.t0r.trvis</string>
  <key>PROJECT_ID</key>    <string>placeholder-project-id</string>
  <key>STORAGE_BUCKET</key><string>placeholder-project-id.appspot.com</string>
  <key>IS_ADS_ENABLED</key>    <false/>
  <key>IS_ANALYTICS_ENABLED</key><false/>
  <key>IS_GCM_ENABLED</key>   <true/>
  <key>IS_SIGNIN_ENABLED</key><true/>
  <key>GOOGLE_APP_ID</key><string>1:000000000000:ios:0000000000000000</string>
</dict>
</plist>
PLIST
  fi
}

# ── xcodegen helper ─────────────────────────────────────────────
generate_xcodeproj() {
  local proj_dir="$1"
  log "Running xcodegen in $proj_dir"
  (cd "$proj_dir" && xcodegen generate --quiet)
}

# ================================================================
# iOS section
# ================================================================
run_ios() {
  log "=== iOS XCUITest ==="

  # ── 1. Build MAUI iOS Simulator app ──────────────────────────
  if [[ "$SKIP_BUILD" == true ]]; then
    log "Skipping iOS MAUI build (--skip-build)"
  else
    ensure_google_plist
    log "Building MAUI app for iossimulator-arm64 …"
    dotnet build "$CSPROJ_PATH" \
      -f net10.0-ios \
      -r iossimulator-arm64 \
      -c Debug \
      /p:DefineConstants="$UI_TEST_DEFINES" \
      /p:_RequireCodeSigning=false \
      /p:CodesignKey="-"
  fi

  # ── 2. Locate the built .app ──────────────────────────────────
  APP_PATH=$(find "$SCRIPT_DIR/TRViS/bin/Debug/net10.0-ios/iossimulator-arm64" \
    -maxdepth 1 -name "*.app" -type d 2>/dev/null | head -1)
  if [[ -z "$APP_PATH" ]]; then
    die "No .app found under TRViS/bin/Debug/net10.0-ios/iossimulator-arm64"
  fi
  log "App: $APP_PATH"

  # ── 3. Select / boot simulator ───────────────────────────────
  if [[ -n "$SIM_UDID_OVERRIDE" ]]; then
    SIM_UDID="$SIM_UDID_OVERRIDE"
    log "Using specified simulator UDID: $SIM_UDID"
  else
    # Try to find a booted simulator named 'xcuitest-poc-iPhone16';
    # fall back to any booted iPhone simulator.
    SIM_UDID=$(xcrun simctl list devices booted -j 2>/dev/null \
      | python3 -c "
import json,sys
d=json.load(sys.stdin)
for rt,devs in d.get('devices',{}).items():
  for dev in devs:
    if dev.get('state')=='Booted' and 'xcuitest-poc-iPhone16' in dev.get('name',''):
      print(dev['udid']); sys.exit(0)
# fallback: any booted iPhone
for rt,devs in d.get('devices',{}).items():
  for dev in devs:
    if dev.get('state')=='Booted' and 'iPhone' in dev.get('name',''):
      print(dev['udid']); sys.exit(0)
" 2>/dev/null || true)

    if [[ -z "$SIM_UDID" ]]; then
      log "No booted iPhone simulator found; creating and booting xcuitest-poc-iPhone16"
      # Use the latest available iOS runtime
      RUNTIME=$(xcrun simctl list runtimes -j \
        | python3 -c "
import json,sys
rts=[r for r in json.load(sys.stdin).get('runtimes',[]) if 'iOS' in r.get('name','') and r.get('isAvailable',False)]
rts.sort(key=lambda r: r.get('version',''))
print(rts[-1]['identifier']) if rts else sys.exit(1)
")
      SIM_UDID=$(xcrun simctl create "xcuitest-poc-iPhone16" \
        "com.apple.CoreSimulator.SimDeviceType.iPhone-16" \
        "$RUNTIME")
      xcrun simctl boot "$SIM_UDID"
      log "Created and booted simulator: $SIM_UDID"
    fi
  fi

  log "Simulator UDID: $SIM_UDID"

  # ── 4. Ad-hoc re-sign all executables in the bundle ──────────
  # iOS 26 simulators enforce code signing on every dylib/executable.
  log "Ad-hoc re-signing .app bundle …"
  find "$APP_PATH" -type f \( -name "*.dylib" -o -name "*.so" \) \
    -exec codesign --force --sign "-" {} \;
  codesign --force --sign "-" "$APP_PATH"

  # ── 5. Uninstall then install into simulator ─────────────────
  # Uninstall first to wipe NSUserDefaults / the app data container.
  # AppLaunchTests.testApp_Launches_Into_StartHome_With_Privacy_Banner
  # asserts the privacy banner is visible, which requires a clean-install
  # state (no prior NSUserDefaults key from a previous run). A bare
  # `simctl install` on an already-installed app preserves the container.
  if [[ "$SKIP_INSTALL" == true ]]; then
    log "Skipping simctl uninstall+install (--skip-install)"
  else
    log "Uninstalling previous app from simulator $SIM_UDID (wipes data container) …"
    xcrun simctl uninstall "$SIM_UDID" "$APP_BUNDLE_ID" 2>/dev/null || true
    log "Installing app into simulator $SIM_UDID …"
    xcrun simctl install "$SIM_UDID" "$APP_PATH"
  fi

  # ── 6. Generate Xcode project ────────────────────────────────
  generate_xcodeproj "$IOS_PROJ_DIR"

  # ── 7. Run XCUITest ─────────────────────────────────────────
  log "Running XCUITest on simulator $SIM_UDID …"
  xcodebuild test \
    -project "$IOS_PROJ_DIR/TRViSUITests-iOS.xcodeproj" \
    -scheme TRViSUITests_iOS \
    -destination "id=$SIM_UDID" \
    CODE_SIGN_IDENTITY="-" \
    CODE_SIGNING_REQUIRED=NO \
    CODE_SIGNING_ALLOWED=NO \
    | tee /tmp/xcuitest-ios.log

  local exit_code=${PIPESTATUS[0]}
  if [[ $exit_code -eq 0 ]]; then
    log "iOS XCUITest PASSED"
  else
    err "iOS XCUITest FAILED (exit $exit_code); see /tmp/xcuitest-ios.log"
    return $exit_code
  fi
}

# ================================================================
# Catalyst section
# ================================================================
run_catalyst() {
  log "=== Mac Catalyst XCUITest ==="

  # ── KNOWN BLOCKER (local machines with /Applications/TRViS.app) ──
  # testmanagerd drives XCUITest against dev.t0r.trvis by asking
  # LaunchServices to resolve the bundle ID.  If the App Store copy at
  # /Applications/TRViS.app is registered, LS picks it (system path wins),
  # the wrong binary is launched, and the runner hangs for 332 s then gets
  # SIGTERM.  CI runners are clean (no App Store TRViS) so this works there.
  # Local workaround:
  #   sudo rm -rf /Applications/TRViS.app
  # or temporarily rename it, run this script, then restore it.
  # ─────────────────────────────────────────────────────────────────
  if [[ -d "/Applications/TRViS.app" ]]; then
    log "WARNING: /Applications/TRViS.app detected."
    log "  LaunchServices will resolve '$APP_BUNDLE_ID' to the App Store copy,"
    log "  causing the XCUITest runner to hang.  Remove or rename it first:"
    log "    sudo rm -rf /Applications/TRViS.app"
    log "  Continuing anyway (will likely fail on this machine)."
    log ""
  fi

  # ── 1. Build MAUI Mac Catalyst app ──────────────────────────
  if [[ "$SKIP_BUILD" == true ]]; then
    log "Skipping Catalyst MAUI build (--skip-build)"
  else
    ensure_google_plist
    log "Building MAUI app for net10.0-maccatalyst …"
    dotnet build "$CSPROJ_PATH" \
      -f net10.0-maccatalyst \
      -c Debug \
      /p:DefineConstants="$UI_TEST_DEFINES"
  fi

  # ── 2. Locate the built .app ──────────────────────────────────
  APP_PATH=$(find "$SCRIPT_DIR/TRViS/bin/Debug/net10.0-maccatalyst" \
    -maxdepth 2 -name "*.app" -type d 2>/dev/null | head -1)
  if [[ -z "$APP_PATH" ]]; then
    die "No .app found under TRViS/bin/Debug/net10.0-maccatalyst"
  fi
  log "App: $APP_PATH"

  # ── 3. Register with LaunchServices ──────────────────────────
  # Copy to ~/Applications so the user's LS domain can register it.
  # lsregister -f -R -trusted is the critical step that makes
  # testmanagerd able to launch dev.t0r.trvis via the debug build.
  log "Copying to ~/Applications/TRViS.app …"
  rm -rf ~/Applications/TRViS.app
  mkdir -p ~/Applications
  cp -r "$APP_PATH" ~/Applications/TRViS.app

  log "Stripping quarantine from ~/Applications/TRViS.app …"
  xattr -rd com.apple.quarantine ~/Applications/TRViS.app 2>/dev/null || true

  log "Registering with LaunchServices …"
  /System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/LaunchServices.framework/Versions/A/Support/lsregister \
    -f -R -trusted ~/Applications/TRViS.app

  # ── 4. Generate Xcode project ────────────────────────────────
  generate_xcodeproj "$CATALYST_PROJ_DIR"

  # ── 5. Build-for-testing, ad-hoc re-sign Runner, then run ──
  # The XCTRunner template ships with Apple's "Software Signing" certificate;
  # once Xcode injects the Catalyst test bundle the seal breaks and Gatekeeper
  # rejects the runner with "TRViSUITests_Catalyst-Runner.app is damaged".
  # Splitting build/test lets us ad-hoc re-sign between the two phases.
  log "Building Catalyst test bundle …"
  xcodebuild build-for-testing \
    -project "$CATALYST_PROJ_DIR/TRViSUITests-Catalyst.xcodeproj" \
    -scheme TRViSUITests_Catalyst \
    -destination "platform=macOS,arch=arm64" \
    -derivedDataPath "$CATALYST_PROJ_DIR/build" \
    CODE_SIGN_IDENTITY="-" \
    CODE_SIGNING_REQUIRED=NO \
    CODE_SIGNING_ALLOWED=NO \
    | tee /tmp/xcuitest-catalyst-build.log

  local build_exit=${PIPESTATUS[0]}
  if [[ $build_exit -ne 0 ]]; then
    err "Catalyst build-for-testing FAILED (exit $build_exit)"
    return $build_exit
  fi

  local RUNNER_APP
  RUNNER_APP=$(find "$CATALYST_PROJ_DIR/build/Build/Products" \
                 -name "TRViSUITests_Catalyst-Runner.app" -type d | head -1)
  if [[ -n "$RUNNER_APP" ]]; then
    log "Ad-hoc re-signing Runner at $RUNNER_APP"
    xattr -dr com.apple.quarantine "$RUNNER_APP" 2>/dev/null || true
    codesign --force --deep --sign - --timestamp=none "$RUNNER_APP" >/dev/null 2>&1 \
      || err "codesign on Runner failed (continuing — test may still pass)"
  else
    err "Could not locate TRViSUITests_Catalyst-Runner.app under $CATALYST_PROJ_DIR/build"
  fi

  log "Running XCUITest (Mac Catalyst) …"
  xcodebuild test-without-building \
    -project "$CATALYST_PROJ_DIR/TRViSUITests-Catalyst.xcodeproj" \
    -scheme TRViSUITests_Catalyst \
    -destination "platform=macOS,arch=arm64" \
    -derivedDataPath "$CATALYST_PROJ_DIR/build" \
    | tee /tmp/xcuitest-catalyst.log

  local exit_code=${PIPESTATUS[0]}
  if [[ $exit_code -eq 0 ]]; then
    log "Catalyst XCUITest PASSED"
  else
    err "Catalyst XCUITest FAILED (exit $exit_code); see /tmp/xcuitest-catalyst.log"
    return $exit_code
  fi
}

# ── Main ────────────────────────────────────────────────────────
check_prereqs

OVERALL_EXIT=0
case "$PLATFORM" in
  ios)
    run_ios || OVERALL_EXIT=$?
    ;;
  catalyst)
    run_catalyst || OVERALL_EXIT=$?
    ;;
  all)
    run_ios      || OVERALL_EXIT=$?
    run_catalyst || OVERALL_EXIT=$?
    ;;
esac

if [[ $OVERALL_EXIT -ne 0 ]]; then
  err "One or more platforms failed."
fi
exit $OVERALL_EXIT
