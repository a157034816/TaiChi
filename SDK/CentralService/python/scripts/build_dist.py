from __future__ import annotations

import argparse
import base64
import hashlib
import re
import tarfile
import zipfile
from pathlib import Path


def _norm_dist_for_wheel(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_")


def _sha256_record(data: bytes) -> str:
    digest = hashlib.sha256(data).digest()
    b64 = base64.urlsafe_b64encode(digest).decode("ascii").rstrip("=")
    return "sha256=" + b64


def _read_file(path: Path) -> bytes:
    return path.read_bytes()


def _iter_package_files(pkg_dir: Path) -> list[Path]:
    return sorted(p for p in pkg_dir.rglob("*.py") if p.is_file())


def build_sdist(project_name: str, version: str, project_root: Path, package_dir: str, out_dir: Path) -> Path:
    root_name = f"{project_name}-{version}"
    out_path = out_dir / f"{root_name}.tar.gz"

    pkg_path = project_root / package_dir
    include = [
        project_root / "pyproject.toml",
        project_root / "README.md",
    ] + _iter_package_files(pkg_path)

    with tarfile.open(out_path, "w:gz") as tf:
        for src in include:
            rel = src.relative_to(project_root).as_posix()
            arcname = f"{root_name}/{rel}"
            tf.add(src.as_posix(), arcname=arcname, recursive=False)

    return out_path


def build_wheel(
    project_name: str,
    version: str,
    project_root: Path,
    package_dir: str,
    summary: str,
    out_dir: Path,
) -> Path:
    dist = _norm_dist_for_wheel(project_name).replace("-", "_")
    wheel_file = f"{dist}-{version}-py3-none-any.whl"
    out_path = out_dir / wheel_file

    pkg_path = project_root / package_dir
    pkg_files = _iter_package_files(pkg_path)

    dist_info = f"{dist}-{version}.dist-info"
    metadata = "\n".join(
        [
            "Metadata-Version: 2.1",
            f"Name: {project_name}",
            f"Version: {version}",
            f"Summary: {summary}",
            "License: MIT",
            "Requires-Python: >=3.10",
            "",
        ]
    ).encode("utf-8")

    wheel_meta = "\n".join(
        [
            "Wheel-Version: 1.0",
            "Generator: erp-centralservice-sdk (offline builder)",
            "Root-Is-Purelib: true",
            "Tag: py3-none-any",
            "",
        ]
    ).encode("utf-8")

    top_level_name = package_dir.strip("/").split("/")[-1]
    top_level = (top_level_name + "\n").encode("utf-8")

    records: list[tuple[str, str, int]] = []

    def write_bytes(zf: zipfile.ZipFile, file_path: str, data: bytes) -> None:
        zf.writestr(file_path, data)
        records.append((file_path, _sha256_record(data), len(data)))

    def write_file(zf: zipfile.ZipFile, src: Path, dst: str) -> None:
        data = _read_file(src)
        zf.writestr(dst, data)
        records.append((dst, _sha256_record(data), len(data)))

    with zipfile.ZipFile(out_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for src in pkg_files:
            rel = src.relative_to(project_root).as_posix()
            write_file(zf, src, rel)

        write_bytes(zf, f"{dist_info}/METADATA", metadata)
        write_bytes(zf, f"{dist_info}/WHEEL", wheel_meta)
        write_bytes(zf, f"{dist_info}/top_level.txt", top_level)

        record_path = f"{dist_info}/RECORD"
        record_lines = []
        for file_path, hash_value, size in records:
            record_lines.append(f"{file_path},{hash_value},{size}")
        record_lines.append(f"{record_path},,")
        record_data = ("\n".join(record_lines) + "\n").encode("utf-8")
        zf.writestr(record_path, record_data)

    return out_path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--project-name", required=True)
    parser.add_argument("--package-dir", required=True)
    parser.add_argument("--summary", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--out", required=True, help="dist 输出目录")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    out_dir = Path(args.out).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    version = args.version.strip()
    if not version:
        raise SystemExit("version 不能为空")

    sdist = build_sdist(args.project_name, version, project_root, args.package_dir, out_dir)
    wheel = build_wheel(args.project_name, version, project_root, args.package_dir, args.summary, out_dir)

    print(str(sdist))
    print(str(wheel))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
