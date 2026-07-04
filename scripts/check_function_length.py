#!/usr/bin/env python3
import argparse
import re
import sys
from pathlib import Path


EXCLUDED_DIRS = {"bin", "obj", ".git", ".vs", "TestResults", "publish"}
CONTROL_KEYWORDS = (
    "if",
    "else",
    "for",
    "foreach",
    "while",
    "switch",
    "catch",
    "try",
    "finally",
    "using",
    "lock",
    "namespace",
    "class",
    "struct",
    "record",
    "interface",
    "enum",
)


def iter_code_files(root: Path):
    for path in root.rglob("*.cs"):
        if any(part in EXCLUDED_DIRS for part in path.parts):
            continue

        yield path


def strip_line_comment(line: str) -> str:
    quote = None
    escaped = False
    for index, char in enumerate(line):
        if escaped:
            escaped = False
            continue

        if char == "\\":
            escaped = True
            continue

        if quote:
            if char == quote:
                quote = None
            continue

        if char in ("\"", "'"):
            quote = char
            continue

        if char == "/" and index + 1 < len(line) and line[index + 1] == "/":
            return line[:index]

    return line


def looks_like_function(signature: str) -> bool:
    text = " ".join(signature.strip().split())
    if not text or "(" not in text or ")" not in text:
        return False

    if "=>" in text:
        return False

    prefix = text.split("(", 1)[0].strip()
    if not prefix:
        return False

    first = prefix.split()[0]
    if first in CONTROL_KEYWORDS:
        return False

    if re.search(r"\b(" + "|".join(CONTROL_KEYWORDS) + r")\s*$", prefix):
        return False

    return bool(re.search(r"[\w>\]\)]\s*$", prefix))


def scan_file(path: Path):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    pending_signature = []
    active = []
    findings = []

    for line_number, raw_line in enumerate(lines, start=1):
        line = strip_line_comment(raw_line)
        stripped = line.strip()

        if not active:
            if stripped:
                pending_signature.append(stripped)
                pending_signature = pending_signature[-8:]

            if "{" in line:
                before_brace = line.split("{", 1)[0]
                signature = " ".join([*pending_signature[:-1], before_brace]).strip()
                if looks_like_function(signature):
                    name_match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>]+>)?\s*\([^()]*\)\s*$", signature)
                    name = name_match.group(1) if name_match else "<unknown>"
                    active.append(
                        {
                            "name": name,
                            "start": line_number,
                            "brace_depth": 0,
                        }
                    )

                pending_signature.clear()

        for char in line:
            if char == "{":
                for item in active:
                    item["brace_depth"] += 1
            elif char == "}":
                completed = []
                for item in active:
                    item["brace_depth"] -= 1
                    if item["brace_depth"] <= 0:
                        completed.append(item)

                for item in completed:
                    active.remove(item)
                    findings.append(
                        {
                            "path": path,
                            "name": item["name"],
                            "start": item["start"],
                            "end": line_number,
                            "length": line_number - item["start"] + 1,
                        }
                    )

    return findings


def main() -> int:
    parser = argparse.ArgumentParser(description="Check each C# function/method length.")
    parser.add_argument("--max-lines", type=int, default=300)
    parser.add_argument("--root", type=Path, default=Path("."))
    args = parser.parse_args()

    violations = []
    for path in iter_code_files(args.root):
        for function in scan_file(path):
            if function["length"] > args.max_lines:
                violations.append(function)

    if not violations:
        print(f"Function length check passed: no single function/method exceeds {args.max_lines} lines.")
        return 0

    print(f"Function length check failed: single functions/methods exceed {args.max_lines} lines.")
    for item in violations:
        print(
            f"{item['path']}:{item['start']} {item['name']} "
            f"is {item['length']} lines (ends at {item['end']})."
        )

    return 1


if __name__ == "__main__":
    sys.exit(main())
