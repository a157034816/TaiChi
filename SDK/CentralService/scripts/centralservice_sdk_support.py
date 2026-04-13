from __future__ import annotations

import hashlib
import shutil
import subprocess
import zipfile
from datetime import datetime
from pathlib import Path
from typing import Any
from xml.sax.saxutils import escape

from centralservice_script_common import (
    current_python_executable,
    ensure_dir,
    normalize_base_url,
    require_command,
    resolve_java_command,
    run_command,
    set_dotnet_env,
    set_go_env,
    set_node_env,
    write_info,
)


VALID_LANGUAGES = ["dotnet", "javascript", "python", "rust", "java", "go"]
VALID_SDK_KINDS = ["service", "client"]


def get_repo_root(script_dir: Path) -> Path:
    """Resolve the repository root from the scripts directory."""
    return script_dir.parents[3]


def get_sdk_root(script_dir: Path) -> Path:
    """Resolve the CentralService SDK root."""
    return script_dir.parent


def read_version(sdk_root: Path) -> str:
    """Read the SDK version file."""
    version_path = sdk_root / "VERSION"
    if not version_path.exists():
        raise RuntimeError(f"VERSION file not found: {version_path}")
    return version_path.read_text(encoding="utf-8").strip()


def reset_dist(sdk_root: Path) -> Path:
    """Recreate the dist directory from scratch."""
    dist_path = sdk_root / "dist"
    shutil.rmtree(dist_path, ignore_errors=True)
    ensure_dir(dist_path)
    return dist_path


def get_artifact_dir(dist_path: Path, language: str, sdk_kind: str) -> Path:
    """Resolve the target artifact directory."""
    return ensure_dir(dist_path / language / sdk_kind)


def get_dotnet_project(sdk_root: Path, kind: str) -> Path:
    """Return the .NET project path for a specific SDK kind."""
    if kind == "service":
        return sdk_root / "dotnet" / "src" / "CentralService.Service" / "CentralService.Service.csproj"
    return sdk_root / "dotnet" / "src" / "CentralService.Client" / "CentralService.Client.csproj"


def clear_dotnet_project_artifacts(project_path: Path) -> None:
    """Remove bin/obj to keep .NET builds deterministic."""
    project_dir = project_path.parent
    for artifact_name in ("bin", "obj"):
        shutil.rmtree(project_dir / artifact_name, ignore_errors=True)


def build_dotnet_sdk(repo_root: Path, sdk_root: Path, configuration: str, kinds: list[str]) -> None:
    """Build the .NET SDK variants."""
    require_command("dotnet", "Install .NET SDK and ensure 'dotnet' is in PATH.")
    env = set_dotnet_env(repo_root)
    net40_build = sdk_root / "dotnet" / "net40" / "build.ps1"
    if net40_build.exists():
        require_command("powershell", "Windows PowerShell is required for the legacy net40 build script.")
        run_command(["powershell", "-ExecutionPolicy", "Bypass", "-File", str(net40_build), "-Configuration", configuration], cwd=sdk_root / "dotnet", env=None, action="net40 build")

    for kind in kinds:
        project = get_dotnet_project(sdk_root, kind)
        clear_dotnet_project_artifacts(project)
        for framework in ("netstandard2.0", "net6.0", "net10.0"):
            run_command(["dotnet", "build", str(project), "-c", configuration, "-f", framework], cwd=sdk_root / "dotnet", env=env, action=f"dotnet build {kind} {framework}")


def build_javascript(sdk_root: Path, kinds: list[str]) -> None:
    """Validate JavaScript package entry points."""
    require_command("node", "Install Node.js and ensure 'node' is in PATH.")
    for kind in kinds:
        project_dir = sdk_root / "javascript" / kind
        run_command(["node", "-e", "require('./src/index.js')"], cwd=project_dir, env=None, action=f"node require {kind}")


def build_python(sdk_root: Path, kinds: list[str]) -> None:
    """Run compileall for Python packages."""
    require_command("python", "Install Python 3.10+ and ensure 'python' is in PATH.")
    python_exe = current_python_executable()
    for kind in kinds:
        project_dir = sdk_root / "python" / kind
        package_dir = "erp_centralservice_service" if kind == "service" else "erp_centralservice_client"
        run_command([python_exe, "-X", "utf8", "-m", "compileall", package_dir], cwd=project_dir, env=None, action=f"python compileall {kind}")


def build_rust(sdk_root: Path, kinds: list[str]) -> None:
    """Build Rust packages."""
    require_command("cargo", "Install Rust and ensure 'cargo' is in PATH.")
    packages: list[str] = []
    if "service" in kinds:
        packages.append("centralservice_service")
    if "client" in kinds:
        packages.append("centralservice_client")
    for package in packages:
        run_command(["cargo", "build", "--release", "-p", package], cwd=sdk_root / "rust", env=None, action=f"cargo build {package}")


def build_java_kind(sdk_root: Path, kind: str) -> Path:
    """Compile Java classes for one SDK kind."""
    java_root = sdk_root / "java"
    source_root = java_root / kind / "src" / "main" / "java"
    if not source_root.exists():
        raise RuntimeError(f"Java source root not found: {source_root}")

    javac = resolve_java_command("javac.exe")
    classes_dir = java_root / "build" / kind / "classes"
    shutil.rmtree(classes_dir, ignore_errors=True)
    ensure_dir(classes_dir)
    sources = [str(path) for path in source_root.rglob("*.java")]
    run_command([javac, "-encoding", "UTF-8", "-source", "8", "-target", "8", "-d", str(classes_dir), *sources], cwd=java_root, env=None, action=f"javac {kind}")
    return classes_dir


def build_java(sdk_root: Path, kinds: list[str]) -> None:
    """Compile Java classes for all requested kinds."""
    for kind in kinds:
        build_java_kind(sdk_root, kind)


def build_go(repo_root: Path, sdk_root: Path, kinds: list[str]) -> None:
    """Build Go modules for all requested kinds."""
    require_command("go", "Install Go 1.20+ and ensure 'go' is in PATH.")
    for kind in kinds:
        env = set_go_env(repo_root)
        run_command(["go", "build", "./..."], cwd=sdk_root / "go" / kind, env=env, action=f"go build {kind}")


def get_dotnet_assembly_name(kind: str) -> str:
    """Return the .NET assembly name for one SDK kind."""
    return "CentralService.Service" if kind == "service" else "CentralService.Client"


def get_dotnet_package_variants(kind: str) -> list[dict[str, str]]:
    """Return the modern and net40 package metadata."""
    assembly_name = get_dotnet_assembly_name(kind)
    title = "CentralService Service" if kind == "service" else "CentralService Client"
    description = (
        "CentralService service project (register/websocket-heartbeat/deregister)."
        if kind == "service"
        else "CentralService client project (list/discover/network)."
    )
    return [
        {
            "Flavor": "modern",
            "PackageId": assembly_name,
            "AssemblyName": assembly_name,
            "Title": title,
            "Description": description,
            "Authors": "ERP",
            "LicenseExpression": "MIT",
            "ProjectUrl": "https://example.invalid/erp",
            "Tags": "centralservice;service-client;modern",
        },
        {
            "Flavor": "net40",
            "PackageId": f"{assembly_name}.Net40",
            "AssemblyName": assembly_name,
            "Title": f"{title} Net40",
            "Description": f"{description} Dedicated .NET Framework 4.0 package.",
            "Authors": "ERP",
            "LicenseExpression": "MIT",
            "ProjectUrl": "https://example.invalid/erp",
            "Tags": "centralservice;service-client;net40",
        },
    ]


def get_dotnet_package_files(sdk_root: Path, kind: str, configuration: str, variant: dict[str, str]) -> list[dict[str, Path | str]]:
    """Return all .NET package payload files for one variant."""
    assembly_name = variant["AssemblyName"]
    project_dir = get_dotnet_project(sdk_root, kind).parent
    files: list[dict[str, Path | str]] = []
    if variant["Flavor"] == "modern":
        for framework in ("netstandard2.0", "net6.0", "net10.0"):
            for extension in ("dll", "xml"):
                source_path = project_dir / "bin" / configuration / framework / f"{assembly_name}.{extension}"
                if not source_path.exists():
                    raise RuntimeError(f"Missing .NET artifact: {source_path}")
                files.append({"SourcePath": source_path, "PackagePath": f"lib/{framework}/{assembly_name}.{extension}"})
        return files

    net40_dir = sdk_root / "dotnet" / "net40" / "bin" / "Release" / "net40"
    for extension in ("dll", "xml"):
        source_path = net40_dir / f"{assembly_name}.{extension}"
        if not source_path.exists():
            raise RuntimeError(f"Missing .NET Framework artifact: {source_path}")
        files.append({"SourcePath": source_path, "PackagePath": f"lib/net40/{assembly_name}.{extension}"})
    return files


def add_zip_text_entry(archive: zipfile.ZipFile, entry_path: str, content: str) -> None:
    """Write a UTF-8 text entry into a zip archive."""
    archive.writestr(entry_path, content.encode("utf-8"))


def new_dotnet_package_archive(package_path: Path, metadata: dict[str, str], version: str, files: list[dict[str, Path | str]]) -> None:
    """Create a NuGet-compatible archive without relying on nuget.exe."""
    if package_path.exists():
        package_path.unlink()
    created = datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")
    core_props_name = f"{datetime.utcnow().strftime('%Y%m%d%H%M%S%f')}.psmdcp"
    nuspec_name = f"{metadata['PackageId']}.nuspec"
    nuspec = f"""<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>{escape(metadata['PackageId'])}</id>
    <version>{escape(version)}</version>
    <title>{escape(metadata['Title'])}</title>
    <authors>{escape(metadata['Authors'])}</authors>
    <license type="expression">{escape(metadata['LicenseExpression'])}</license>
    <projectUrl>{escape(metadata['ProjectUrl'])}</projectUrl>
    <description>{escape(metadata['Description'])}</description>
    <tags>{escape(metadata['Tags'])}</tags>
  </metadata>
</package>
"""
    content_types = """<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="dll" ContentType="application/octet" />
  <Default Extension="xml" ContentType="text/xml" />
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="psmdcp" ContentType="application/vnd.openxmlformats-package.core-properties+xml" />
  <Default Extension="nuspec" ContentType="application/octet" />
</Types>
"""
    rels = f"""<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/{nuspec_name}" Id="R1" />
  <Relationship Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="/package/services/metadata/core-properties/{core_props_name}" Id="R2" />
</Relationships>
"""
    core_properties = f"""<?xml version="1.0" encoding="utf-8"?>
<coreProperties xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:creator>{escape(metadata['Authors'])}</dc:creator>
  <dc:description>{escape(metadata['Description'])}</dc:description>
  <dc:identifier>{escape(metadata['PackageId'])}</dc:identifier>
  <version>{escape(version)}</version>
  <keywords>{escape(metadata['Tags'])}</keywords>
  <title>{escape(metadata['Title'])}</title>
  <lastModifiedBy>{escape(metadata['Authors'])}</lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">{created}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">{created}</dcterms:modified>
</coreProperties>
"""
    with zipfile.ZipFile(package_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        add_zip_text_entry(archive, nuspec_name, nuspec)
        add_zip_text_entry(archive, "[Content_Types].xml", content_types)
        add_zip_text_entry(archive, "_rels/.rels", rels)
        add_zip_text_entry(archive, f"package/services/metadata/core-properties/{core_props_name}", core_properties)
        for file in files:
            archive.write(str(file["SourcePath"]), arcname=str(file["PackagePath"]))


def pack_dotnet_kind(sdk_root: Path, dist_path: Path, version: str, configuration: str, kind: str) -> None:
    """Package .NET artifacts for one kind."""
    artifact_dir = get_artifact_dir(dist_path, "dotnet", kind)
    for path in artifact_dir.glob("*"):
        if path.is_file():
            path.unlink()
    for variant in get_dotnet_package_variants(kind):
        files = get_dotnet_package_files(sdk_root, kind, configuration, variant)
        package_path = artifact_dir / f"{variant['PackageId']}.{version}.nupkg"
        new_dotnet_package_archive(package_path, variant, version, files)


def pack_javascript_kind(sdk_root: Path, dist_path: Path, kind: str, repo_root: Path) -> None:
    """Pack one JavaScript package."""
    require_command("npm", "Install npm and ensure 'npm' is in PATH.")
    project_dir = sdk_root / "javascript" / kind
    artifact_dir = get_artifact_dir(dist_path, "javascript", kind)
    env = set_node_env(repo_root)
    completed = run_command(["npm", "pack"], cwd=project_dir, env=env, action=f"npm pack {kind}", capture_output=True)
    archive_name = completed.stdout.strip().splitlines()[-1].strip()
    source_path = project_dir / archive_name
    target_path = artifact_dir / archive_name
    if target_path.exists():
        target_path.unlink()
    shutil.move(str(source_path), str(target_path))


def pack_python_kind(sdk_root: Path, dist_path: Path, version: str, kind: str) -> None:
    """Build Python sdist/wheel artifacts."""
    require_command("python", "Install Python 3.10+ and ensure 'python' is in PATH.")
    artifact_dir = get_artifact_dir(dist_path, "python", kind)
    project_dir = sdk_root / "python" / kind
    package_dir = "erp_centralservice_service" if kind == "service" else "erp_centralservice_client"
    project_name = "erp-centralservice-service" if kind == "service" else "erp-centralservice-client"
    summary = (
        "CentralService service project (register/websocket-heartbeat/deregister)."
        if kind == "service"
        else "CentralService client project (list/discover/network)."
    )
    run_command(
        [
            current_python_executable(),
            "-X",
            "utf8",
            str(sdk_root / "python" / "scripts" / "build_dist.py"),
            "--project-root",
            str(project_dir),
            "--project-name",
            project_name,
            "--package-dir",
            package_dir,
            "--summary",
            summary,
            "--version",
            version,
            "--out",
            str(artifact_dir),
        ],
        cwd=sdk_root,
        env=None,
        action=f"python pack {kind}",
    )


def pack_rust_kind(sdk_root: Path, dist_path: Path, version: str, kind: str) -> None:
    """Build and collect one Rust crate."""
    require_command("cargo", "Install Rust and ensure 'cargo' is in PATH.")
    rust_dir = sdk_root / "rust"
    project_dir = rust_dir / kind
    crate_name = "centralservice_service" if kind == "service" else "centralservice_client"
    artifact_name = f"{crate_name}-{version}.crate"
    artifact_dir = get_artifact_dir(dist_path, "rust", kind)
    run_command(["cargo", "package", "--allow-dirty", "--no-verify"], cwd=project_dir, env=None, action=f"cargo package {kind}")
    matches = sorted(rust_dir.rglob(artifact_name), key=lambda path: path.stat().st_mtime, reverse=True)
    if not matches:
        raise RuntimeError(f"Rust crate not found: {artifact_name}")
    shutil.copy2(matches[0], artifact_dir / artifact_name)


def pack_java_kind(sdk_root: Path, dist_path: Path, version: str, kind: str) -> None:
    """Build and package Java classes into a jar."""
    classes_dir = build_java_kind(sdk_root, kind)
    artifact_dir = get_artifact_dir(dist_path, "java", kind)
    jar = resolve_java_command("jar.exe")
    jar_path = artifact_dir / f"erp-centralservice-{kind}-java-{version}.jar"
    if jar_path.exists():
        jar_path.unlink()
    run_command([jar, "cf", str(jar_path), "."], cwd=classes_dir, env=None, action=f"jar {kind}")


def pack_go_kind(sdk_root: Path, dist_path: Path, version: str, kind: str) -> None:
    """Zip the Go project directory."""
    project_dir = sdk_root / "go" / kind
    artifact_dir = get_artifact_dir(dist_path, "go", kind)
    zip_path = artifact_dir / f"centralservice-{kind}-go-{version}.zip"
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(project_dir.rglob("*")):
            archive.write(path, arcname=path.relative_to(project_dir))


def get_artifact_sha256(path: Path) -> str:
    """Calculate a lowercase SHA-256 hash."""
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def generate_manifest(dist_path: Path, version: str, base_url: str) -> None:
    """Generate dist/manifest.json."""
    manifest_path = dist_path / "manifest.json"
    if manifest_path.exists():
        manifest_path.unlink()
    artifacts = []
    for path in sorted(dist_path.rglob("*")):
        if not path.is_file() or path == manifest_path:
            continue
        relative = path.relative_to(dist_path).as_posix()
        parts = relative.split("/")
        artifacts.append(
            {
                "language": parts[0] if len(parts) >= 1 else "",
                "sdkKind": parts[1] if len(parts) >= 2 else "",
                "artifactPath": f"dist/{relative}",
                "size": path.stat().st_size,
                "sha256": get_artifact_sha256(path),
            }
        )
    payload = {
        "name": "CentralService SDK",
        "version": version,
        "builtAt": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "baseUrl": base_url,
        "artifacts": artifacts,
    }
    manifest_path.write_text(__import__("json").dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    write_info(f"Manifest generated: {manifest_path}")


def build_all(repo_root: Path, sdk_root: Path, langs: list[str], kinds: list[str]) -> None:
    """Run all requested build steps."""
    write_info(f"Build: {', '.join(langs)} | kinds: {', '.join(kinds)}")
    if "dotnet" in langs:
        build_dotnet_sdk(repo_root, sdk_root, "Release", kinds)
    if "javascript" in langs:
        build_javascript(sdk_root, kinds)
    if "python" in langs:
        build_python(sdk_root, kinds)
    if "rust" in langs:
        build_rust(sdk_root, kinds)
    if "java" in langs:
        build_java(sdk_root, kinds)
    if "go" in langs:
        build_go(repo_root, sdk_root, kinds)


def run_e2e(repo_root: Path, sdk_root: Path, langs: list[str], base_url: str) -> None:
    """Delegate E2E execution to e2e.py."""
    run_command(
        [
            current_python_executable(),
            "-X",
            "utf8",
            str(sdk_root / "scripts" / "e2e.py"),
            "-BaseUrl",
            base_url,
            "-RepoRoot",
            str(repo_root),
            "-Languages",
            ",".join(langs),
        ],
        cwd=sdk_root,
        env=None,
        action="e2e.py",
    )


def pack_all(repo_root: Path, sdk_root: Path, langs: list[str], kinds: list[str], base_url: str) -> None:
    """Run all requested packaging steps and emit the manifest."""
    dist_path = reset_dist(sdk_root)
    version = read_version(sdk_root)
    if "dotnet" in langs:
        build_dotnet_sdk(repo_root, sdk_root, "Release", kinds)
        for kind in kinds:
            pack_dotnet_kind(sdk_root, dist_path, version, "Release", kind)
    if "javascript" in langs:
        build_javascript(sdk_root, kinds)
        for kind in kinds:
            pack_javascript_kind(sdk_root, dist_path, kind, repo_root)
    if "python" in langs:
        build_python(sdk_root, kinds)
        for kind in kinds:
            pack_python_kind(sdk_root, dist_path, version, kind)
    if "rust" in langs:
        build_rust(sdk_root, kinds)
        for kind in kinds:
            pack_rust_kind(sdk_root, dist_path, version, kind)
    if "java" in langs:
        for kind in kinds:
            pack_java_kind(sdk_root, dist_path, version, kind)
    if "go" in langs:
        build_go(repo_root, sdk_root, kinds)
        for kind in kinds:
            pack_go_kind(sdk_root, dist_path, version, kind)
    generate_manifest(dist_path, version, normalize_base_url(base_url))
