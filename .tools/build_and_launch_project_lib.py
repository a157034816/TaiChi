#!/usr/bin/env python3
"""通用构建启动器核心逻辑。"""

from __future__ import annotations

import ctypes
import json
import os
import re
import shutil
import subprocess
import xml.etree.ElementTree as element_tree
from dataclasses import dataclass
from pathlib import Path


SOLUTION_FILE_NAME = "TaiChi.sln"
EXE_OUTPUT_TYPES = {"exe", "winexe"}
EXCLUDED_SEGMENTS = {"tests", "examples"}
INCOMPATIBLE_TFM_MARKERS = ("-android", "-browser", "-ios", "-linux", "-maccatalyst", "-tizen")
DEFAULT_ARGUMENT_MODE = "default"
LAUNCH_SETTINGS_ARGUMENT_MODE = "launch_settings"
EMPTY_ARGUMENT_MODE = "empty"
CUSTOM_ARGUMENT_MODE = "custom"
ARGUMENT_MODE_OPTIONS = (
    (DEFAULT_ARGUMENT_MODE, "使用默认启动参数"),
    (LAUNCH_SETTINGS_ARGUMENT_MODE, "使用launchSettings.json提供的启动参数"),
    (EMPTY_ARGUMENT_MODE, "使用无启动参数"),
    (CUSTOM_ARGUMENT_MODE, "自定义输入启动参数"),
)
LEGACY_DEFAULT_ARGUMENTS = {
    "CentralService": '--urls "https://0.0.0.0:25700;http://0.0.0.0:15700"',
}
SENSITIVE_SWITCHES = {
    "--db-password",
    "--db-pwd",
    "--sinour-db-password",
    "--sinour-db-pwd",
}


class LauncherError(RuntimeError):
    """启动器业务错误。"""


@dataclass(frozen=True)
class LaunchProfile:
    """启动配置。"""

    name: str
    command_line_args: str


@dataclass(frozen=True)
class ProjectInfo:
    """可启动项目。"""

    name: str
    relative_path: Path
    absolute_path: Path
    output_type: str
    assembly_name: str
    configurations: tuple[str, ...]
    target_frameworks: tuple[str, ...]
    preferred_target_framework: str | None
    preferred_platform: str | None
    launch_profiles: tuple[LaunchProfile, ...]


@dataclass(frozen=True)
class ProjectMetadata:
    """项目元数据。"""

    output_type: str
    assembly_name: str
    configurations: tuple[str, ...]
    target_frameworks: tuple[str, ...]
    preferred_target_framework: str | None
    preferred_platform: str | None


@dataclass(frozen=True)
class ArgumentSelection:
    """启动参数选择结果。"""

    arguments: str
    mode_label: str
    source_label: str


@dataclass(frozen=True)
class RunDescriptor:
    """启动命令描述。"""

    command: tuple[str, ...]
    working_directory: Path
    resolved_from: str


def main() -> int:
    """运行通用启动器。"""

    try:
        repo_root = find_repo_root(Path(__file__).resolve().parent)
        solution_path = repo_root / SOLUTION_FILE_NAME
        solution_configurations = parse_solution_configurations(solution_path)
        projects = discover_runnable_projects(solution_path, solution_configurations)
        if not projects:
            raise LauncherError("未在解决方案中找到可启动项目。")

        msbuild_executable = resolve_msbuild_executable()
        print("==== TaiChi 通用构建启动器 ====")
        print(f"仓库根目录: {repo_root}")
        print(f"解决方案: {solution_path.name}")
        print(f"MSBuild: {msbuild_executable}")
        print()

        project = select_project(projects)
        configuration = select_configuration(project)
        argument_selection = select_startup_arguments(project)
        print_selection_summary(project, configuration, argument_selection)

        build_project(msbuild_executable, project, configuration)
        run_descriptor = resolve_run_descriptor(
            msbuild_executable,
            project,
            configuration,
            argument_selection.arguments,
        )
        launch_process(run_descriptor)
        print()
        print("启动命令已发出。")
        print(f"工作目录: {run_descriptor.working_directory}")
        print(f"命令来源: {run_descriptor.resolved_from}")
        return 0
    except KeyboardInterrupt:
        print()
        print("已取消操作。")
        return 130
    except LauncherError as error:
        print(f"错误: {error}")
        return 1


def find_repo_root(start_directory: Path) -> Path:
    """向上查找仓库根目录。"""

    current = start_directory.resolve()
    while True:
        if (current / SOLUTION_FILE_NAME).exists():
            return current
        if current.parent == current:
            break
        current = current.parent
    raise LauncherError(f"未找到 {SOLUTION_FILE_NAME}，无法定位仓库根目录。")


def discover_runnable_projects(
    solution_path: Path,
    fallback_configurations: tuple[str, ...],
) -> list[ProjectInfo]:
    """发现解决方案中的可执行项目。"""

    repo_root = solution_path.parent
    projects: list[ProjectInfo] = []
    for project_name, relative_project_path in parse_solution_projects(solution_path):
        if is_excluded_project(relative_project_path):
            continue
        absolute_project_path = (repo_root / relative_project_path).resolve()
        if not absolute_project_path.exists():
            continue

        metadata = parse_project_metadata(absolute_project_path, fallback_configurations)
        if metadata.output_type.casefold() not in EXE_OUTPUT_TYPES:
            continue
        if metadata.preferred_target_framework is None:
            continue

        projects.append(
            ProjectInfo(
                name=project_name,
                relative_path=relative_project_path,
                absolute_path=absolute_project_path,
                output_type=metadata.output_type,
                assembly_name=metadata.assembly_name,
                configurations=metadata.configurations,
                target_frameworks=metadata.target_frameworks,
                preferred_target_framework=metadata.preferred_target_framework,
                preferred_platform=metadata.preferred_platform,
                launch_profiles=load_launch_profiles(absolute_project_path.parent),
            )
        )
    return sorted(projects, key=lambda item: item.name.casefold())


def parse_solution_projects(solution_path: Path) -> list[tuple[str, Path]]:
    """解析解决方案中的 csproj 列表。"""

    pattern = re.compile(r'^Project\(".*?"\)\s*=\s*"(?P<name>.*?)",\s*"(?P<path>.*?)",\s*".*?"$')
    projects: list[tuple[str, Path]] = []
    for line in read_text(solution_path).splitlines():
        match = pattern.match(line.strip())
        if not match:
            continue
        relative_path = match.group("path").replace("\\", "/")
        if relative_path.casefold().endswith(".csproj"):
            projects.append((match.group("name"), Path(relative_path)))
    return projects


def parse_solution_configurations(solution_path: Path) -> tuple[str, ...]:
    """解析解决方案级构建配置。"""

    configurations: list[str] = []
    in_section = False
    for line in read_text(solution_path).splitlines():
        stripped = line.strip()
        if stripped.startswith("GlobalSection(SolutionConfigurationPlatforms)"):
            in_section = True
            continue
        if in_section and stripped.startswith("EndGlobalSection"):
            break
        if not in_section or "=" not in stripped:
            continue
        configuration = stripped.split("=", 1)[0].split("|", 1)[0].strip()
        if configuration and configuration not in configurations:
            configurations.append(configuration)
    return tuple(sorted(configurations, key=str.casefold))


def parse_project_metadata(
    project_path: Path,
    fallback_configurations: tuple[str, ...],
) -> ProjectMetadata:
    """解析项目文件中的启动相关元数据。"""

    root = element_tree.parse(project_path).getroot()
    output_type = first_xml_text(root, "OutputType") or default_output_type_for_project(root)
    assembly_name = first_xml_text(root, "AssemblyName") or project_path.stem
    platforms = split_semicolon_list(first_xml_text(root, "Platforms"))
    configurations = split_semicolon_list(first_xml_text(root, "Configurations")) or list(fallback_configurations)
    if not configurations:
        configurations = ["Debug", "Release"]

    target_frameworks = split_semicolon_list(first_xml_text(root, "TargetFrameworks"))
    if not target_frameworks:
        single_target_framework = first_xml_text(root, "TargetFramework")
        if single_target_framework:
            target_frameworks = [single_target_framework]

    return ProjectMetadata(
        output_type=output_type,
        assembly_name=assembly_name,
        configurations=tuple(sorted(set(configurations), key=str.casefold)),
        target_frameworks=tuple(target_frameworks),
        preferred_target_framework=choose_preferred_target_framework(target_frameworks),
        preferred_platform=choose_preferred_platform(platforms),
    )


def default_output_type_for_project(root: element_tree.Element) -> str:
    """为未显式声明 OutputType 的项目推断默认输出类型。"""

    sdk_name = str(root.attrib.get("Sdk", "") or "").casefold()
    if "microsoft.net.sdk.web" in sdk_name or "microsoft.net.sdk.worker" in sdk_name:
        return "Exe"
    return ""


def load_launch_profiles(project_directory: Path) -> tuple[LaunchProfile, ...]:
    """读取 launchSettings 中可用的 Project profile。"""

    launch_settings_path = project_directory / "Properties" / "launchSettings.json"
    if not launch_settings_path.exists():
        return ()
    try:
        payload = json.loads(read_text(launch_settings_path))
    except (OSError, json.JSONDecodeError):
        return ()

    profiles: list[LaunchProfile] = []
    for profile_name, profile_value in payload.get("profiles", {}).items():
        if not isinstance(profile_value, dict):
            continue
        if str(profile_value.get("commandName", "")).casefold() != "project":
            continue
        profiles.append(LaunchProfile(profile_name, str(profile_value.get("commandLineArgs", "") or "")))
    return tuple(sorted(profiles, key=lambda item: item.name.casefold()))


def choose_preferred_target_framework(target_frameworks: list[str] | tuple[str, ...]) -> str | None:
    """选择当前 Windows 主机优先使用的目标框架。"""

    compatible = [item for item in target_frameworks if is_windows_compatible_framework(item)]
    if not compatible:
        return None
    windows_specific = [item for item in compatible if "-windows" in item.casefold()]
    return windows_specific[0] if windows_specific else compatible[0]


def choose_preferred_platform(platforms: list[str]) -> str | None:
    """选择优先平台。"""

    if not platforms:
        return None
    for candidate in platforms:
        if candidate.casefold() == "anycpu":
            return candidate
    return platforms[0]


def is_windows_compatible_framework(target_framework: str) -> bool:
    """判断目标框架是否兼容当前 Windows 主机。"""

    lowered = target_framework.casefold()
    return not any(marker in lowered for marker in INCOMPATIBLE_TFM_MARKERS)


def is_excluded_project(relative_project_path: Path) -> bool:
    """判断项目是否应排除在菜单外。"""

    filename = relative_project_path.name.casefold()
    stem = relative_project_path.stem.casefold()
    if filename.endswith(".tests.csproj") or stem.endswith(".tests") or "example" in stem:
        return True
    for part in relative_project_path.parts:
        lowered = part.casefold()
        if lowered in EXCLUDED_SEGMENTS or lowered.endswith(".tests") or "example" in lowered:
            return True
    return False


def resolve_msbuild_executable() -> Path:
    """解析可用的 MSBuild 路径。"""

    override_label = None
    override = os.environ.get("TAICHI_MSBUILD_EXE")
    if override:
        override_label = "TAICHI_MSBUILD_EXE"
    else:
        override = os.environ.get("ERP_MSBUILD_EXE")
        if override:
            override_label = "ERP_MSBUILD_EXE"

    if override:
        candidate = Path(override).expanduser()
        if candidate.exists():
            return candidate.resolve()
        raise LauncherError(f"{override_label} 指向的 MSBuild 不存在: {candidate}")

    vswhere_path = resolve_vswhere_path()
    if vswhere_path:
        result = subprocess.run(
            [
                str(vswhere_path),
                "-latest",
                "-products",
                "*",
                "-requires",
                "Microsoft.Component.MSBuild",
                "-find",
                r"MSBuild\**\Bin\MSBuild.exe",
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
        if result.returncode == 0:
            for line in result.stdout.splitlines():
                candidate = Path(line.strip())
                if candidate.exists():
                    return candidate.resolve()

    known_candidates = [
        Path(r"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\17\Community\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\17\Professional\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\17\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
        Path(r"C:\Program Files\Microsoft Visual Studio\17\BuildTools\MSBuild\Current\Bin\MSBuild.exe"),
    ]
    for candidate in known_candidates:
        if candidate.exists():
            return candidate.resolve()

    path_candidate = shutil.which("MSBuild.exe")
    if path_candidate:
        return Path(path_candidate).resolve()
    raise LauncherError(
        "未找到可用的 MSBuild.exe，请先安装 Visual Studio/MSBuild 或设置 TAICHI_MSBUILD_EXE/ERP_MSBUILD_EXE。"
    )


def resolve_vswhere_path() -> Path | None:
    """解析 vswhere 可执行文件。"""

    vswhere_on_path = shutil.which("vswhere.exe")
    if vswhere_on_path:
        return Path(vswhere_on_path)
    program_files_x86 = os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")
    candidate = Path(program_files_x86) / "Microsoft Visual Studio" / "Installer" / "vswhere.exe"
    return candidate if candidate.exists() else None


def select_project(projects: list[ProjectInfo]) -> ProjectInfo:
    """选择启动项目。"""

    options = [
        f"{project.name} | {project.relative_path.as_posix()} | {project.preferred_target_framework}"
        for project in projects
    ]
    return projects[prompt_for_choice("第 1 步：选择项目", options)]


def select_configuration(project: ProjectInfo) -> str:
    """选择构建配置。"""

    default_index = next(
        (index for index, configuration in enumerate(project.configurations) if configuration.casefold() == "debug"),
        None,
    )
    return project.configurations[
        prompt_for_choice("第 2 步：选择构建配置", list(project.configurations), default_index)
    ]


def select_startup_arguments(project: ProjectInfo) -> ArgumentSelection:
    """选择启动参数模式并返回最终参数。"""

    default_index = next(
        index for index, (mode_key, _) in enumerate(ARGUMENT_MODE_OPTIONS) if mode_key == DEFAULT_ARGUMENT_MODE
    )
    while True:
        index = prompt_for_choice(
            "第 3 步：选择启动参数模式",
            [item[1] for item in ARGUMENT_MODE_OPTIONS],
            default_index,
        )
        mode_key, mode_label = ARGUMENT_MODE_OPTIONS[index]
        if mode_key == DEFAULT_ARGUMENT_MODE:
            return build_default_argument_selection(project.name, mode_label)
        if mode_key == LAUNCH_SETTINGS_ARGUMENT_MODE:
            selection = select_launch_settings_arguments(project, mode_label)
            if selection is not None:
                return selection
            continue
        if mode_key == EMPTY_ARGUMENT_MODE:
            return ArgumentSelection("", mode_label, "空参数")
        if mode_key == CUSTOM_ARGUMENT_MODE:
            return build_custom_argument_selection(mode_label)
        raise LauncherError(f"不支持的启动参数模式: {mode_key}")


def build_default_argument_selection(project_name: str, mode_label: str) -> ArgumentSelection:
    """构造旧 BAT 默认参数选择结果。"""

    arguments = LEGACY_DEFAULT_ARGUMENTS.get(project_name, "")
    if project_name not in LEGACY_DEFAULT_ARGUMENTS:
        print("当前项目未配置默认启动参数，将按空参数启动。")
    return ArgumentSelection(arguments, mode_label, "旧BAT默认参数")


def select_launch_settings_arguments(
    project: ProjectInfo,
    mode_label: str,
) -> ArgumentSelection | None:
    """按 launchSettings 模式选择参数。"""

    if not project.launch_profiles:
        print("当前项目没有可用的 launchSettings 启动参数，请重新选择启动参数模式。")
        print()
        return None

    options = [
        f"{profile.name} | 参数: {format_argument_preview(profile.command_line_args)}"
        for profile in project.launch_profiles
    ]
    selected_profile = project.launch_profiles[prompt_for_choice("launchSettings 模式：选择配置", options)]
    arguments = selected_profile.command_line_args.strip()
    if not arguments:
        print("当前 launchSettings 配置没有启动参数，将按空参数启动。")
    return ArgumentSelection(arguments, mode_label, f"launchSettings:{selected_profile.name}")


def build_custom_argument_selection(mode_label: str) -> ArgumentSelection:
    """读取自定义启动参数。"""

    while True:
        custom_arguments = input("请输入自定义启动参数: ").strip()
        if custom_arguments:
            return ArgumentSelection(custom_arguments, mode_label, "自定义输入参数")
        print("自定义启动参数不能为空，请重新输入。")


def prompt_for_choice(title: str, options: list[str], default_index: int | None = None) -> int:
    """输出序号菜单并读取选择。"""

    while True:
        print(title)
        for index, option in enumerate(options, start=1):
            suffix = " (默认)" if default_index == index - 1 else ""
            print(f"  {index}. {option}{suffix}")
        prompt = f"请输入序号 (1-{len(options)})"
        if default_index is not None:
            prompt += f"，回车默认 {default_index + 1}"
        selected = input(f"{prompt}: ").strip()
        if not selected and default_index is not None:
            return default_index
        if selected.isdigit():
            numeric_index = int(selected)
            if 1 <= numeric_index <= len(options):
                return numeric_index - 1
        print("输入的序号无效，请重新输入。")
        print()


def print_selection_summary(
    project: ProjectInfo,
    configuration: str,
    argument_selection: ArgumentSelection,
) -> None:
    """输出本次启动配置摘要。"""

    print()
    print("已选择:")
    print(f"  项目: {project.name}")
    print(f"  配置: {configuration}")
    print(f"  启动参数模式: {argument_selection.mode_label}")
    print(f"  参数来源: {argument_selection.source_label}")
    print(f"  启动参数: {format_argument_preview(argument_selection.arguments)}")
    print()


def build_project(msbuild_executable: Path, project: ProjectInfo, configuration: str) -> None:
    """构建目标项目。"""

    command = [
        str(msbuild_executable),
        str(project.absolute_path),
        "/t:Build",
        "/nologo",
        "/verbosity:minimal",
        f"/p:Configuration={configuration}",
    ]
    if project.preferred_platform:
        command.append(f"/p:Platform={project.preferred_platform}")
    if project.preferred_target_framework:
        command.append(f"/p:TargetFramework={project.preferred_target_framework}")

    print("[1/3] 正在构建项目...")
    result = subprocess.run(command, check=False)
    if result.returncode != 0:
        raise LauncherError(f"项目构建失败，退出码: {result.returncode}")


def resolve_run_descriptor(
    msbuild_executable: Path,
    project: ProjectInfo,
    configuration: str,
    startup_arguments: str,
) -> RunDescriptor:
    """解析最终启动命令。"""

    print("[2/3] 正在解析启动目标...")
    resolved = try_resolve_run_descriptor_from_msbuild(
        msbuild_executable,
        project,
        configuration,
        startup_arguments,
    )
    if resolved is not None:
        return resolved
    return resolve_run_descriptor_from_output(project, configuration, startup_arguments)


def try_resolve_run_descriptor_from_msbuild(
    msbuild_executable: Path,
    project: ProjectInfo,
    configuration: str,
    startup_arguments: str,
) -> RunDescriptor | None:
    """优先使用 MSBuild 属性解析启动命令。"""

    command = [
        str(msbuild_executable),
        str(project.absolute_path),
        "-nologo",
        "-verbosity:quiet",
        "-getProperty:RunCommand,RunArguments,RunWorkingDirectory,TargetPath,TargetFramework",
        f"-property:Configuration={configuration}",
    ]
    if project.preferred_platform:
        command.append(f"-property:Platform={project.preferred_platform}")
    if project.preferred_target_framework:
        command.append(f"-property:TargetFramework={project.preferred_target_framework}")

    result = subprocess.run(
        command,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if result.returncode != 0:
        return None
    try:
        payload = json.loads(result.stdout.strip() or "{}")
    except json.JSONDecodeError:
        return None

    properties = payload.get("Properties", {})
    run_command = str(properties.get("RunCommand", "") or "").strip()
    run_arguments = str(properties.get("RunArguments", "") or "").strip()
    run_working_directory = str(properties.get("RunWorkingDirectory", "") or "").strip()
    target_path = str(properties.get("TargetPath", "") or "").strip()

    command_parts = split_windows_command_line(run_command)
    if run_arguments:
        command_parts.extend(split_windows_command_line(run_arguments))
    if startup_arguments:
        command_parts.extend(split_windows_command_line(startup_arguments))

    if command_parts:
        working_directory = (
            Path(run_working_directory).resolve()
            if run_working_directory
            else Path(target_path).resolve().parent if target_path
            else project.absolute_path.parent
        )
        return RunDescriptor(tuple(command_parts), working_directory, "MSBuild RunCommand")

    if target_path:
        target = Path(target_path).resolve()
        command_parts = build_command_from_target_path(target)
        if startup_arguments:
            command_parts.extend(split_windows_command_line(startup_arguments))
        return RunDescriptor(tuple(command_parts), target.parent, "MSBuild TargetPath")
    return None


def resolve_run_descriptor_from_output(
    project: ProjectInfo,
    configuration: str,
    startup_arguments: str,
) -> RunDescriptor:
    """从构建输出目录兜底解析命令。"""

    output_root = project.absolute_path.parent / "bin" / configuration
    if not output_root.exists():
        raise LauncherError(f"未找到构建输出目录: {output_root}")

    executable_candidates = list(output_root.rglob(f"{project.assembly_name}.exe"))
    dll_candidates = list(output_root.rglob(f"{project.assembly_name}.dll"))
    selected_target = choose_best_artifact(executable_candidates or dll_candidates, project.preferred_target_framework)
    if selected_target is None:
        raise LauncherError(f"未找到可启动产物，项目: {project.name}，配置: {configuration}。")

    command_parts = build_command_from_target_path(selected_target)
    if startup_arguments:
        command_parts.extend(split_windows_command_line(startup_arguments))
    return RunDescriptor(tuple(command_parts), selected_target.parent, "本地构建产物兜底")


def choose_best_artifact(candidates: list[Path], preferred_target_framework: str | None) -> Path | None:
    """挑选最佳产物。"""

    if not candidates:
        return None

    def sort_key(candidate: Path) -> tuple[int, int, str]:
        candidate_text = candidate.as_posix().casefold()
        framework_penalty = 0
        if preferred_target_framework and preferred_target_framework.casefold() not in candidate_text:
            framework_penalty = 1
        return (framework_penalty, len(candidate.parts), candidate_text)

    return sorted(candidates, key=sort_key)[0]


def build_command_from_target_path(target_path: Path) -> list[str]:
    """根据目标文件路径构造启动命令。"""

    if not target_path.exists():
        raise LauncherError(f"目标文件不存在: {target_path}")
    return ["dotnet", str(target_path)] if target_path.suffix.casefold() == ".dll" else [str(target_path)]


def launch_process(run_descriptor: RunDescriptor) -> None:
    """启动目标进程。"""

    print("[3/3] 正在启动进程...")
    print("        " + subprocess.list2cmdline(list(run_descriptor.command)))
    subprocess.Popen(list(run_descriptor.command), cwd=run_descriptor.working_directory)


def split_windows_command_line(command_line: str) -> list[str]:
    """按 Windows 规则拆分命令行。"""

    if not command_line or not command_line.strip():
        return []

    shell32 = ctypes.windll.shell32
    kernel32 = ctypes.windll.kernel32
    shell32.CommandLineToArgvW.argtypes = [ctypes.c_wchar_p, ctypes.POINTER(ctypes.c_int)]
    shell32.CommandLineToArgvW.restype = ctypes.POINTER(ctypes.c_wchar_p)
    kernel32.LocalFree.argtypes = [ctypes.c_void_p]
    kernel32.LocalFree.restype = ctypes.c_void_p

    argument_count = ctypes.c_int()
    argument_values = shell32.CommandLineToArgvW(command_line, ctypes.byref(argument_count))
    if not argument_values:
        raise LauncherError(f"无法解析命令行参数: {command_line}")
    try:
        return [argument_values[index] for index in range(argument_count.value)]
    finally:
        kernel32.LocalFree(argument_values)


def format_argument_preview(argument_text: str) -> str:
    """格式化启动参数预览。"""

    if not argument_text.strip():
        return "无"
    masked = mask_sensitive_arguments(argument_text)
    return masked if len(masked) <= 100 else masked[:97] + "..."


def mask_sensitive_arguments(argument_text: str) -> str:
    """隐藏敏感参数值。"""

    masked_parts: list[str] = []
    mask_next = False
    for part in split_windows_command_line(argument_text):
        lowered = part.casefold()
        if mask_next:
            masked_parts.append("***")
            mask_next = False
            continue
        if "=" in part:
            key, _ = part.split("=", 1)
            if key.casefold() in SENSITIVE_SWITCHES:
                masked_parts.append(f"{key}=***")
                continue
            masked_parts.append(part)
            continue
        masked_parts.append(part)
        if lowered in SENSITIVE_SWITCHES:
            mask_next = True
    return subprocess.list2cmdline(masked_parts)


def get_legacy_default_arguments(project_name: str) -> str:
    """返回旧 BAT 默认参数。"""

    return LEGACY_DEFAULT_ARGUMENTS.get(project_name, "")


def read_text(file_path: Path) -> str:
    """以 UTF-8 读取文本文件。"""

    return file_path.read_text(encoding="utf-8-sig")


def first_xml_text(root: element_tree.Element, local_name: str) -> str:
    """获取首个匹配 XML 节点文本。"""

    for element in root.iter():
        if xml_local_name(element.tag) == local_name and element.text and element.text.strip():
            return element.text.strip()
    return ""


def xml_local_name(tag_name: str) -> str:
    """获取 XML 标签本地名。"""

    return tag_name.rsplit("}", 1)[1] if "}" in tag_name else tag_name


def split_semicolon_list(raw_value: str) -> list[str]:
    """拆分分号列表。"""

    return [item.strip() for item in raw_value.split(";") if item.strip()] if raw_value else []
