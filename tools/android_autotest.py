"""Run the game's in-game autotest (unity/Assets/Scripts/Core/AutoTest.cs) on an Android emulator, no hands needed.

Usage:
    python tools/android_autotest.py Builds/7ab263d/PolicePursuit.apk
    python tools/android_autotest.py <apk> --avd Medium_Phone_API_37.0 --keep-emulator --out art/autotest

Starts the emulator without a window if no device is attached, installs the APK, launches it with the "autotest"
extra, follows the AUTOTEST lines in the log, takes a screenshot at each SHOT, and writes report.txt with the
screenshots to --out (default: autotest-results/<apk folder name>, git-ignored under Builds/ when the APK is there).
Exit code 0 only when the run finished with every check passing and no crash.
Needs a test APK from the android-check workflow (built with -ppAutotest); a release APK ignores the extra.
The emulator runs Arm code through translation: its frame rate says nothing about a real phone.
"""
import argparse
import os
import queue
import re
import subprocess
import sys
import threading
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
    if not avd:
        found = subprocess.run([str(EMULATOR), "-list-avds"], capture_output=True, text=True).stdout.split()
        if not found:
            raise RuntimeError("no emulator phone (AVD) exists; make one in Android Studio's Device Manager")
        avd = found[0]
    print(f"Starting emulator {avd} (no window)...")
    log = Path(os.environ.get("TEMP", "/tmp")) / "android_autotest_emulator.log"
    # -gpu host: the real GPU. "auto" chose llvmpipe (Vulkan on the CPU), too slow for Unity: adb dropped off.
    proc = subprocess.Popen([str(EMULATOR), "-avd", avd, "-no-window", "-no-audio", "-no-boot-anim",
                             "-no-snapshot-save", "-gpu", "host"],
                            stdout=log.open("w"), stderr=subprocess.STDOUT)
    deadline = time.time() + boot_timeout
    while time.time() < deadline:
        if proc.poll() is not None:
            tail = log.read_text(errors="replace").strip().splitlines()[-5:]
            raise RuntimeError(f"the emulator exited (code {proc.returncode}): " + " / ".join(tail))
        if device_ready() and adb("shell", "getprop", "sys.boot_completed", check=False) == "1":
            print("Emulator booted.")
            return proc
        time.sleep(3)
    proc.kill()
    raise RuntimeError(f"the emulator did not boot within {boot_timeout:.0f} s")


def follow_log():
    # logcat on a reader thread, so the main loop can time out and notice when the device drops off.
    proc = subprocess.Popen([str(ADB), "logcat", "-v", "time", "Unity:V", "AndroidRuntime:E", "DEBUG:F", "*:S"],
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    q = queue.Queue()

    def pump():
        for raw in proc.stdout:
            q.put(raw.decode(errors="replace").rstrip())

    threading.Thread(target=pump, daemon=True).start()
    return proc, q


def wait_for_device(seconds: float) -> bool:
    try:
        subprocess.run([str(ADB), "wait-for-device"], timeout=seconds, capture_output=True)
        return device_ready()
    except subprocess.TimeoutExpired:
        return False


def screenshot(path: Path) -> bool:
    try:
        path.write_bytes(adb("exec-out", "screencap", "-p", binary=True, timeout=60))
        return True
    except Exception as e:  # noqa: BLE001 - a missed screenshot must not end the run
        print(f"screenshot failed: {e}")
        return False


def prepare_device():
    # A sleeping screen pauses Unity (no frames, so the autotest never starts); keep it awake and unlocked,
    # accept Android's one-time "Viewing full screen" hint so it does not cover the screenshots, and keep a log
    # buffer big enough that start-up lines are not lost.
    for cmd in (["shell", "svc", "power", "stayon", "true"],
                ["shell", "settings", "put", "system", "screen_off_timeout", "1800000"],
                ["shell", "settings", "put", "secure", "immersive_mode_confirmations", "confirmed"],
                ["shell", "input", "keyevent", "KEYCODE_WAKEUP"],
                ["shell", "wm", "dismiss-keyguard"],
                ["logcat", "-G", "16M"]):
        adb(*cmd, check=False)


def launch_component() -> str:
    out = adb("shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE)
    comp = out.splitlines()[-1].strip()
    if "/" not in comp:
        raise RuntimeError(f"cannot find the app's launch activity: {out}")
    return comp


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("apk", type=Path)
    p.add_argument("--avd", help="emulator phone to start (default: the first one `emulator -list-avds` shows)")
    p.add_argument("--out", type=Path)
    p.add_argument("--timeout", type=float, default=900, help="seconds for the whole autotest")
    p.add_argument("--boot-timeout", type=float, default=420)
    p.add_argument("--start-timeout", type=float, default=300, help="seconds to wait for the first AUTOTEST line")
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
        prepare_device()
        adb("shell", "am", "force-stop", PACKAGE)
        adb("logcat", "-c")
        adb("shell", "am", "start", "-n", comp, "--es", "autotest", "1")
        print("Launched", comp)

        lines, passed, failed, done, crashed, started = [], 0, 0, False, False, False
        seen, shots, t0 = set(), 0, time.time()
        deadline = t0 + a.timeout
        log, q = follow_log()
        try:
            while time.time() < deadline and not done:
                if not started and time.time() - t0 > a.start_timeout:
                    break
                try:
                    text = q.get(timeout=5)
                except queue.Empty:
                    if log.poll() is not None:  # logcat ended: the device dropped off; wait for it, then follow again
                        print(f"[{time.time() - t0:5.0f}s] device dropped off; waiting for it...")
                        if not wait_for_device(120):
                            lines.append("DEVICE LOST")
                            break
                        log, q = follow_log()
                    continue
                if text in seen:  # a reconnected logcat replays its buffer
                    continue
                seen.add(text)
                if "FATAL EXCEPTION" in text or "Fatal signal" in text or ("AndroidRuntime" in text and PACKAGE in text):
                    crashed = True
                    lines.append("CRASH " + text)
                    print("CRASH", text)
                m = LINE.search(text)
                if not m:
                    continue
                kind, rest = m.group(1), m.group(2).strip()
                started = True
                lines.append(f"{kind} {rest}")
                print(f"[{time.time() - t0:5.0f}s] {kind} {rest}")
                if kind == "SHOT":
                    shots += 1
                    if not screenshot(out / f"{shots:02d}-{rest or 'shot'}.png"):
                        lines.append(f"NOSHOT {rest}")
                elif kind == "PASS":
                    passed += 1
                elif kind == "FAIL":
                    failed += 1
                elif kind == "DONE":
                    done = True
        finally:
            log.kill()

        if not done:
            reason = ("crashed" if crashed else "device lost" if "DEVICE LOST" in lines
                      else f"no AUTOTEST lines within {a.start_timeout:.0f} s (not a test APK, or too slow to start?)"
                      if not started else "timed out")
            lines.append(f"INCOMPLETE {reason}")
            screenshot(out / "last-screen.png")
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
