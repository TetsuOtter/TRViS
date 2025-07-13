ERROR_DEVICES_FILE="$(cd "$(dirname "$0")"; pwd)/uitest_devices_errors.txt"
> "$UPDATED_DEVICES_FILE"
> "$ERROR_DEVICES_FILE"
cat "$DEVICES_FILE" | while IFS= read -r line; do
  echo "Processing: $line"
  if [[ -z "$line" ]]; then continue; fi
  DISPLAY_NAME="${line%%|*}"
  DEVICETYPE_ID="${line#*|}"
  if [[ -z "$DISPLAY_NAME" || -z "$DEVICETYPE_ID" ]]; then
    echo "    [SKIP] Invalid format: $line"
    echo "$line" >> "$ERROR_DEVICES_FILE"
    continue
  fi
  # Simulatorが存在しない場合は作成
  if ! grep -Fxq "$DEVICETYPE_ID" "$AVAILABLE_DEVICES_FILE"; then
    echo "    [INFO] Creating simulator for $DISPLAY_NAME..."
    if xcrun simctl create "$DISPLAY_NAME" "$DEVICETYPE_ID" "com.apple.CoreSimulator.SimRuntime.iOS-17-5"; then
      echo "    [CREATED] Simulator for $DISPLAY_NAME created."
    else
      echo "    [ERROR] Failed to create simulator for $DISPLAY_NAME."
      echo "$line" >> "$ERROR_DEVICES_FILE"
      continue
    fi
  fi
  echo "$DISPLAY_NAME|$DEVICETYPE_ID" >> "$UPDATED_DEVICES_FILE"
  echo "  [OK] $DISPLAY_NAME ($DEVICETYPE_ID) added to list."
done < "$DEVICES_FILE"
#!/bin/zsh
set -e

echo "[1/4] Getting available iOS Simulator device names..."
AVAILABLE_DEVICES_RAW=$(xcrun simctl list devices)
AVAILABLE_DEVICES_AWK=$(echo "$AVAILABLE_DEVICES_RAW" | awk -F '(' '/\(Shutdown\)|\(Booted\)/ {gsub(/^ +| +$/, "", $1); print $1}')
AVAILABLE_DEVICES_SORTED=$(echo "$AVAILABLE_DEVICES_AWK" | sort)

AVAILABLE_DEVICES_FILE="$(cd "$(dirname "$0")"; pwd)/available_devices.txt"
echo "$AVAILABLE_DEVICES_SORTED" > "$AVAILABLE_DEVICES_FILE"
echo "[1/4] Available devices:"
cat "$AVAILABLE_DEVICES_FILE"

DEVICES_FILE="$(cd "$(dirname "$0")"; pwd)/uitest_devices.txt"
echo "[2/4] Creating new device list..."
echo "[4/4] Updated device list: $UPDATED_DEVICES_FILE"
echo "[2/2] Creating new device list..."
cat "$DEVICES_FILE" | while IFS= read -r line; do
  echo "Processing: $line"
  if [[ -z "$line" ]]; then continue; fi
  DISPLAY_NAME="${line%%|*}"
  REST="${line#*|}"
  DEVICETYPE_ID="${REST%%|*}"
  OS_VERSION="${REST#*|}"
  if [[ -z "$DISPLAY_NAME" || -z "$DEVICETYPE_ID" || -z "$OS_VERSION" ]]; then
    echo "    [SKIP] Invalid format: $line"
    echo "$line" >> "$ERROR_DEVICES_FILE"
    continue
  fi
  # OSバージョンからsimctl runtime IDを決定
  RUNTIME_ID="com.apple.CoreSimulator.SimRuntime.iOS-${OS_VERSION//./-}"
  # Simulatorが存在しない場合は作成
  if ! grep -Fxq "$DEVICETYPE_ID" "$AVAILABLE_DEVICES_FILE"; then
    echo "    [INFO] Creating simulator for $DISPLAY_NAME ($OS_VERSION)..."
    if xcrun simctl create "$DISPLAY_NAME" "$DEVICETYPE_ID" "$RUNTIME_ID"; then
      echo "    [CREATED] Simulator for $DISPLAY_NAME ($OS_VERSION) created."
    else
      echo "    [ERROR] Failed to create simulator for $DISPLAY_NAME ($OS_VERSION)."
      echo "$line" >> "$ERROR_DEVICES_FILE"
      continue
    fi
  fi
done

# 追加できなかった場合のみエラー出力
if [[ -s "$ERROR_DEVICES_FILE" ]]; then
  echo "[ERROR] Failed to add the following devices as simulators or resolve their deviceTypeId:"
  cat "$ERROR_DEVICES_FILE"
  exit 1
fi
