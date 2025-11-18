#!/bin/bash

cd `dirname $0`

TARGET_PROJ="TRViS/TRViS.csproj"
TARGET_FRAMEWORK="net10.0-ios"

UDID_PATTERN="[[:alnum:]]{8}-[[:alnum:]]{4}-[[:alnum:]]{4}-[[:alnum:]]{4}-[[:alnum:]]{12}"

ARGS=($@)

SCRIPT_NAME=`basename $0`

if [ "$1" = "-h" ]; then
  cat << END
iOS Simulator Launcher
	Usage: $SCRIPT_NAME [UDID/DeviceName] [dotnet command options]

If you don't specify UDID/DeviceName, you can select Device from available device list.

dotnet command will executed like below ...
	dotnet build -t:Run $TARGET_PROJ -f $TARGET_FRAMEWORK -r iossimulator-x64 --no-self-contained --nologo "/p:_DeviceName=[DeviceName|:v2:udid=UDID]" [dotnet command options]

---

Exec Example: Run Release build on the simulator (UDID=12345678-1234-1234-1234-123456789ABC)
	$SCRIPT_NAME 12345678-1234-1234-1234-123456789ABC -c Release
Exec Example: Run Release build on the Device (DeviceName=Sample_iPad)
	$SCRIPT_NAME Sample_iPad -c Release

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
      DeviceArr[$COUNTER]=":v2:udid=$_UDID"
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

  echo "Please select the device you want to launch App on."
  INPUT_MAX=`expr $ARRLEN - 1`; read -p "0 ~ $INPUT_MAX [0]: " answer

  if [ -z "$answer" ]; then
    answer=0
  fi

  IS_NUM_PATTERN='^[0-9]+$'
  if ! [[ $answer =~ $IS_NUM_PATTERN ]] || [ ! \( 0 -le $answer -a $answer -lt $ARRLEN \) ]; then
    echo "Invalid Selection ($answer)" 1>&2
    exit 1
  fi

  DeviceName="${DeviceArr[$answer]}"
else
  ARG_UDID=`echo "$1" | grep -oE "$UDID_PATTERN"`
  if [ -z "$ARG_UDID" ]; then
    DeviceName=$1
  else
    DeviceName=":v2:udid=$ARG_UDID"
  fi
  ARGS[0]=""
fi

UDID=`echo "$DeviceName" | grep -oE "$UDID_PATTERN"`
if [ -z "$UDID" ]; then
  echo "... DeviceName: $DeviceName (Run on Physical Device)"
  RUNTIME_IDENTIFIER='ios-arm64'
else
  echo "... UDID: $UDID (Run on Simulator)"
  RUNTIME_IDENTIFIER='iossimulator-x64'
fi

if [ ! -z "$DeviceName" ]; then
  dotnet build -t:Run $TARGET_PROJ -f $TARGET_FRAMEWORK -r $RUNTIME_IDENTIFIER --self-contained --nologo "/p:_DeviceName=$DeviceName" ${ARGS[@]}
fi
