#!/usr/bin/env python3
"""Export AAR v3 from a verified technical DWG copy using AutoCAD Core Console."""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import shutil
import subprocess
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path


CORE_CONSOLE_CANDIDATES = (
    Path(r"D:\Autodesk\AutoCAD 2025\accoreconsole.exe"),
    Path(r"C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe"),
    Path(r"C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe"),
    Path(r"C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe"),
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def lisp_path(path: Path) -> str:
    return str(path.resolve()).replace("\\", "/").replace('"', '\\"')


def locate_project_root(explicit: Path | None) -> Path:
    candidates = []
    if explicit is not None:
        candidates.append(explicit.resolve())
    candidates.extend([Path.cwd(), *Path.cwd().parents])
    script = Path(__file__).resolve()
    candidates.extend(script.parents)
    for candidate in candidates:
        marker = (
            candidate
            / "src"
            / "AutoAIBuilder.Infrastructure"
            / "Recognition"
            / "AutoCadRecognitionInventory.lsp"
        )
        if marker.is_file():
            return candidate
    raise FileNotFoundError(
        "Raiz do AutoAIBuilder não localizada; informe --project-root."
    )


def locate_core_console(explicit: Path | None) -> Path:
    if explicit is not None:
        resolved = explicit.resolve()
        if resolved.is_file():
            return resolved
        raise FileNotFoundError(f"AutoCAD Core Console não encontrado: {resolved}")
    for candidate in CORE_CONSOLE_CANDIDATES:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("AutoCAD Core Console oficial não localizado.")


def assert_core_configuration_available(core_console: Path) -> None:
    config_files = sorted(core_console.parent.glob("acad*.cfg"))
    if not config_files:
        return
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    create_file = kernel32.CreateFileW
    create_file.argtypes = [
        ctypes.c_wchar_p,
        ctypes.c_uint32,
        ctypes.c_uint32,
        ctypes.c_void_p,
        ctypes.c_uint32,
        ctypes.c_uint32,
        ctypes.c_void_p,
    ]
    create_file.restype = ctypes.c_void_p
    close_handle = kernel32.CloseHandle
    close_handle.argtypes = [ctypes.c_void_p]
    close_handle.restype = ctypes.c_int
    invalid_handle = ctypes.c_void_p(-1).value
    for config in config_files:
        handle = create_file(
            str(config),
            0x80000000,
            0,
            None,
            3,
            0x80,
            None,
        )
        if handle == invalid_handle:
            error = ctypes.get_last_error()
            raise RuntimeError(
                f"A configuração {config.name} está bloqueada ou indisponível "
                f"(Win32 {error}). Feche o AutoCAD manualmente e repita."
            )
        close_handle(handle)


def decode_process_output(value: bytes) -> str:
    if not value:
        return ""
    if value.count(b"\x00") > len(value) // 8:
        return value.decode("utf-16-le", errors="replace")
    for encoding in ("utf-8", "cp1252"):
        try:
            return value.decode(encoding)
        except UnicodeDecodeError:
            continue
    return value.decode(errors="replace")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source_dwg", type=Path)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--project-root", type=Path)
    parser.add_argument("--core-console", type=Path)
    parser.add_argument("--timeout-seconds", type=int, default=180)
    args = parser.parse_args()

    source = args.source_dwg.resolve()
    if not source.is_file() or source.suffix.lower() != ".dwg":
        print("ERRO: informe um arquivo DWG existente.", file=sys.stderr)
        return 2

    try:
        project_root = locate_project_root(args.project_root)
        lisp = (
            project_root
            / "src"
            / "AutoAIBuilder.Infrastructure"
            / "Recognition"
            / "AutoCadRecognitionInventory.lsp"
        )
        core_console = locate_core_console(args.core_console)
        assert_core_configuration_available(core_console)
        output_root = args.output_root.resolve()
        output_root.mkdir(parents=True, exist_ok=True)
        timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
        run_dir = output_root / f"{timestamp}-{uuid.uuid4().hex}"
        run_dir.mkdir(parents=False, exist_ok=False)

        source_hash_before = sha256(source)
        technical_copy = run_dir / "entrada-verificada.dwg"
        inventory = run_dir / "inventario.aar"
        script = run_dir / "autoaibuilder-recognition-inventory.scr"
        log = run_dir / "core-console.log"
        manifest_path = run_dir / "inventory-manifest.json"

        shutil.copy2(source, technical_copy)
        copy_hash_before = sha256(technical_copy)
        if copy_hash_before != source_hash_before:
            raise RuntimeError("A cópia técnica não reproduziu o SHA-256 da origem.")

        script.write_text(
            '(setvar "FILEDIA" 0)\n'
            '(setvar "CMDDIA" 0)\n'
            '(setvar "SECURELOAD" 0)\n'
            f'(load "{lisp_path(lisp)}")\n'
            f'(aar:export "{lisp_path(inventory)}")\n'
            "(princ)\n",
            encoding="utf-8",
        )

        process = subprocess.run(
            [
                str(core_console),
                "/i",
                str(technical_copy),
                "/s",
                str(script),
                "/l",
                "en-US",
            ],
            cwd=run_dir,
            capture_output=True,
            timeout=args.timeout_seconds,
            check=False,
        )
        log.write_text(
            decode_process_output(process.stdout)
            + "\n"
            + decode_process_output(process.stderr),
            encoding="utf-8",
            errors="replace",
        )
        if process.returncode != 0 or not inventory.is_file():
            raise RuntimeError(
                f"Inventário não concluído; código {process.returncode}. "
                f"Consulte {log}."
            )

        with inventory.open("r", encoding="utf-8-sig") as stream:
            first_line = stream.readline().strip()
        if first_line != "AAR|3":
            raise RuntimeError(f"Contrato AAR inesperado: {first_line!r}.")

        source_hash_after = sha256(source)
        if source_hash_after != source_hash_before:
            raise RuntimeError("O SHA-256 do DWG original mudou durante a leitura.")

        manifest = {
            "contract_version": "autoaibuilder-cad-inventory/1.0",
            "created_at_utc": datetime.now(timezone.utc).isoformat(),
            "source_path": str(source),
            "source_sha256_before": source_hash_before,
            "source_sha256_after": source_hash_after,
            "source_integrity_confirmed": True,
            "technical_copy": str(technical_copy),
            "inventory_path": str(inventory),
            "inventory_contract": first_line,
            "inventory_lisp": str(lisp),
            "inventory_lisp_sha256": sha256(lisp),
            "core_console": str(core_console),
            "core_console_exit_code": process.returncode,
            "mutation_target": "TECHNICAL_COPY_ONLY",
            "original_write_commands": 0,
        }
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(json.dumps(manifest, ensure_ascii=False, indent=2))
        return 0
    except subprocess.TimeoutExpired:
        print("ERRO: o AutoCAD excedeu o tempo limite.", file=sys.stderr)
        return 3
    except (OSError, RuntimeError) as exc:
        print(f"ERRO: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
