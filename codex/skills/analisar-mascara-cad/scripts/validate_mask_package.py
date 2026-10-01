#!/usr/bin/env python3
"""Validate an auditable mask-recognition package without modifying it."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


APPROVED_DECISIONS = {
    "APPROVED",
    "APPROVED_BY_USER",
    "APPROVED_SEMANTIC",
    "CORRECTED",
}
VALID_DECISIONS = APPROVED_DECISIONS | {"PENDING", "REJECTED"}
CARDINAL_DEGREES = {"RIGHT": 0.0, "UP": 90.0, "LEFT": 180.0, "DOWN": 270.0}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate coordinates, decisions, profiles and source integrity."
    )
    parser.add_argument("package_dir", type=Path)
    parser.add_argument("--source-dwg", type=Path)
    parser.add_argument("--require-approved", action="store_true")
    parser.add_argument("--report", type=Path)
    parser.add_argument("--tolerance", type=float, default=1e-3)
    return parser.parse_args()


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_jsonl(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []
    return [
        json.loads(line)
        for line in path.read_text(encoding="utf-8-sig").splitlines()
        if line.strip()
    ]


def as_list(value: Any, label: str) -> list[dict[str, Any]]:
    if isinstance(value, list):
        return value
    if isinstance(value, dict):
        for key in ("items", "profiles", "records", "value"):
            candidate = value.get(key)
            if isinstance(candidate, list):
                return candidate
    raise ValueError(f"{label} must contain a JSON array")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def angular_error(actual: float, expected: float) -> float:
    return abs((actual - expected + 180.0) % 360.0 - 180.0)


def validate_profiles(
    profiles: list[dict[str, Any]], tolerance: float, errors: list[str]
) -> None:
    ids: set[str] = set()
    groups: dict[str, list[int]] = defaultdict(list)
    expected_counts: dict[str, int] = {}

    for index, profile in enumerate(profiles, start=1):
        segment_id = str(profile.get("segment_id") or profile.get("profile_id") or "")
        if not segment_id:
            errors.append(f"profile {index}: missing segment_id")
        elif segment_id in ids:
            errors.append(f"profile {index}: duplicate segment_id {segment_id}")
        ids.add(segment_id)

        parent = str(profile.get("parent_path_id") or segment_id)
        segment_index = int(profile.get("segment_index", 1))
        segment_count = int(profile.get("segment_count", 1))
        if not 1 <= segment_index <= segment_count:
            errors.append(f"{segment_id}: invalid segment_index/segment_count")
        groups[parent].append(segment_index)
        if parent in expected_counts and expected_counts[parent] != segment_count:
            errors.append(f"{parent}: inconsistent segment_count")
        expected_counts[parent] = segment_count

        endpoints = profile.get("segment_wcs")
        anchor = profile.get("placement_anchor_wcs")
        if not isinstance(endpoints, list) or len(endpoints) != 2 or not isinstance(anchor, dict):
            errors.append(f"{segment_id}: missing two endpoints or placement anchor")
            continue

        try:
            x1, y1 = float(endpoints[0]["x"]), float(endpoints[0]["y"])
            x2, y2 = float(endpoints[1]["x"]), float(endpoints[1]["y"])
            ax, ay = float(anchor["x"]), float(anchor["y"])
        except (KeyError, TypeError, ValueError):
            errors.append(f"{segment_id}: invalid WCS coordinates")
            continue

        if math.hypot(ax - (x1 + x2) / 2.0, ay - (y1 + y2) / 2.0) > tolerance:
            errors.append(f"{segment_id}: anchor is not the segment midpoint")

        tangent = profile.get("path_tangent_deg")
        if tangent is not None:
            calculated = math.degrees(math.atan2(y2 - y1, x2 - x1)) % 360.0
            if angular_error(float(tangent) % 360.0, calculated) > tolerance:
                errors.append(f"{segment_id}: path tangent disagrees with endpoints")

    for parent, indexes in groups.items():
        expected = list(range(1, expected_counts[parent] + 1))
        if sorted(indexes) != expected:
            errors.append(f"{parent}: segment indexes {sorted(indexes)} != {expected}")


def main() -> int:
    args = parse_args()
    root = args.package_dir.resolve()
    errors: list[str] = []
    warnings: list[str] = []

    manifest_path = root / "mask-recognition-manifest.json"
    csv_path = root / "environment-coordinate-register.csv"
    if not manifest_path.exists():
        errors.append("missing mask-recognition-manifest.json")
    if not csv_path.exists():
        errors.append("missing environment-coordinate-register.csv")
    if errors:
        result = {"status": "FAIL", "package": str(root), "errors": errors}
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1

    manifest = read_json(manifest_path)
    with csv_path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))

    if not rows:
        errors.append("coordinate register is empty")
        fieldnames: list[str] = []
    else:
        fieldnames = list(rows[0])

    id_column = next(
        (name for name in ("id", "candidate_id", "point_id", "segment_id") if name in fieldnames),
        None,
    )
    kind_column = next(
        (name for name in ("record_kind", "category") if name in fieldnames), None
    )
    required = {"environment", "semantic_code", "decision", "x_wcs", "y_wcs"}
    missing_columns = sorted(required - set(fieldnames))
    if missing_columns:
        errors.append(f"coordinate register missing columns: {missing_columns}")
    if id_column is None:
        errors.append("coordinate register has no supported id column")
    if kind_column is None:
        errors.append("coordinate register has no record_kind/category column")

    seen_ids: set[str] = set()
    decision_counts: Counter[str] = Counter()
    kind_counts: Counter[str] = Counter()
    for line_number, row in enumerate(rows, start=2):
        record_id = (row.get(id_column or "") or "").strip()
        if not record_id:
            errors.append(f"CSV line {line_number}: empty id")
        elif record_id in seen_ids:
            errors.append(f"CSV line {line_number}: duplicate id {record_id}")
        seen_ids.add(record_id)

        for coordinate in ("x_wcs", "y_wcs"):
            try:
                float(row.get(coordinate, ""))
            except ValueError:
                errors.append(f"CSV line {line_number}: invalid {coordinate}")

        decision = (row.get("decision") or "").strip()
        decision_counts[decision] += 1
        if decision not in VALID_DECISIONS:
            errors.append(f"CSV line {line_number}: unsupported decision {decision!r}")
        if args.require_approved and decision not in APPROVED_DECISIONS:
            errors.append(f"CSV line {line_number}: non-approved decision {decision}")

        kind_counts[(row.get(kind_column or "") or "").strip()] += 1

    totals = manifest.get("totals", {})
    expected_fixed = totals.get("fixed_lighting_occurrences")
    if expected_fixed is not None and kind_counts["FIXED_LIGHTING"] != int(expected_fixed):
        errors.append(
            f"fixed count {kind_counts['FIXED_LIGHTING']} != manifest {expected_fixed}"
        )

    expected_special = totals.get(
        "special_led_no_box_points", totals.get("special_led_no_box_entities")
    )
    if expected_special is not None and kind_counts["SPECIAL_LED_NO_BOX"] != int(expected_special):
        errors.append(
            f"special LED count {kind_counts['SPECIAL_LED_NO_BOX']} != manifest {expected_special}"
        )

    expected_total = totals.get("placement_units_total")
    if expected_total is not None and len(rows) != int(expected_total):
        errors.append(f"CSV rows {len(rows)} != manifest placement total {expected_total}")

    profiles_path = root / "profile-anchor-register.json"
    profiles: list[dict[str, Any]] = []
    if profiles_path.exists():
        profiles = as_list(read_json(profiles_path), profiles_path.name)
        validate_profiles(profiles, args.tolerance, errors)
        expected_profiles = totals.get(
            "led_profile_straight_segments", totals.get("led_profile_paths")
        )
        if expected_profiles is not None and len(profiles) != int(expected_profiles):
            errors.append(
                f"profile count {len(profiles)} != manifest {expected_profiles}"
            )
        if args.require_approved:
            for profile in profiles:
                if profile.get("decision") not in APPROVED_DECISIONS:
                    errors.append(
                        f"profile {profile.get('segment_id')}: non-approved decision"
                    )
    else:
        warnings.append("profile-anchor-register.json not present")

    orientation_decisions = read_jsonl(root / "orientation-review-decisions.jsonl")
    for index, decision in enumerate(orientation_decisions, start=1):
        direction = decision.get("effective_direction")
        degrees = decision.get("effective_orientation_deg")
        if direction in CARDINAL_DEGREES and degrees is not None:
            if angular_error(float(degrees), CARDINAL_DEGREES[direction]) > args.tolerance:
                errors.append(
                    f"orientation decision {index}: {direction} disagrees with {degrees}°"
                )

    source_integrity = manifest.get("source_integrity", {})
    if int(source_integrity.get("original_write_commands", 0)) != 0:
        errors.append("manifest reports write commands directed to the original DWG")
    expected_hash = (
        source_integrity.get("sha256_expected")
        or source_integrity.get("inventory_sha256_before")
    )
    current_hash = None
    if args.source_dwg:
        if not args.source_dwg.exists():
            errors.append(f"source DWG not found: {args.source_dwg}")
        else:
            current_hash = sha256(args.source_dwg)
            if expected_hash and current_hash != str(expected_hash).upper():
                errors.append("source DWG SHA-256 differs from the manifest")

    for relative in manifest.get("visual_evidence", []):
        if not (root / relative).exists():
            errors.append(f"missing visual evidence: {relative}")

    final_state = manifest.get("final_state")
    if args.require_approved and final_state != "APPROVED_BY_USER":
        errors.append(f"final_state is {final_state!r}, expected APPROVED_BY_USER")

    result = {
        "status": "PASS" if not errors else "FAIL",
        "package": str(root),
        "final_state": final_state,
        "coordinate_rows": len(rows),
        "record_kinds": dict(sorted(kind_counts.items())),
        "decisions": dict(sorted(decision_counts.items())),
        "profile_segments": len(profiles),
        "orientation_decisions": len(orientation_decisions),
        "source_sha256": current_hash,
        "errors": errors,
        "warnings": warnings,
    }
    output = json.dumps(result, ensure_ascii=False, indent=2)
    print(output)
    if args.report:
        args.report.write_text(output + "\n", encoding="utf-8")
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
