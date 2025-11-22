#!/bin/bash

cd `dirname $0`

TARGET_PROJ="TRViS.iOS.UITests/TRViS.iOS.UITests.xcodeproj"
TARGET_SCHEME="TRViS.iOS.UITests"

UDID_PATTERN="[[:alnum:]]{8}-[[:alnum:]]{4}-[[:alnum:]]{4}-[[:alnum:]]{4}-[[:alnum:]]{12}"

ARGS=($@)

SCRIPT_NAME=`basename $0`

if [ "$1" = "-h" ]; then
  cat << END
iOS UI Test Runner
	Usage: $SCRIPT_NAME [UDID/DeviceName] [xcodebuild options]

If you don't specify UDID/DeviceName, you can select Device from available device list.

xcodebuild test will executed like below ...
	xcodebuild test -project $TARGET_PROJ -scheme $TARGET_SCHEME -destination "platform=iOS Simulator,id=UDID" [xcodebuild options]

---

Exec Example: Run UI tests on the simulator (UDID=12345678-1234-1234-1234-123456789ABC)
	$SCRIPT_NAME 12345678-1234-1234-1234-123456789ABC
Exec Example: Run UI tests on the Device (DeviceName=Sample_iPad)
	$SCRIPT_NAME Sample_iPad

END

  exit 0
fi

if [ -z "$1" ] || [[ $1 = -* ]]; then
  COUNTER=0
  declare -a DeviceArr=()
  while read ListLine
  do
    if [ -z "$ListLine" ] || [[ "$ListLine" = ==*== ]]; then
      echo "$ListLine"
      continue
    fi

    _UDID=`echo "$ListLine" | grep -oE "$UDID_PATTERN"`
    if [ ! -z "$_UDID" ]; then
      echo "[$COUNTER]" "$ListLine"
      DeviceArr[$COUNTER]="$_UDID"
    else
      echo "[$COUNTER]" "$ListLine"
      UnneededPart=`echo "$ListLine" | grep -ioE ' \([0-9\.]+\) \([0-9a-f\-]+\)$'`
      DeviceArr[$COUNTER]=${ListLine%$UnneededPart}
    fi

    COUNTER=`expr $COUNTER + 1`
  done <<< "$(xcrun xctrace list devices)"

  ARRLEN=${#DeviceArr[*]}
  if [ $ARRLEN -eq 0 ]; then
    echo "Error: No Device detected." 1>&2
    exit 0
  fi

  echo "Please select the device you want to run UI tests on."
  INPUT_MAX=`expr $ARRLEN - 1`; read -p "0 ~ $INPUT_MAX [0]: " answer

  if [ -z "$answer" ]; then
    answer=0
  fi

  IS_NUM_PATTERN='^[0-9]+$'
  if ! [[ $answer =~ $IS_NUM_PATTERN ]] || [ ! \( 0 -le $answer -a $answer -lt $ARRLEN \) ]; then
    echo "Invalid Selection ($answer)" 1>&2
    exit 1
  fi

  DeviceID="${DeviceArr[$answer]}"
else
  ARG_UDID=`echo "$1" | grep -oE "$UDID_PATTERN"`
  if [ -z "$ARG_UDID" ]; then
    DeviceID=$1
  else
    DeviceID="$ARG_UDID"
  fi
  ARGS[0]=""
fi

echo "... DeviceID: $DeviceID"

export DEVICE_UDID="$DeviceID"

# Boot simulator if it's a simulator
UDID_CHECK=`echo "$DeviceID" | grep -oE "$UDID_PATTERN"`
if [ ! -z "$UDID_CHECK" ]; then
  echo "Booting simulator..."
  xcrun simctl boot $DeviceID
fi

# Build TRViS app
echo "Building TRViS app..."
dotnet build TRViS/TRViS.csproj -f net9.0-ios -c Debug --no-restore

# Find TRViS app path
APP_PATH=$(find TRViS/bin/Debug/net9.0-ios -name "*.app" | head -n 1)
if [ -z "$APP_PATH" ]; then
  echo "Error: TRViS.app not found after build." 1>&2
  exit 1
fi
echo "... App Path: $APP_PATH"

# Run UI tests
export APP_PATH="$APP_PATH"
xcodebuild test -project $TARGET_PROJ -scheme $TARGET_SCHEME -destination "platform=iOS Simulator,id=$DeviceID" -resultBundlePath "ios-${DeviceID}-ui-test-results.xcresult"

# Shutdown simulator if booted
if [ ! -z "$UDID_CHECK" ]; then
  echo "Shutting down simulator..."
  xcrun simctl shutdown $DeviceID
fi
