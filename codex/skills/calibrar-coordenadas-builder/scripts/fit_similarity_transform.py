#!/usr/bin/env python3
"""Fit a 2D similarity transform and report residuals without auto-approval."""

from __future__ import annotations

import argparse
import csv
import json
import math
import sys
from pathlib import Path
from typing import Any


REQUIRED_COLUMNS = {
    "point_id",
    "source_x",
    "source_y",
    "target_x",
    "target_y",
    "role",
}


def read_points(path: Path) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        missing = REQUIRED_COLUMNS.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(
                "Colunas ausentes: " + ", ".join(sorted(missing))
            )
        points = []
        for row_number, row in enumerate(reader, start=2):
            try:
                role = row["role"].strip().upper()
                if role not in {"FIT", "VALIDATION"}:
                    raise ValueError("role deve ser FIT ou VALIDATION")
                points.append(
                    {
                        "point_id": row["point_id"].strip(),
                        "source_x": float(row["source_x"]),
                        "source_y": float(row["source_y"]),
                        "target_x": float(row["target_x"]),
                        "target_y": float(row["target_y"]),
                        "role": role,
                    }
                )
            except ValueError as exc:
                raise ValueError(f"Linha {row_number}: {exc}") from exc
    if len({point["point_id"] for point in points}) != len(points):
        raise ValueError("point_id duplicado.")
    return points


def fit(points: list[dict[str, Any]]) -> tuple[float, float, float, float]:
    fit_points = [point for point in points if point["role"] == "FIT"]
    if len(fit_points) < 2:
        raise ValueError("São necessários pelo menos dois pontos FIT.")

    sx = sum(point["source_x"] for point in fit_points) / len(fit_points)
    sy = sum(point["source_y"] for point in fit_points) / len(fit_points)
    tx_mean = sum(point["target_x"] for point in fit_points) / len(fit_points)
    ty_mean = sum(point["target_y"] for point in fit_points) / len(fit_points)

    dot = 0.0
    cross = 0.0
    source_energy = 0.0
    for point in fit_points:
        x = point["source_x"] - sx
        y = point["source_y"] - sy
        u = point["target_x"] - tx_mean
        v = point["target_y"] - ty_mean
        dot += x * u + y * v
        cross += x * v - y * u
        source_energy += x * x + y * y

    if source_energy <= 1e-18:
        raise ValueError("Pontos FIT coincidentes ou sem extensão espacial.")

    a = dot / source_energy
    b = cross / source_energy
    tx = tx_mean - a * sx + b * sy
    ty = ty_mean - b * sx - a * sy
    return a, b, tx, ty


def residuals(
    points: list[dict[str, Any]],
    a: float,
    b: float,
    tx: float,
    ty: float,
) -> list[dict[str, Any]]:
    rows = []
    for point in points:
        predicted_x = a * point["source_x"] - b * point["source_y"] + tx
        predicted_y = b * point["source_x"] + a * point["source_y"] + ty
        residual_x = predicted_x - point["target_x"]
        residual_y = predicted_y - point["target_y"]
        rows.append(
            {
                **point,
                "predicted_x": predicted_x,
                "predicted_y": predicted_y,
                "residual_x": residual_x,
                "residual_y": residual_y,
                "error": math.hypot(residual_x, residual_y),
            }
        )
    return rows


def metrics(rows: list[dict[str, Any]], role: str) -> dict[str, Any]:
    selected = [row for row in rows if row["role"] == role]
    if not selected:
        return {"count": 0, "rms_error": None, "max_error": None}
    return {
        "count": len(selected),
        "rms_error": math.sqrt(
            sum(row["error"] ** 2 for row in selected) / len(selected)
        ),
        "max_error": max(row["error"] for row in selected),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("control_points_csv", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-unit", required=True)
    parser.add_argument("--target-unit", required=True)
    args = parser.parse_args()

    try:
        points = read_points(args.control_points_csv)
        a, b, tx, ty = fit(points)
        rows = residuals(points, a, b, tx, ty)
        result = {
            "contract_version": "autoaibuilder-coordinate-calibration/1.0",
            "status": "CALCULATED_AWAITING_HUMAN_APPROVAL",
            "model": "SIMILARITY_2D",
            "source_unit": args.source_unit,
            "target_unit": args.target_unit,
            "coefficients": {"a": a, "b": b, "tx": tx, "ty": ty},
            "derived": {
                "scale": math.hypot(a, b),
                "rotation_degrees": math.degrees(math.atan2(b, a)),
            },
            "metrics": {
                "fit": metrics(rows, "FIT"),
                "validation": metrics(rows, "VALIDATION"),
            },
            "points": rows,
            "warnings": (
                []
                if any(row["role"] == "VALIDATION" for row in rows)
                else ["Nenhum ponto VALIDATION foi fornecido."]
            ),
        }
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(result, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, csv.Error) as exc:
        print(f"ERRO: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

