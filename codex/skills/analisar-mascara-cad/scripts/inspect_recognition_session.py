#!/usr/bin/env python3
"""Inspect an AutoAIBuilder recognition session without mutating its inputs."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def get(data: dict[str, Any], *names: str, default: Any = None) -> Any:
    for name in names:
        if name in data:
            return data[name]
    return default


def status_name(value: Any) -> str:
    numeric = {
        0: "PENDING",
        1: "APPROVED",
        2: "REJECTED",
        3: "CORRECTED",
    }
    if isinstance(value, int):
        return numeric.get(value, f"UNKNOWN_{value}")
    text = str(value or "PENDING").strip().upper()
    return {
        "PENDING": "PENDING",
        "APPROVED": "APPROVED",
        "REJECTED": "REJECTED",
        "CORRECTED": "CORRECTED",
    }.get(text, text)


def summarize(session: dict[str, Any], source_dwg: Path | None) -> dict[str, Any]:
    candidates = list(get(session, "Candidates", "candidates", default=[]) or [])
    legend = get(session, "LegendAnalysis", "legendAnalysis", default={}) or {}
    expected_hash = str(
        get(session, "SourceSha256", "sourceSha256", default="")
    ).upper()
    actual_hash = None
    integrity = None
    if source_dwg is not None:
        if not source_dwg.is_file():
            raise FileNotFoundError(f"DWG de origem não encontrado: {source_dwg}")
        actual_hash = sha256(source_dwg)
        integrity = actual_hash == expected_hash

    status_counts = Counter(
        status_name(get(item, "Status", "status", default=0))
        for item in candidates
    )
    anchor_counts = Counter(
        str(get(item, "PositionSource", "positionSource", default="UNKNOWN"))
        for item in candidates
    )
    confidence_counts = Counter(
        int(get(item, "ConfidenceScore", "confidenceScore", default=0) or 0)
        for item in candidates
    )
    legend_matched = sum(
        bool(get(item, "LegendMatched", "legendMatched", default=False))
        for item in candidates
    )

    summary = {
        "contract_version": "autoaibuilder-recognition-summary/1.0",
        "session_id": str(get(session, "Id", "id", default="")),
        "source_file": str(
            get(session, "SourceFileName", "sourceFileName", default="")
        ),
        "source_path": str(
            get(session, "SourceDwgPath", "sourceDwgPath", default="")
        ),
        "source_sha256_expected": expected_hash,
        "source_sha256_actual": actual_hash,
        "source_integrity_confirmed_now": integrity,
        "session_integrity_flag": bool(
            get(
                session,
                "OriginalIntegrityConfirmed",
                "originalIntegrityConfirmed",
                default=False,
            )
        ),
        "inventory": {
            "entities": int(
                get(
                    session,
                    "InventoryEntityCount",
                    "inventoryEntityCount",
                    default=0,
                )
                or 0
            ),
            "inserts": int(
                get(
                    session,
                    "InventoryInsertCount",
                    "inventoryInsertCount",
                    default=0,
                )
                or 0
            ),
            "expanded_inserts": int(
                get(
                    session,
                    "InventoryExpandedInsertCount",
                    "inventoryExpandedInsertCount",
                    default=0,
                )
                or 0
            ),
        },
        "candidates": {
            "total": len(candidates),
            "status": dict(sorted(status_counts.items())),
            "anchor_source": dict(sorted(anchor_counts.items())),
            "confidence_score": {
                str(key): value
                for key, value in sorted(
                    confidence_counts.items(), reverse=True
                )
            },
            "legend_matched": legend_matched,
        },
        "legend": {
            "detected": bool(
                get(legend, "IsDetected", "isDetected", default=False)
            ),
            "anchors": int(
                get(legend, "AnchorCount", "anchorCount", default=0) or 0
            ),
            "pairs": len(
                list(get(legend, "Entries", "entries", default=[]) or [])
            ),
            "unpaired_descriptions": int(
                get(
                    legend,
                    "UnpairedDescriptionCount",
                    "unpairedDescriptionCount",
                    default=0,
                )
                or 0
            ),
            "status": str(get(legend, "Status", "status", default="")),
        },
    }
    if source_dwg is not None and integrity is False:
        raise ValueError(
            "SHA-256 do DWG diverge da sessão; revisão bloqueada."
        )
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("session_json", type=Path)
    parser.add_argument("--source-dwg", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    try:
        session = json.loads(args.session_json.read_text(encoding="utf-8-sig"))
        result = summarize(session, args.source_dwg)
        encoded = json.dumps(result, ensure_ascii=False, indent=2)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(encoded + "\n", encoding="utf-8")
        print(encoded)
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"ERRO: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

