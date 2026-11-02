#!/usr/bin/env bash
set -euo pipefail

sdk="${ANDROID_HOME:?Android SDK required}"
out="$RUNNER_TEMP/controlhub-android"
mkdir -p "$out"
"$sdk/cmdline-tools/latest/bin/sdkmanager" "platforms;android-35" "build-tools;36.0.0" "system-images;android-35;google_atd;x86_64" emulator platform-tools
build=(python3 Plugins/AndroidRemote/Android/build.py --sdk "$sdk" --jdk "$JAVA_HOME" --keystore "$out/dev.p12")
"${build[@]}" --out "$out/ControlHub-AndroidRemote-0.0.001.apk"
"${build[@]}" --ui-test --out "$out/ui-test.apk"

sudo setfacl -m "u:$(id -un):rw" /dev/kvm
echo no | "$sdk/cmdline-tools/latest/bin/avdmanager" create avd -n ControlHubTrial -k "system-images;android-35;google_atd;x86_64" --device pixel_5
"$sdk/emulator/emulator" -avd ControlHubTrial -port 5590 -no-window -no-audio -no-snapshot -no-boot-anim -gpu swiftshader -memory 2048 >"$out/emulator.log" 2>&1 &
emulator_pid=$!
trap 'kill "$emulator_pid" 2>/dev/null || true' EXIT
adb="$sdk/platform-tools/adb"
for attempt in {1..120}; do
  if [[ "$("$adb" -s emulator-5590 shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == 1 ]]; then
    python3 tests/android-ui/run.py --serial emulator-5590 --adb "$adb" --apk "$out/ControlHub-AndroidRemote-0.0.001.apk" --test-apk "$out/ui-test.apk"
    exit 0
  fi
  sleep 2
done
tail -40 "$out/emulator.log"
exit 1
