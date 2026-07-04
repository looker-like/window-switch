#!/usr/bin/env python3
import argparse
import sys
from pathlib import Path


EXCLUDED_DIRS = {"bin", "obj", ".git", ".vs", "TestResults", "publish"}


def iter_code_files(root: Path):
    for path in root.rglob("*.cs"):
        if any(part in EXCLUDED_DIRS for part in path.parts):
            continue

        yield path


def count_lines(path: Path) -> int:
    return len(path.read_text(encoding="utf-8-sig").splitlines())


def main() -> int:
    parser = argparse.ArgumentParser(description="Check each C# code file length.")
    parser.add_argument("--max-lines", type=int, default=300)
    parser.add_argument("--root", type=Path, default=Path("."))
    args = parser.parse_args()

    violations = []
    for path in iter_code_files(args.root):
        line_count = count_lines(path)
        if line_count > args.max_lines:
            violations.append((path, line_count))

    if not violations:
        print(f"Code file length check passed: no .cs file exceeds {args.max_lines} lines.")
        return 0

    print(f"Code file length check failed: .cs files exceed {args.max_lines} lines.")
    for path, line_count in violations:
        print(f"{path}: {line_count} lines.")

    return 1


if __name__ == "__main__":
    sys.exit(main())
