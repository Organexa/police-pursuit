"""Run the game's in-game autotest (unity/Assets/Scripts/Core/AutoTest.cs) on an Android emulator, no hands needed.

Usage:
    python tools/android_autotest.py Builds/7ab263d/PolicePursuit.apk
    python tools/android_autotest.py <apk> --avd Medium_Phone --keep-emulator --out art/autotest

Starts the emulator without a window if no device is attached, installs the APK, launches it with the "autotest"
extra, follows the AUTOTEST lines in the log, takes a screenshot at each SHOT, and writes report.txt with the
screenshots to --out (default: autotest-results/<apk folder name>, git-ignored under Builds/ when the APK is there).
Exit code 0 only when the run finished with every check passing and no crash.
Needs a test APK from the android-check workflow (built with -ppAutotest); a release APK ignores the extra.
The emulator runs Arm code through translation: its frame rate says nothing about a real phone.
"""
import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

PACKAGE = "com.policepursuit.game"  # Core/UpdateCheck.PackageName
SDK = Path(os.environ.get("ANDROID_SDK_ROOT") or os.environ.get("ANDROID_HOME")
           or Path(os.environ.get("LOCALAPPDATA", "")) / "Android" / "Sdk")
ADB = SDK / "platform-tools" / ("adb.exe" if os.name == "nt" else "adb")
EMULATOR = SDK / "emulator" / ("emulator.exe" if os.name == "nt" else "emulator")
LINE = re.compile(r"AUTOTEST (\w+) ?(.*)")


def adb(*args, check=True, timeout=120, binary=False):
    r = subprocess.run([str(ADB), *args], capture_output=True, timeout=timeout)
    if check and r.returncode != 0:
        raise RuntimeError(f"adb {' '.join(args)} failed: {r.stderr.decode(errors='replace').strip()}")
    return r.stdout if binary else r.stdout.decode(errors="replace").strip()


def device_ready() -> bool:
    return "\tdevice" in adb("devices", check=False)


def start_emulator(avd: str, boot_timeout: float) -> subprocess.Popen:
    print(f"Starting emulator {avd} (no window)...")
    proc = subprocess.Popen([str(EMULATOR), "-avd", avd, "-no-window", "-no-audio", "-no-boot-anim",
                             "-no-snapshot-save", "-gpu", "auto"],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    deadline = time.time() + boot_timeout
    while time.time() < deadline:
        if proc.poll() is not None:
            raise RuntimeError(f"the emulator exited (code {proc.returncode})")
        if device_ready() and adb("shell", "getprop", "sys.boot_completed", check=False) == "1":
            print("Emulator booted.")
            return proc
        time.sleep(3)
    proc.kill()
    raise RuntimeError(f"the emulator did not boot within {boot_timeout:.0f} s")


def launch_component() -> str:
    out = adb("shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE)
    comp = out.splitlines()[-1].strip()
    if "/" not in comp:
        raise RuntimeError(f"cannot find the app's launch activity: {out}")
    return comp


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("apk", type=Path)
    p.add_argument("--avd", default="Medium_Phone")
    p.add_argument("--out", type=Path)
    p.add_argument("--timeout", type=float, default=900, help="seconds for the whole autotest")
    p.add_argument("--boot-timeout", type=float, default=420)
    p.add_argument("--keep-emulator", action="store_true", help="leave an emulator this script started running")
    a = p.parse_args()

    if not a.apk.is_file():
        print(f"No such APK: {a.apk}", file=sys.stderr)
        return 2
    if not ADB.is_file():
        print(f"adb not found at {ADB}; set ANDROID_SDK_ROOT", file=sys.stderr)
        return 2
    out = a.out or (a.apk.parent / "autotest")
    out.mkdir(parents=True, exist_ok=True)

    emulator = None
    try:
        if not device_ready():
            emulator = start_emulator(a.avd, a.boot_timeout)
        print("Installing", a.apk)
        adb("install", "-r", "-g", str(a.apk), timeout=600)
        comp = launch_component()
        adb("shell", "am", "force-stop", PACKAGE)
        adb("logcat", "-c")
        adb("shell", "am", "start", "-n", comp, "--es", "autotest", "1")
        print("Launched", comp)

        log = subprocess.Popen([str(ADB), "logcat", "-v", "brief", "Unity:V", "AndroidRuntime:E", "DEBUG:F", "*:S"],
                               stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        lines, passed, failed, done, crashed = [], 0, 0, False, False
        deadline = time.time() + a.timeout
        started = False
        try:
            while time.time() < deadline:
                raw = log.stdout.readline()
                if not raw:
                    if log.poll() is not None:
                        break
                    continue
                text = raw.decode(errors="replace").rstrip()
                if "FATAL EXCEPTION" in text or "Fatal signal" in text or ("AndroidRuntime" in text and PACKAGE in text):
                    crashed = True
                    lines.append("CRASH " + text)
                    print("CRASH", text)
                m = LINE.search(text)
                if not m:
                    if crashed and len([l for l in lines if l.startswith("CRASH")]) > 30:
                        break
                    continue
                kind, rest = m.group(1), m.group(2)
                started = True
                lines.append(f"{kind} {rest}")
                print(kind, rest)
                if kind == "SHOT":
                    png = adb("exec-out", "screencap", "-p", binary=True, timeout=60)
                    (out / f"{len([l for l in lines if l.startswith('SHOT')]):02d}-{rest.strip() or 'shot'}.png").write_bytes(png)
                elif kind == "PASS":
                    passed += 1
                elif kind == "FAIL":
                    failed += 1
                elif kind == "DONE":
                    done = True
                    break
                if not started and time.time() > deadline - a.timeout + 120:
                    break
        finally:
            log.kill()

        if not done:
            reason = "crashed" if crashed else "no AUTOTEST lines (not a test APK?)" if not started else "timed out"
            lines.append(f"INCOMPLETE {reason}")
            adb("exec-out", "screencap", "-p", binary=True, timeout=60) and \
                (out / "last-screen.png").write_bytes(adb("exec-out", "screencap", "-p", binary=True, timeout=60))
        ok = done and failed == 0 and not crashed
        summary = f"{'OK' if ok else 'FAILED'}: passed={passed} failed={failed} done={done} crashed={crashed}"
        (out / "report.txt").write_text(f"{a.apk}\n{summary}\n\n" + "\n".join(lines) + "\n", encoding="utf-8")
        print(summary)
        print("Report and screenshots:", out)
        return 0 if ok else 1
    except Exception as e:  # noqa: BLE001 - report any setup failure plainly
        print(f"Autotest could not run: {e}", file=sys.stderr)
        return 1
    finally:
        if emulator and not a.keep_emulator:
            adb("emu", "kill", check=False)
            try:
                emulator.wait(timeout=60)
            except subprocess.TimeoutExpired:
                emulator.kill()


if __name__ == "__main__":
    sys.exit(main())
