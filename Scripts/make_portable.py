#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把 ZenTimings 的构建产物打包成便携版 ZIP。

用法:
    python Scripts/make_portable.py              # 默认打包 Release，缺失则退回 Debug
    python Scripts/make_portable.py Release
    python Scripts/make_portable.py Debug

产物: 仓库根目录下 ZenTimings-zhCN-portable-YYYYMMDD.zip

便携版会排除调试符号(*.pdb)与本机的 settings.xml，
并写入一份只含界面语言设置的 settings.xml，使解压后开箱即为简体中文
（用户仍可在「工具 → 选项 → 常规 → 界面语言」里切换）。

只需要 Python 3 标准库，无需安装任何依赖。
"""
import datetime
import os
import sys
import zipfile

# 仓库根目录 = 本脚本所在目录的上级
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BIN = os.path.join(REPO_ROOT, "WPF", "bin")

# 便携版需要的文件（相对构建输出目录）
INCLUDE = [
    "ZenTimings.exe",
    "ZenTimings.exe.config",
    "ZenStates-Core.dll",
    "inpoutx64.dll",
    "WinIo32.dll",
    "WinIo32.sys",
    "InpOut.LICENSE.txt",
    "WinIo32.LICENSE.txt",
    "LICENSE",
    "README.md",
]

EXCLUDE_EXT = {".pdb", ".xml"}
EXCLUDE_NAME = {"settings.xml", "crash.log", "crash.old.log", "live_snapshot.json"}

# 预置设置：只指定界面语言，其余字段用 AppSettings 的默认值。
SETTINGS_TEMPLATE = (
    '<?xml version="1.0" encoding="utf-16"?>\r\n'
    '<AppSettings xmlns:xsd="http://www.w3.org/2001/XMLSchema" '
    'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">\r\n'
    '  <Version>1.17</Version>\r\n'
    '  <Language>zh-CN</Language>\r\n'
    '</AppSettings>'
)


def pick_config(preferred):
    """返回 (配置名, 目录)；preferred 不存在时退回另一个。"""
    candidates = [preferred] + [c for c in ("Release", "Debug") if c != preferred]
    for cfg in candidates:
        path = os.path.join(BIN, cfg)
        if os.path.isdir(path) and os.path.exists(os.path.join(path, "ZenTimings.exe")):
            return cfg, path
    return None, None


def human(n):
    return f"{n / 1024:.1f} KB" if n < 1024 * 1024 else f"{n / 1024 / 1024:.2f} MB"


def main():
    preferred = sys.argv[1] if len(sys.argv) > 1 else "Release"
    cfg, src = pick_config(preferred)
    if not cfg:
        print("未找到构建产物。请先编译项目，确保存在：")
        print("  " + os.path.join(BIN, "Release", "ZenTimings.exe"))
        print("  或 " + os.path.join(BIN, "Debug", "ZenTimings.exe"))
        return 1

    print(f"打包配置: {cfg}")
    print(f"源目录: {src}")

    stamp = datetime.datetime.now().strftime("%Y%m%d")
    out = os.path.join(REPO_ROOT, f"ZenTimings-zhCN-portable-{stamp}.zip")

    added, missing = [], []
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name in INCLUDE:
            path = os.path.join(src, name)
            if os.path.exists(path):
                z.write(path, name)
                added.append(name)
            else:
                missing.append(name)

        z.writestr("settings.xml", SETTINGS_TEMPLATE)
        added.append("settings.xml")

        # 兜底：带上输出目录里其余的 dll / sys（驱动库等），排除调试符号与本机配置
        for fn in sorted(os.listdir(src)):
            if fn in added or fn in EXCLUDE_NAME:
                continue
            if os.path.splitext(fn)[1].lower() in EXCLUDE_EXT:
                continue
            full = os.path.join(src, fn)
            if os.path.isfile(full) and fn.lower().endswith((".dll", ".sys", ".txt", ".md")):
                z.write(full, fn)
                added.append(fn)

    print(f"\n已打包 {len(added)} 个文件 -> {out}")
    print(f"文件大小: {human(os.path.getsize(out))}")
    if missing:
        print("\n[注意] 以下文件缺失（构建可能不完整）:")
        for m in missing:
            print("  - " + m)
    return 0


if __name__ == "__main__":
    sys.exit(main())
