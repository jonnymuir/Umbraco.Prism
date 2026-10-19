"""Tab-switch times from a simulator screen recording and the Maestro log of the same run.

Usage: tab-switch-timing.py <recording.mp4> <maestro.log>

The first tap in the run (a checkbox) redraws instantly, so its first visible change lines up the
video clock with the log clock. Each later tap is timed from when Maestro sent it to the last frame
in which the page area was still changing, ending at the first 300 ms of stillness. A spinner, a
blank page and content arriving all count as not finished.
"""
import datetime
import re
import subprocess
import sys

import numpy as np

FPS = 60
WIDTH, HEIGHT = 120, 260
STILL_FOR = int(0.3 * FPS)


def frames(video):
    raw = subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-i", video, "-vf", f"fps={FPS},scale={WIDTH}:{HEIGHT},format=gray",
         "-f", "rawvideo", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(-1, HEIGHT, WIDTH).astype(np.int16)


def seconds(line):
    t = datetime.datetime.strptime(line[:12], "%H:%M:%S.%f")
    return t.hour * 3600 + t.minute * 60 + t.second + t.microsecond / 1e6


def taps(log):
    # Maestro logs each tap's metadata twice; the second line follows the touch being sent.
    lines = [(m.group(1), seconds(line)) for line in open(log)
             if (m := re.search(r'Tap on "([^"]+)" metadata', line))]
    return lines[1::2]


def settle_frames(moving, start):
    quiet, last_moving = 0, None
    for i in range(max(start - 3, 0), min(start + 8 * FPS, len(moving))):
        if moving[i]:
            last_moving, quiet = i, 0
        else:
            quiet += 1
            if quiet >= STILL_FOR and (last_moving is not None or i > start + FPS):
                break
    return 0 if last_moving is None else last_moving + 1 - start


def main(video, log):
    video_frames = frames(video)
    page_area = video_frames[:, int(HEIGHT * 0.07):int(HEIGHT * 0.80), :]  # below the status bar, above the tab bar
    moving = np.abs(np.diff(page_area, axis=0)).mean(axis=(1, 2)) > 0.3

    sent = taps(log)
    calibration_name, calibration_time = sent[0]
    offset = int(np.argmax(moving)) / FPS - calibration_time
    print(f"calibrated on '{calibration_name}'")

    times = []
    for name, t in sent[1:]:
        ms = settle_frames(moving, int(round((t + offset) * FPS))) * 1000 / FPS
        times.append(ms)
        print(f"{name:15s} {ms:6.0f} ms")
    print(f"median {np.median(times):.0f} ms, max {max(times):.0f} ms over {len(times)} switches")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
