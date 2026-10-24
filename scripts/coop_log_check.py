#!/usr/bin/env python3
"""Checks two players' BepInEx logs from one co-op session for bundles that went out of step.

    py -3 scripts/coop_log_check.py HOST_LogOutput.log GUEST_LogOutput.log
    py -3 scripts/coop_log_check.py HOST.log GUEST.log --stall HOST_BundleUp-stall.log GUEST_BundleUp-stall.log

It reads the "[Coop]" lines Dizzy Bundle Up writes (see CoopBridge.cs). Both players need
a build that writes "Applied" lines, or the final counts can't be compared.

What it checks:
  1. Delivery: every "Sent" on one side is matched by a "Received" of the same bundle on the
     other. A bundle sent more often than it was received lost a message.
  2. Final counts: the last count each side has for a bundle, from what it sent, applied or
     merged. A bundle whose two counts differ ended out of step.
  3. Unknown bundles: "Received bundle N, which is not here."
  4. Warnings and errors from Dizzy Bundle Up.
  5. Stall reports ("Game stopped for N s") from the optional stall logs.

Also listed, but not failures: merges of simultaneous changes and ignored pick-ups.

Exit code 0 when nothing is wrong, 1 when a check failed, 2 on a bad argument.
"""
import argparse
import re
import sys
from collections import defaultdict

PLUGIN = "Dizzy Bundle Up"

SENT = re.compile(r"\[Coop\] Sent (?P<name>.+?) (?P<id>-?\d+): (?P<n>\d+) pieces\.")
RECEIVED = re.compile(r"\[Coop\] Received (?P<name>.+?) (?P<id>-?\d+)(?: \(held\))?\.$")
APPLIED = re.compile(r"\[Coop\] Applied (?P<name>.+?) (?P<id>-?\d+): (?P<n>\d+) pieces\.")
NOT_HERE = re.compile(r"\[Coop\] Received bundle (?P<id>-?\d+), which is not here\.")
MERGED = re.compile(r"\[Coop\] (?P<name>.+?) (?P<id>-?\d+) was changed here too: (?P<n>\d+) pieces\.")
IGNORED = re.compile(r"\[Coop\] Another player picked up (?P<id>-?\d+), a bundle we just combined")
LEVEL = re.compile(r"^\[(?P<level>Warning|Error|Fatal)\s*:" + re.escape(PLUGIN) + r"\] (?P<msg>.*)$")
STALL = re.compile(r"Game stopped for (?P<secs>\d+) s\. Last place: (?P<place>.*?)\. Last bundle work: (?P<work>.*)\.")


class Side:
    def __init__(self, name):
        self.name = name
        self.sent = defaultdict(int)
        self.received = defaultdict(int)
        self.final = {}          # id -> (count, line number)
        self.names = {}
        self.not_here = []       # (id, line number)
        self.merged = []         # (id, count, line number)
        self.ignored = []
        self.problems = []       # (level, message, line number)
        self.seen = False


def parse(path, name):
    side = Side(name)
    with open(path, encoding="utf-8", errors="replace") as handle:
        for number, raw in enumerate(handle, 1):
            line = raw.rstrip("\r\n")
            m = LEVEL.match(line)
            if m:
                side.problems.append((m.group("level"), m.group("msg"), number))
            if "[Coop]" not in line:
                continue
            side.seen = True
            m = SENT.search(line)
            if m:
                side.sent[m.group("id")] += 1
                side.names[m.group("id")] = m.group("name")
                side.final[m.group("id")] = (int(m.group("n")), number)
                continue
            m = APPLIED.search(line)
            if m:
                side.names[m.group("id")] = m.group("name")
                side.final[m.group("id")] = (int(m.group("n")), number)
                continue
            m = RECEIVED.search(line)
            if m and not line.endswith("is not here."):
                side.received[m.group("id")] += 1
                side.names[m.group("id")] = m.group("name")
                continue
            m = NOT_HERE.search(line)
            if m:
                side.not_here.append((m.group("id"), number))
                continue
            m = MERGED.search(line)
            if m:
                side.merged.append((m.group("id"), int(m.group("n")), number))
                side.final[m.group("id")] = (int(m.group("n")), number)
                continue
            m = IGNORED.search(line)
            if m:
                side.ignored.append((m.group("id"), number))
                continue
    return side


def label(side, ident):
    return "#" + ident + " (" + side.names.get(ident, "?") + ")"


def check_delivery(a, b, report):
    bad = 0
    for sender, receiver in ((a, b), (b, a)):
        for ident, sent in sorted(sender.sent.items()):
            got = receiver.received.get(ident, 0)
            if got < sent:
                bad += 1
                report("LOST", "%s sent %s %d time(s); %s received it %d time(s)."
                       % (sender.name, label(sender, ident), sent, receiver.name, got))
    return bad


def check_final(a, b, report):
    bad = 0
    for ident in sorted(set(a.final) & set(b.final), key=lambda i: int(i)):
        (na, la), (nb, lb) = a.final[ident], b.final[ident]
        if na != nb:
            bad += 1
            report("DIFFERS", "%s ends at %d pieces on %s (line %d) and %d on %s (line %d)."
                   % (label(a, ident), na, a.name, la, nb, b.name, lb))
    return bad


def check_unknown(sides, report):
    bad = 0
    for side in sides:
        for ident, number in side.not_here:
            bad += 1
            report("UNKNOWN", "%s was sent a bundle %s that it does not have (line %d)."
                   % (side.name, ident, number))
    return bad


def check_problems(sides, report):
    bad = 0
    for side in sides:
        for level, message, number in side.problems:
            bad += 1
            report(level.upper(), "%s line %d: %s" % (side.name, number, message))
    return bad


def read_stalls(path, name, report):
    bad = 0
    with open(path, encoding="utf-8", errors="replace") as handle:
        for number, raw in enumerate(handle, 1):
            m = STALL.search(raw)
            if m:
                bad += 1
                report("STALL", "%s stalled %s s at '%s', last bundle work: %s (line %d)."
                       % (name, m.group("secs"), m.group("place"), m.group("work"), number))
    return bad


def run(host_log, guest_log, stalls=(), out=print):
    findings = []

    def report(kind, text):
        findings.append((kind, text))

    host, guest = parse(host_log, "host"), parse(guest_log, "guest")
    for side in (host, guest):
        if not side.seen:
            report("EMPTY", "%s's log has no [Coop] lines. Was Coop loaded, and did anyone make a bundle?" % side.name)

    bad = 0
    bad += check_delivery(host, guest, report)
    bad += check_final(host, guest, report)
    bad += check_unknown((host, guest), report)
    bad += check_problems((host, guest), report)
    for path, name in zip(stalls, ("host", "guest")):
        bad += read_stalls(path, name, report)
    bad += sum(1 for kind, _ in findings if kind == "EMPTY")

    # Not failures: worth knowing.
    notes = []
    for side in (host, guest):
        for ident, count, number in side.merged:
            notes.append("MERGED  %s kept both changes to #%s: %d pieces (line %d)." % (side.name, ident, count, number))
        for ident, number in side.ignored:
            notes.append("IGNORED %s ignored a pick-up of #%s, a bundle it had just combined (line %d)." % (side.name, ident, number))

    both = set(host.final) & set(guest.final)
    out("host: %d bundle(s) sent or applied; guest: %d. Compared: %d."
        % (len(host.final), len(guest.final), len(both)))
    for kind, text in findings:
        out("%-8s %s" % (kind, text))
    for note in notes:
        out(note)
    out("FAILED: %d problem(s)." % bad if bad else "OK: no problems found.")
    return 1 if bad else 0


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check two players' logs for bundles that went out of step.")
    parser.add_argument("host_log")
    parser.add_argument("guest_log")
    parser.add_argument("--stall", nargs=2, metavar=("HOST", "GUEST"), help="the two BundleUp-stall.log files")
    args = parser.parse_args(argv)
    try:
        return run(args.host_log, args.guest_log, args.stall or ())
    except OSError as ex:
        print("Could not read a log: %s" % ex, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
