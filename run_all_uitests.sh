#!/bin/zsh
set -e

DEVICES_FILE="$(cd "$(dirname "$0")"; pwd)/uitest_devices.txt"
PROJECT_DIR="$(cd "$(dirname "$0")"; pwd)"
UITEST_PROJECT="$PROJECT_DIR/TRViS.UITests/TRViS.UITests.csproj"
APP_PROJECT="$PROJECT_DIR/TRViS/TRViS.csproj"

# Build the app for iOS simulator
cd "$PROJECT_DIR"
dotnet build "$APP_PROJECT" -c Debug -f net10.0-ios


# Start Appium server (if not running)
if ! pgrep -f appium > /dev/null; then
  nohup appium &
  echo "Waiting for Appium server to start..."
  for i in {1..30}; do
    sleep 1
    if lsof -i :4723 | grep LISTEN > /dev/null; then
      echo "Appium server is running."
      break
    fi
    if [[ $i -eq 30 ]]; then
      echo "Appium server did not start within 30 seconds." >&2
      exit 1
    fi
  done
fi

# Run tests for each device
while read device; do
  if [[ -z "$device" ]]; then continue; fi
  echo "Running UI test for: $device"
  UITEST_DEVICE="$device" UITEST_RUNNING=1 dotnet test "$UITEST_PROJECT" --logger "trx;LogFileName=uitest_${device// /_}.trx"
done < "$DEVICES_FILE"
