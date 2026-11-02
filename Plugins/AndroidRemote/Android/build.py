"""Build and verify the development APK with the Android SDK and a JDK."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent
APP_ID = "dk.thebonden.controlhub.remote"


def build(source, manifest, output, android_sdk, jdk, keystore):
    sdk = android_sdk / "build-tools" / "36.0.0"
    platform = android_sdk / "platforms" / "android-35" / "android.jar"
    executable = ".exe" if os.name == "nt" else ""
    java = jdk / "bin" / ("java" + executable)

    def run(*args):
        subprocess.run([str(arg) for arg in args], check=True)

    output.parent.mkdir(parents=True, exist_ok=True)
    keystore.parent.mkdir(parents=True, exist_ok=True)
    if not keystore.exists():
        run(jdk / "bin" / ("keytool" + executable), "-genkeypair", "-keystore", keystore,
            "-storepass", "android", "-keypass", "android", "-alias", "androiddebugkey",
            "-keyalg", "RSA", "-keysize", "2048", "-validity", "3650", "-dname", "CN=Control Hub Development")

    with tempfile.TemporaryDirectory(dir=output.parent) as temp:
        work = Path(temp)
        classes = work / "classes"
        classes.mkdir()
        run(jdk / "bin" / ("javac" + executable), "-encoding", "UTF-8", "-source", "8", "-target", "8",
            "-classpath", platform, "-d", classes, *sorted(source.rglob("*.java")))
        jar = work / "classes.jar"
        with zipfile.ZipFile(jar, "w") as archive:
            for entry in classes.rglob("*.class"):
                archive.write(entry, entry.relative_to(classes).as_posix())
        run(java, "-cp", sdk / "lib/d8.jar", "com.android.tools.r8.D8", "--min-api", "26",
            "--lib", platform, "--output", work, jar)
        unsigned = work / "unsigned.apk"
        run(sdk / ("aapt2" + executable), "link", "-o", unsigned, "-I", platform, "--manifest", manifest)
        with zipfile.ZipFile(unsigned, "a", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.write(work / "classes.dex", "classes.dex")
        aligned = work / "aligned.apk"
        run(sdk / ("zipalign" + executable), "-f", "4", unsigned, aligned)
        signed = work / "signed.apk"
        signer = sdk / "lib/apksigner.jar"
        run(java, "-jar", signer, "sign", "--ks", keystore, "--ks-pass", "pass:android",
            "--ks-key-alias", "androiddebugkey", "--out", signed, aligned)
        run(java, "-jar", signer, "verify", "--verbose", signed)
        signed.replace(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--sdk", type=Path, required=True)
    parser.add_argument("--jdk", type=Path, required=True)
    parser.add_argument("--keystore", type=Path, required=True, help="Development signing key outside the repository")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--ui-test", action="store_true")
    args = parser.parse_args()
    module = ROOT.parents[2] / "tests" / "android-ui" if args.ui_test else ROOT
    build(module / "src", module / "AndroidManifest.xml", args.out.resolve(),
          args.sdk.resolve(), args.jdk.resolve(), args.keystore.resolve())
