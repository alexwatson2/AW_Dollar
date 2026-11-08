#!/usr/bin/env python3
"""Stream build output and expose failures in GitHub check annotations, too."""
import collections
import pathlib
import subprocess
import sys
import xml.etree.ElementTree as ET

name, *command = sys.argv[1:]
if not command:
    raise SystemExit("Usage: run.py NAME COMMAND [ARG ...]")
log_dir = pathlib.Path("build/ci")
log_dir.mkdir(parents=True, exist_ok=True)
tail = collections.deque(maxlen=100)
with (log_dir / (name + ".log")).open("w", encoding="utf-8") as log:
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               text=True, encoding="utf-8", errors="replace")
    for line in process.stdout:
        print(line, end="", flush=True)
        log.write(line)
        if not line.lstrip().startswith(("at ", "... ")):
            tail.append(line)
    code = process.wait()
if code:
    # Test failures are otherwise buried above Gradle's long stack trace.
    for report in pathlib.Path("app/build/test-results").rglob("TEST-*.xml"):
        for case in ET.parse(report).iter("testcase"):
            for failure in list(case.findall("failure")) + list(case.findall("error")):
                tail.append(case.get("classname", "") + "." + case.get("name", "") + ": "
                            + failure.get("message", "") + "\n" + (failure.text or "")[:2000] + "\n")
    message = (name + " failed (exit " + str(code) + "):\n" + "".join(tail))[-12000:]
    message = message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    print("::error title=Android " + name + "::" + message, flush=True)
raise SystemExit(code)
