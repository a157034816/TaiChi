from __future__ import annotations

import argparse
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from centralservice_script_common import normalize_base_url, normalize_items, write_info
from centralservice_sdk_support import (
    VALID_LANGUAGES,
    VALID_SDK_KINDS,
    build_all,
    get_repo_root,
    get_sdk_root,
    pack_all,
    run_e2e,
)


def parse_args() -> argparse.Namespace:
    """Parse CLI arguments while preserving the PowerShell-compatible names."""
    parser = argparse.ArgumentParser()
    parser.add_argument("-Build", "--build", action="store_true")
    parser.add_argument("-E2E", "--e2e", action="store_true")
    parser.add_argument("-Pack", "--pack", action="store_true")
    parser.add_argument("-All", "--all", action="store_true")
    parser.add_argument("-BaseUrl", "--base-url", default="http://127.0.0.1:5000")
    parser.add_argument("-Languages", "--languages", nargs="*", default=list(VALID_LANGUAGES))
    parser.add_argument("-SdkKinds", "--sdk-kinds", nargs="*", default=list(VALID_SDK_KINDS))
    return parser.parse_args()


def normalize_languages(values: list[str]) -> list[str]:
    """Validate and normalize requested languages."""
    normalized = [item.lower() for item in normalize_items(values)]
    for language in normalized:
        if language not in VALID_LANGUAGES:
            raise RuntimeError(f"Unsupported language: {language}")
    return normalized


def normalize_sdk_kinds(values: list[str]) -> list[str]:
    """Validate and normalize requested SDK kinds."""
    normalized = [item.lower() for item in normalize_items(values)]
    for kind in normalized:
        if kind not in VALID_SDK_KINDS:
            raise RuntimeError(f"Unsupported sdk kind: {kind}")
    return normalized


def main() -> None:
    """Execute the unified build, E2E and packaging entry point."""
    args = parse_args()
    repo_root = get_repo_root(SCRIPT_DIR)
    sdk_root = get_sdk_root(SCRIPT_DIR)
    base_url = normalize_base_url(args.base_url)
    languages = normalize_languages(args.languages)
    sdk_kinds = normalize_sdk_kinds(args.sdk_kinds)

    build_flag = args.build
    e2e_flag = args.e2e
    pack_flag = args.pack
    all_flag = args.all
    if not build_flag and not e2e_flag and not pack_flag and not all_flag:
        all_flag = True
    if all_flag:
        build_flag = True
        e2e_flag = True
        pack_flag = True

    if build_flag:
        build_all(repo_root, sdk_root, languages, sdk_kinds)
    if e2e_flag:
        run_e2e(repo_root, sdk_root, languages, base_url)
    if pack_flag:
        pack_all(repo_root, sdk_root, languages, sdk_kinds, base_url)
    write_info("Done.")


if __name__ == "__main__":
    main()
