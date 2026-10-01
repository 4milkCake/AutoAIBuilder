#!/usr/bin/env python3
"""Export stable CSV review queues from an AutoAIBuilder session."""

from __future__ import annotations

import argparse
import csv
import json
import sys
from collections import Counter
from pathlib import Path
from typing import Any


def get(data: dict[str, Any], *names: str, default: Any = "") -> Any:
    for name in names:
        if name in data:
            return data[name]
    return default


def write_csv(path: Path, fieldnames: list[str], rows: list[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("session_json", type=Path)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    try:
        session = json.loads(args.session_json.read_text(encoding="utf-8-sig"))
        candidates = list(get(session, "Candidates", "candidates", default=[]) or [])
        legend = get(session, "LegendAnalysis", "legendAnalysis", default={}) or {}
        entries = list(get(legend, "Entries", "entries", default=[]) or [])
        output = args.output_dir.resolve()
        output.mkdir(parents=True, exist_ok=True)

        legend_rows = []
        for index, entry in enumerate(entries, start=1):
            legend_rows.append(
                {
                    "review_order": index,
                    "symbol_handle": get(entry, "SymbolHandle", "symbolHandle"),
                    "description_handle": get(
                        entry, "DescriptionHandle", "descriptionHandle"
                    ),
                    "block_name": get(entry, "BlockName", "blockName"),
                    "description": get(entry, "Description", "description"),
                    "symbol_x": get(entry, "SymbolX", "symbolX"),
                    "symbol_y": get(entry, "SymbolY", "symbolY"),
                    "description_x": get(
                        entry, "DescriptionX", "descriptionX"
                    ),
                    "description_y": get(
                        entry, "DescriptionY", "descriptionY"
                    ),
                    "pair_distance": get(
                        entry, "PairDistance", "pairDistance"
                    ),
                    "expansion_depth": get(
                        entry, "ExpansionDepth", "expansionDepth", default=0
                    ),
                    "root_handle": get(entry, "RootHandle", "rootHandle"),
                    "stable_path": get(entry, "StablePath", "stablePath"),
                    "geometry_signature": get(
                        entry, "GeometrySignature", "geometrySignature"
                    ),
                    "evidence": get(entry, "Evidence", "evidence"),
                    "decision": "PENDING",
                    "corrected_description": "",
                    "review_note": "",
                }
            )

        candidate_rows = []
        for index, item in enumerate(candidates, start=1):
            candidate_rows.append(
                {
                    "review_order": index,
                    "candidate_id": get(item, "Id", "id"),
                    "handle": get(item, "Handle", "handle"),
                    "root_handle": get(item, "RootHandle", "rootHandle"),
                    "block_name": get(item, "BlockName", "blockName"),
                    "source_layer": get(item, "SourceLayer", "sourceLayer"),
                    "proposed_code": get(
                        item, "ProposedCode", "proposedCode"
                    ),
                    "proposed_description": get(
                        item, "ProposedDescription", "proposedDescription"
                    ),
                    "proposed_height": get(
                        item, "ProposedHeight", "proposedHeight"
                    ),
                    "confidence_score": get(
                        item, "ConfidenceScore", "confidenceScore", default=0
                    ),
                    "legend_matched": get(
                        item, "LegendMatched", "legendMatched", default=False
                    ),
                    "legend_description": get(
                        item, "LegendDescription", "legendDescription"
                    ),
                    "anchor_source": get(
                        item, "PositionSource", "positionSource"
                    ),
                    "position_x_wcs": get(item, "PositionX", "positionX"),
                    "position_y_wcs": get(item, "PositionY", "positionY"),
                    "position_z_wcs": get(item, "PositionZ", "positionZ"),
                    "source_insertion_x": get(
                        item, "SourceInsertionX", "sourceInsertionX"
                    ),
                    "source_insertion_y": get(
                        item, "SourceInsertionY", "sourceInsertionY"
                    ),
                    "rotation_degrees": get(
                        item, "RotationDegrees", "rotationDegrees"
                    ),
                    "expansion_depth": get(
                        item, "ExpansionDepth", "expansionDepth", default=0
                    ),
                    "stable_path": get(item, "StablePath", "stablePath"),
                    "geometry_signature": get(
                        item, "GeometrySignature", "geometrySignature"
                    ),
                    "detection_reason": get(
                        item, "DetectionReason", "detectionReason"
                    ),
                    "decision": "PENDING",
                    "corrected_code": "",
                    "corrected_description": "",
                    "review_note": "",
                }
            )

        legend_path = output / "legend-review.csv"
        candidate_path = output / "candidate-review.csv"
        write_csv(
            legend_path,
            list(legend_rows[0].keys()) if legend_rows else [],
            legend_rows,
        )
        write_csv(
            candidate_path,
            list(candidate_rows[0].keys()) if candidate_rows else [],
            candidate_rows,
        )

        anchor_counts = Counter(row["anchor_source"] for row in candidate_rows)
        manifest = {
            "contract_version": "autoaibuilder-review-queues/1.0",
            "session_id": str(get(session, "Id", "id")),
            "source_file": str(
                get(session, "SourceFileName", "sourceFileName")
            ),
            "source_sha256": str(
                get(session, "SourceSha256", "sourceSha256")
            ),
            "legend_rows": len(legend_rows),
            "unpaired_legend_descriptions": int(
                get(
                    legend,
                    "UnpairedDescriptionCount",
                    "unpairedDescriptionCount",
                    default=0,
                )
                or 0
            ),
            "candidate_rows": len(candidate_rows),
            "anchor_sources": dict(sorted(anchor_counts.items())),
            "legend_review_path": str(legend_path),
            "candidate_review_path": str(candidate_path),
            "status": "PENDING_HUMAN_REVIEW",
        }
        manifest_path = output / "review-queue-manifest.json"
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(json.dumps(manifest, ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, json.JSONDecodeError, csv.Error) as exc:
        print(f"ERRO: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

