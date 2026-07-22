#!/usr/bin/env python3
# coverage-gaps.py — ranks uncovered/under-covered methods from a cobertura coverage report.
#
# Usage: python scripts/coverage-gaps.py [--report path/to/coverage.cobertura.xml] [--top N]
#                                        [--include-noise] [--namespace TRPG.Application]
#
#   --report        path to a specific coverage.cobertura.xml. Defaults to the newest
#                    TestResults/**/coverage.cobertura.xml (i.e. the most recent
#                    `dotnet test --collect:"XPlat Code Coverage"` run).
#   --top           how many methods to print, ranked by uncovered line count descending
#                    (default 40).
#   --include-noise disables the default filtering (see below) and shows every method,
#                    including DI wiring, Program.cs, migrations, and compiler-generated
#                    members. Useful for sanity-checking the filter itself.
#   --namespace     restrict output to classes whose full name starts with this prefix
#                    (e.g. `--namespace TRPG.Application.Combat`). Repeatable.
#
# Generate the report first (see CLAUDE.md's "Code Coverage" section):
#   dotnet test TRPG.sln --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults
#
# coverlet.runsettings (repo root) excludes TRPG.Data entirely at collection time --
# entity models, EF schema config, and migrations, none of which have branching logic
# worth chasing (see that file's comment). This script's own filtering below handles
# everything else that's still noise after that: Program.cs/Main, *ServiceCollectionExtensions
# (DI wiring), and compiler/record-generated members.
#
# What this does that reading CoverageReport/index.html doesn't:
#   - Folds compiler-generated async state machines (Foo/<Bar>d__6.MoveNext) and iterator
#     blocks back onto the real method they belong to (Foo.Bar), so an uncovered async
#     method shows up as one entry instead of a cryptic nested-class line.
#   - Drops known-noise buckets by default: Program.cs / Main, *ServiceCollectionExtensions
#     (DI wiring), and compiler/record-generated members (ToString/Equals/GetHashCode/
#     Deconstruct/op_*/<Clone>$/backing-field property accessors) -- these were called out
#     in an earlier coverage pass as not worth chasing, so they're excluded here rather
#     than re-triaged by eye every time.
#   - Ranks by uncovered line count, which is a decent proxy for "how much untested
#     behavior lives here" -- not perfect (a 40-line method with 2 untested branches
#     ranks below a 10-line fully-untested method), so treat the ranking as a starting
#     point for reading the actual code, not a final verdict.
#
# Output is plain text (namespace/class/method, covered/total lines, first few
# uncovered line numbers) so it can be read directly in a terminal or piped/grepped.

import argparse
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

NOISE_NAMESPACE_PREFIXES = [
    # TRPG.Data (models, schema config, migrations) is excluded at collection time via
    # coverlet.runsettings, not here -- see that file's comment for why.
]

NOISE_CLASS_SUFFIXES = [
    "ServiceCollectionExtensions",
]

NOISE_CLASS_NAMES = {
    "Program",
}

NOISE_METHOD_NAMES = {
    "ToString",
    "Equals",
    "GetHashCode",
    "Deconstruct",
    "Finalize",
    "Main",
    "<Clone>$",
    "op_Equality",
    "op_Inequality",
}

STATE_MACHINE_CLASS_RE = re.compile(r"^(?P<owner>.+)/<(?P<method>[^>]+)>d__\d+$")
LOCAL_FUNCTION_METHOD_RE = re.compile(r"^<(?P<method>[^>]+)>g__.+\|.+$")
BACKING_PROPERTY_METHOD_RE = re.compile(r"^(get|set)_(?P<prop>.+)$")


def find_default_report():
    candidates = glob.glob(
        os.path.join("TestResults", "**", "coverage.cobertura.xml"), recursive=True
    )
    if not candidates:
        return None
    candidates.sort(key=os.path.getmtime, reverse=True)
    return candidates[0]


def normalize_class_and_method(class_name, method_name):
    """Fold compiler-generated state-machine/local-function names back onto their real owner."""
    m = STATE_MACHINE_CLASS_RE.match(class_name)
    if m and method_name == "MoveNext":
        return m.group("owner"), m.group("method")

    m = LOCAL_FUNCTION_METHOD_RE.match(method_name)
    if m:
        return class_name, f"{m.group('method')} (local function)"

    return class_name, method_name


def is_noise(namespace_and_class, method_name, include_noise):
    if include_noise:
        return False

    for prefix in NOISE_NAMESPACE_PREFIXES:
        if namespace_and_class.startswith(prefix):
            return True

    class_simple = namespace_and_class.rsplit(".", 1)[-1].split("/")[0]
    if class_simple in NOISE_CLASS_NAMES:
        return True
    for suffix in NOISE_CLASS_SUFFIXES:
        if class_simple.endswith(suffix):
            return True

    if method_name in NOISE_METHOD_NAMES:
        return True
    if method_name == ".ctor" or method_name == ".cctor":
        return True
    if BACKING_PROPERTY_METHOD_RE.match(method_name):
        return True

    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", default=None)
    parser.add_argument("--top", type=int, default=40)
    parser.add_argument("--include-noise", action="store_true")
    parser.add_argument("--namespace", action="append", default=[])
    args = parser.parse_args()

    report_path = args.report or find_default_report()
    if not report_path or not os.path.exists(report_path):
        print(
            "No coverage.cobertura.xml found. Run:\n"
            '  dotnet test TRPG.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults',
            file=sys.stderr,
        )
        sys.exit(1)

    tree = ET.parse(report_path)
    root = tree.getroot()

    # key: (full_class_name, method_name) -> {"total": int, "covered": int, "uncovered_lines": [int]}
    aggregated = {}

    for package in root.iter("package"):
        for cls in package.iter("class"):
            class_name = cls.get("name")
            for method in cls.iter("method"):
                method_name = method.get("name")
                owner_class, owner_method = normalize_class_and_method(
                    class_name, method_name
                )

                if is_noise(owner_class, owner_method, args.include_noise):
                    continue

                if args.namespace and not any(
                    owner_class.startswith(ns) for ns in args.namespace
                ):
                    continue

                entry = aggregated.setdefault(
                    (owner_class, owner_method), {"total": 0, "covered": 0, "uncovered": []}
                )
                for line in method.iter("line"):
                    entry["total"] += 1
                    hits = int(line.get("hits", "0"))
                    if hits > 0:
                        entry["covered"] += 1
                    else:
                        entry["uncovered"].append(int(line.get("number")))

    ranked = [
        (owner_class, owner_method, data)
        for (owner_class, owner_method), data in aggregated.items()
        if data["total"] > data["covered"]
    ]
    ranked.sort(key=lambda x: (len(x[2]["uncovered"]), x[2]["total"]), reverse=True)

    total_methods = len(aggregated)
    fully_covered = sum(1 for d in aggregated.values() if d["covered"] == d["total"])
    print(
        f"{report_path}\n"
        f"{total_methods} methods tracked (after filtering), {fully_covered} fully covered, "
        f"{len(ranked)} with at least one uncovered line\n"
    )

    for owner_class, owner_method, data in ranked[: args.top]:
        uncovered = data["uncovered"]
        shown = ", ".join(str(n) for n in uncovered[:8])
        more = f", +{len(uncovered) - 8} more" if len(uncovered) > 8 else ""
        print(
            f"{owner_class}.{owner_method}  "
            f"[{data['covered']}/{data['total']} lines covered]  "
            f"uncovered: {shown}{more}"
        )


if __name__ == "__main__":
    main()
