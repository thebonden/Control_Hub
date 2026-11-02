"""Drive the actual APK against a loopback TLS fixture on an explicitly selected emulator."""
import argparse
import base64
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--serial", required=True)
parser.add_argument("--adb", default="adb")
parser.add_argument("--apk", required=True)
parser.add_argument("--test-apk", required=True)
args = parser.parse_args()
if not args.serial.startswith("emulator-"):
    raise SystemExit("Select a disposable emulator / Vælg en testemulator")


def adb(*command):
    result = subprocess.run([args.adb, "-s", args.serial, *command], capture_output=True, timeout=150)
    if result.returncode:
        raise RuntimeError("Emulator command failed: " + command[0])
    return result.stdout.decode(errors="replace")


def exchange(process, command):
    process.stdin.write(command + "\n")
    process.stdin.flush()
    return json.loads(process.stdout.readline())


if adb("shell", "getprop", "ro.kernel.qemu").strip() != "1":
    raise SystemExit("Expected an emulator")
subprocess.run(["dotnet", "build", "tests/PhoneHarness", "-c", "Release"], check=True, cwd=root)
fixture = root / "tests/PhoneHarness/bin/Release/net10.0/PhoneHarness.dll"
with subprocess.Popen(["dotnet", str(fixture)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True) as server:
    port = None
    try:
        invite = exchange(server, "invite")
        from urllib.parse import urlparse
        port = str(urlparse(invite["transport"]["endpoint"]).port)
        adb("reverse", "tcp:" + port, "tcp:" + port)
        adb("install", "-r", args.apk)
        adb("install", "-r", args.test_apk)
        adb("shell", "pm", "clear", "dk.thebonden.controlhub.remote")
        # Mint a fresh invitation after install, so slow package installation does not spend its lifetime.
        invite = exchange(server, "invite")
        encoded = base64.b64encode(json.dumps(invite).encode()).decode()
        runner = "dk.thebonden.controlhub.remote.test/.PhoneTrial"
        response = adb("shell", "am", "instrument", "-w", "-e", "invite", encoded, runner)
        if "result=PASS" not in response:
            raise RuntimeError(response.replace(encoded, "[invitation]"))
        counts = exchange(server, "counts")
        assert counts == {"slides": 2, "scenes": 1, "slide": 1, "scene": "Camera"}, counts
        adb("reverse", "--remove", "tcp:" + port)
        response = adb("shell", "am", "instrument", "-w", "-e", "phase", "offline", runner)
        if "result=PASS" not in response:
            raise RuntimeError(response)
        adb("reverse", "tcp:" + port, "tcp:" + port)
        response = adb("shell", "am", "instrument", "-w", "-e", "phase", "restored", runner)
        if "result=PASS" not in response:
            raise RuntimeError(response)
        assert exchange(server, "counts") == counts
        exchange(server, "revoke")
        response = adb("shell", "am", "instrument", "-w", "-e", "phase", "revoked", runner)
        if "result=PASS" not in response:
            raise RuntimeError(response)
        assert exchange(server, "counts") == counts
        print("PASS: wrong certificate, pairing, next, previous, OBS scene, status, persistent session, revocation and exact action counts")
    finally:
        if port:
            adb("reverse", "--remove", "tcp:" + port)
        server.stdin.close()
