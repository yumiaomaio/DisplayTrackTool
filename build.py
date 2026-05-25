"""
build.py - DisplayTrackTool 三层构建编排脚本

完整构建:
    python build.py

选择性构建:
    python build.py --frontend       # 仅构建前端
    python build.py --cpp            # 仅构建 C++ host.dll
    python build.py --csharp         # 仅构建 C#
    python build.py --skip-frontend  # 跳过前端（WebUI 已是最新时）

清理:
    python build.py --clean
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).parent.resolve()

# 各层目录
FRONTEND_DIR = ROOT / "vite-project"
CPP_DIR = ROOT / "webview-wrapper"
CPP_BUILD_DIR = CPP_DIR / "build"
CSHARP_DIR = ROOT / "ImmersiveDisplay"

# 产物路径
FRONTEND_OUTPUT = FRONTEND_DIR / "dist" / "index.html"
HOST_DLL_SOURCE = CPP_BUILD_DIR / "Release" / "host.dll"
# WebView2Loader.dll 由 CMake FetchContent 下载到 build/_deps/webview2-src/
WEBVIEW2_LOADER_SOURCE = (
    CPP_BUILD_DIR / "_deps" / "webview2-src" / "build" / "native" / "x64" / "WebView2Loader.dll"
)
DOTNET_PUBLISH_DIR = ROOT / "Release"


def log(msg: str):
    print(f"  {msg}")


def run(cmd, cwd=None, description="", check=True, env=None):
    """Run命令，实时输出，出错可退出."""
    print(f"\n>>> {description}")
    print(f"    {cmd if isinstance(cmd, str) else ' '.join(cmd)}")
    result = subprocess.run(
        cmd,
        cwd=cwd,
        shell=isinstance(cmd, str),
        env=env,
    )
    if check and result.returncode != 0:
        print(f"\n[FAIL] {description}")
        sys.exit(result.returncode)
    return result


# ---------------------------------------------------------------------------
# 前端
# ---------------------------------------------------------------------------

def build_frontend():
    print("\n=== [1/4] 构建前端 (Vue) ===")
    if not (FRONTEND_DIR / "node_modules").exists():
        run("npm install", cwd=FRONTEND_DIR, description="npm install")
    run("npm run build", cwd=FRONTEND_DIR, description="vite build")
    log(f"前端产物: {FRONTEND_OUTPUT}")


def deploy_frontend():
    """复制 index.html → 最终输出目录的 WebUI/"""
    if not FRONTEND_OUTPUT.exists():
        print(f"[ERROR] 前端产物不存在: {FRONTEND_OUTPUT}")
        print("  请先执行 python build.py --frontend")
        sys.exit(1)
    if not DOTNET_PUBLISH_DIR.exists():
        print(f"[ERROR] 输出目录不存在: {DOTNET_PUBLISH_DIR}")
        sys.exit(1)
    dest = DOTNET_PUBLISH_DIR / "WebUI" / "index.html"
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(FRONTEND_OUTPUT, dest)
    log(f"复制: {FRONTEND_OUTPUT.name} → {dest}")


# ---------------------------------------------------------------------------
# C++
# ---------------------------------------------------------------------------

def build_cpp():
    print("\n=== [2/4] 构建 C++ host.dll (CMake) ===")
    CPP_BUILD_DIR.mkdir(parents=True, exist_ok=True)
    if not (CPP_BUILD_DIR / "CMakeCache.txt").exists():
        run(["cmake", ".."], cwd=CPP_BUILD_DIR, description="CMake 配置")
    run(
        ["cmake", "--build", ".", "--config", "Release"],
        cwd=CPP_BUILD_DIR,
        description="CMake 编译",
    )
    log(f"C++ 产物: {HOST_DLL_SOURCE}")


# ---------------------------------------------------------------------------
# C#
# ---------------------------------------------------------------------------

def build_csharp():
    print("\n=== [3/4] 构建 C# ImmersiveDisplay.exe (dotnet publish) ===")
    run(
        ["dotnet", "publish", "--output", str(DOTNET_PUBLISH_DIR)],
        cwd=CSHARP_DIR,
        description="dotnet publish",
    )


def deploy_cpp_to_csharp():
    """复制 host.dll / WebView2Loader.dll → dotnet publish 输出目录"""
    if not DOTNET_PUBLISH_DIR.exists():
        print(f"[ERROR] dotnet publish 输出目录不存在: {DOTNET_PUBLISH_DIR}")
        print("  请先执行 python build.py --csharp")
        sys.exit(1)

    # host.dll
    if not HOST_DLL_SOURCE.exists():
        print(f"[ERROR] host.dll 不存在: {HOST_DLL_SOURCE}")
        print("  请先执行 python build.py --cpp")
        sys.exit(1)
    dest = DOTNET_PUBLISH_DIR / "host.dll"
    shutil.copy2(HOST_DLL_SOURCE, dest)
    log(f"复制: {HOST_DLL_SOURCE.name} → {dest}")

    # WebView2Loader.dll
    if not WEBVIEW2_LOADER_SOURCE.exists():
        print(f"[WARN] WebView2Loader.dll 未找到，跳过复制。")
        print(f"       预期路径: {WEBVIEW2_LOADER_SOURCE}")
        print(f"       请确认 CMake 已成功完成配置（FetchContent 自动下载 WebView2 SDK）。")
        return
    dest = DOTNET_PUBLISH_DIR / "WebView2Loader.dll"
    shutil.copy2(WEBVIEW2_LOADER_SOURCE, dest)
    log(f"复制: {WEBVIEW2_LOADER_SOURCE.name} → {dest}")


# ---------------------------------------------------------------------------
# 验证
# ---------------------------------------------------------------------------

def verify():
    print("\n=== [4/4] 验证最终输出 ===")
    required = [
        ("ImmersiveDisplay.exe", DOTNET_PUBLISH_DIR / "ImmersiveDisplay.exe"),
        ("host.dll",             DOTNET_PUBLISH_DIR / "host.dll"),
        ("WebView2Loader.dll",   DOTNET_PUBLISH_DIR / "WebView2Loader.dll"),
        ("WebUI/index.html",     DOTNET_PUBLISH_DIR / "WebUI" / "index.html"),
    ]
    all_ok = True
    for name, path in required:
        ok = path.is_file()
        size = path.stat().st_size if ok else 0
        status = "OK" if ok else "MISSING"
        if ok:
            print(f"  [{status}] {name} ({size // 1024} KB)")
        else:
            print(f"  [{status}] {name}")
            all_ok = False

    if all_ok:
        print(f"\n 构建成功！输出目录: {DOTNET_PUBLISH_DIR}")
    else:
        print("\n[WARNING] 部分产物缺失，请检查错误信息。")
        sys.exit(1)


# ---------------------------------------------------------------------------
# 清理
# ---------------------------------------------------------------------------

def clean():
    print("\n=== 清理构建产物 ===")
    targets = [
        ("前端 dist",        FRONTEND_DIR / "dist"),
        ("前端 node_modules", FRONTEND_DIR / "node_modules"),
        ("C++ build",        CPP_BUILD_DIR),
        ("C# bin",           CSHARP_DIR / "bin"),
        ("C# obj",           CSHARP_DIR / "obj"),
    ]
    for label, path in targets:
        if path.exists():
            shutil.rmtree(path)
            log(f"已删除: {label} ({path})")
        else:
            log(f"跳过: {label} (不存在)")
    print("  清理完成。")


# ---------------------------------------------------------------------------
# 打包
# ---------------------------------------------------------------------------

def package():
    """将构建产物打包为 zip（外层带 DisplayTrackTool/ 文件夹），排除 .pdb"""
    if not DOTNET_PUBLISH_DIR.exists():
        print(f"[ERROR] 输出目录不存在: {DOTNET_PUBLISH_DIR}")
        print("  请先执行完整构建。")
        sys.exit(1)

    zip_path = ROOT / "DisplayTrackTool.zip"
    staging = ROOT / "DisplayTrackTool"

    # PowerShell: 复制到 staging 目录（排除 .pdb），然后打包
    ps = f"""
$src = '{DOTNET_PUBLISH_DIR}'
$dst = '{staging}'
if (Test-Path $dst) {{ Remove-Item -Recurse -Force $dst }}
New-Item -ItemType Directory -Path $dst -Force > $null
Get-ChildItem $src -Exclude '*.pdb' |
  Where-Object {{ $_ -notmatch '\\.WebView2$' }} |
  Copy-Item -Destination $dst -Recurse
Compress-Archive -Path $dst -DestinationPath '{zip_path}' -Force
Remove-Item -Recurse -Force $dst
"""
    run(
        ["powershell", "-NoProfile", "-Command", ps],
        description="打包为 zip（外层文件夹 + 排除 .pdb）",
    )

    size = zip_path.stat().st_size
    log(f"打包完成: {zip_path.name} ({size // 1024} KB)")


# ---------------------------------------------------------------------------
# 主入口
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="DisplayTrackTool 构建脚本 —— 编排 Vue + C++ + C# 三层构建",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  python build.py                  # 完整构建（三层 + 部署 + 验证）
  python build.py --frontend       # 只构建前端
  python build.py --cpp            # 只构建 C++
  python build.py --csharp         # 只构建 C#
  python build.py --skip-frontend  # 完整构建但跳过前端
  python build.py --package        # 从已有产物打包 zip（排除 .pdb）
  python build.py --clean          # 清理所有产物
  python build.py --skip-frontend --package  # 跳过前端 + 打包
        """,
    )
    parser.add_argument("--frontend",      action="store_true", help="仅构建前端 (Vue)")
    parser.add_argument("--cpp",           action="store_true", help="仅构建 C++ host.dll")
    parser.add_argument("--csharp",        action="store_true", help="仅构建 C#")
    parser.add_argument("--skip-frontend", action="store_true", help="跳过前端构建（WebUI 已最新时使用）")
    parser.add_argument("--package",       action="store_true", help="构建完成后打包为 zip（排除 .pdb）")
    parser.add_argument("--clean",         action="store_true", help="清理所有构建产物")
    args = parser.parse_args()

    if args.clean:
        clean()
        return

    if args.frontend:
        build_frontend()
        if DOTNET_PUBLISH_DIR.exists():
            deploy_frontend()
        else:
            log("前端构建完成。执行完整构建 (python build.py) 或 --csharp 生成输出目录后自动部署。")
        return

    if args.cpp:
        build_cpp()
        if DOTNET_PUBLISH_DIR.exists():
            deploy_cpp_to_csharp()
        else:
            log("C++ 构建完成。执行完整构建 (python build.py) 或 --csharp 生成输出目录后自动部署。")
        return

    if args.csharp:
        build_csharp()
        if FRONTEND_OUTPUT.exists():
            deploy_frontend()
        if HOST_DLL_SOURCE.exists():
            deploy_cpp_to_csharp()
        return

    if args.package:
        package()
        return

    # ---- 完整构建 ----
    if args.skip_frontend:
        print("=== 跳过前端构建 (--skip-frontend) ===")
    else:
        build_frontend()

    build_cpp()
    build_csharp()

    # 部署前端到输出目录（在 publish 之后）
    if FRONTEND_OUTPUT.exists():
        deploy_frontend()
    elif args.skip_frontend:
        print("[WARN] --skip-frontend 但未找到前端产物，跳过 WebUI 部署。")

    deploy_cpp_to_csharp()
    verify()

    if args.package:
        package()

    # 下次构建可跳过前端
    print("\n提示: 前端未修改时，可加 --skip-frontend 跳过前端构建。")


if __name__ == "__main__":
    main()
