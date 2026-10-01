#!/usr/bin/env python3
"""Detect outlet-like triangles drawn as loose LINE entities in an AAR v3 inventory."""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Point:
    x: float
    y: float


@dataclass(frozen=True)
class Segment:
    handle: str
    start: Point
    end: Point
    length: float


@dataclass(frozen=True)
class Triangle:
    handles: tuple[str, ...]
    vertices: tuple[Point, ...]
    center: Point
    perimeter: float


def distance(left: Point, right: Point) -> float:
    return math.hypot(left.x - right.x, left.y - right.y)


def close(left: Point, right: Point, tolerance: float) -> bool:
    return distance(left, right) <= tolerance


def other_endpoint(fields: list[str]) -> Point:
    start = Point(float(fields[5]), float(fields[6]))
    corners = (
        Point(float(fields[19]), float(fields[20])),
        Point(float(fields[19]), float(fields[23])),
        Point(float(fields[22]), float(fields[20])),
        Point(float(fields[22]), float(fields[23])),
    )
    return max(corners, key=lambda point: distance(start, point))


def parse_segments(path: Path, layer: str, bounds: tuple[float, ...] | None) -> list[Segment]:
    version_found = False
    result: list[Segment] = []
    for raw_line in path.read_text(encoding="utf-8-sig", errors="strict").splitlines():
        if raw_line.startswith("AAR|"):
            version_found = raw_line.split("|", 2)[1] == "3"
            continue
        if not raw_line.startswith("ENTITY|"):
            continue
        fields = raw_line.split("|")
        if len(fields) < 32 or fields[2].upper() != "LINE":
            continue
        if fields[3].casefold() != layer.casefold() or fields[25] != "0" or fields[18] != "1":
            continue
        start = Point(float(fields[5]), float(fields[6]))
        if bounds and not (bounds[0] <= start.x <= bounds[2] and bounds[1] <= start.y <= bounds[3]):
            continue
        end = other_endpoint(fields)
        result.append(Segment(fields[1], start, end, distance(start, end)))
    if not version_found:
        raise ValueError("O arquivo não declara AAR|3.")
    return result


def merge_vertices(segments: tuple[Segment, ...], tolerance: float) -> list[Point]:
    vertices: list[Point] = []
    for endpoint in (point for segment in segments for point in (segment.start, segment.end)):
        for index, current in enumerate(vertices):
            if close(current, endpoint, tolerance):
                vertices[index] = Point((current.x + endpoint.x) / 2, (current.y + endpoint.y) / 2)
                break
        else:
            vertices.append(endpoint)
    return vertices


def build_triangle(
    first: Segment,
    second: Segment,
    third: Segment,
    endpoint_tolerance: float,
    side_min: float,
    side_max: float,
    side_ratio: float,
) -> Triangle | None:
    segments = (first, second, third)
    vertices = merge_vertices(segments, endpoint_tolerance)
    if len(vertices) != 3:
        return None
    degrees = [
        sum(close(segment.start, vertex, endpoint_tolerance) or close(segment.end, vertex, endpoint_tolerance) for segment in segments)
        for vertex in vertices
    ]
    lengths = [segment.length for segment in segments]
    if degrees != [2, 2, 2] or min(lengths) < side_min or max(lengths) > side_max:
        return None
    if max(lengths) / min(lengths) > side_ratio:
        return None
    center = Point(sum(vertex.x for vertex in vertices) / 3, sum(vertex.y for vertex in vertices) / 3)
    return Triangle(tuple(sorted(segment.handle for segment in segments)), tuple(vertices), center, sum(lengths))


def triangle_candidates(segments: list[Segment], args: argparse.Namespace) -> list[Triangle]:
    edges = [segment for segment in segments if args.side_min <= segment.length <= args.side_max]
    adjacency: list[set[int]] = [set() for _ in edges]
    for left in range(len(edges)):
        for right in range(left + 1, len(edges)):
            if any(close(a, b, args.endpoint_tolerance) for a in (edges[left].start, edges[left].end) for b in (edges[right].start, edges[right].end)):
                adjacency[left].add(right)
                adjacency[right].add(left)
    raw: list[Triangle] = []
    for first in range(len(edges)):
        for second in sorted(index for index in adjacency[first] if index > first):
            for third in sorted(index for index in adjacency[first] & adjacency[second] if index > second):
                triangle = build_triangle(
                    edges[first], edges[second], edges[third], args.endpoint_tolerance,
                    args.side_min, args.side_max, args.side_ratio,
                )
                if triangle:
                    raw.append(triangle)
    distinct: list[Triangle] = []
    for triangle in sorted(raw, key=lambda item: item.perimeter, reverse=True):
        if not any(close(triangle.center, existing.center, args.dedup_center_tolerance) for existing in distinct):
            distinct.append(triangle)
    return distinct


def side_midpoints(vertices: tuple[Point, ...]) -> tuple[Point, ...]:
    return tuple(
        Point((vertices[index].x + vertices[(index + 1) % 3].x) / 2,
              (vertices[index].y + vertices[(index + 1) % 3].y) / 2)
        for index in range(3)
    )


def axis_angle(triangle: Triangle, attached_midpoint: Point) -> float:
    opposite = max(triangle.vertices, key=lambda vertex: distance(vertex, attached_midpoint))
    return math.degrees(math.atan2(opposite.y - attached_midpoint.y, opposite.x - attached_midpoint.x)) % 180


def angle_error(left: float, right: float) -> float:
    return abs((left - right + 90) % 180 - 90)


def attach_stems(
    triangles: list[Triangle], segments: list[Segment], args: argparse.Namespace
) -> list[dict]:
    stems = [segment for segment in segments if args.stem_min <= segment.length <= args.stem_max]
    candidates: list[dict] = []
    for triangle in triangles:
        matches: list[tuple[float, Segment, Point]] = []
        for midpoint in side_midpoints(triangle.vertices):
            for stem in stems:
                gap = min(distance(stem.start, midpoint), distance(stem.end, midpoint))
                if gap <= args.stem_tolerance:
                    matches.append((gap, stem, midpoint))
        if not matches:
            continue
        _, stem, midpoint = min(matches, key=lambda item: item[0])
        min_x = min(vertex.x for vertex in triangle.vertices) - args.association_margin
        min_y = min(vertex.y for vertex in triangle.vertices) - args.association_margin
        max_x = max(vertex.x for vertex in triangle.vertices) + args.association_margin
        max_y = max(vertex.y for vertex in triangle.vertices) + args.association_margin
        associated = {
            segment.handle
            for segment in segments
            if min_x <= segment.start.x <= max_x and min_y <= segment.start.y <= max_y
            and min_x <= segment.end.x <= max_x and min_y <= segment.end.y <= max_y
        }
        associated.add(stem.handle)
        candidates.append({
            "triangle": triangle,
            "stem": stem,
            "handles": associated,
            "axis_deg": axis_angle(triangle, midpoint),
            "side_mean": triangle.perimeter / 3,
        })
    return candidates


def group_physical(candidates: list[dict], segments: list[Segment], args: argparse.Namespace) -> list[dict]:
    parents = list(range(len(candidates)))

    def root(index: int) -> int:
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    def union(left: int, right: int) -> None:
        left_root, right_root = root(left), root(right)
        if left_root != right_root:
            parents[right_root] = left_root

    for left in range(len(candidates)):
        for right in range(left + 1, len(candidates)):
            separation = distance(candidates[left]["triangle"].center, candidates[right]["triangle"].center)
            direction = math.degrees(math.atan2(
                candidates[right]["triangle"].center.y - candidates[left]["triangle"].center.y,
                candidates[right]["triangle"].center.x - candidates[left]["triangle"].center.x,
            )) % 180
            if not args.group_distance_min <= separation <= args.group_distance_max:
                continue
            if angle_error(candidates[left]["axis_deg"], candidates[right]["axis_deg"]) > args.group_axis_tolerance:
                continue
            if angle_error(direction, candidates[left]["axis_deg"]) <= args.group_axis_tolerance:
                union(left, right)

    groups: dict[int, list[dict]] = {}
    for index, candidate in enumerate(candidates):
        groups.setdefault(root(index), []).append(candidate)

    segment_by_handle = {segment.handle: segment for segment in segments}
    output: list[dict] = []
    for group in groups.values():
        handles = sorted({handle for candidate in group for handle in candidate["handles"]})
        points = [point for handle in handles for point in (segment_by_handle[handle].start, segment_by_handle[handle].end)]
        triangle_count = len(group)
        code = {1: "TOMADA_SIMPLES", 2: "TOMADA_DUPLA", 3: "TOMADA_TRIPLA"}.get(
            triangle_count, "TOMADA_MULTIPLA_PENDENTE"
        )
        output.append({
            "candidate_id": f"LOOSE-{handles[0]}-{handles[-1]}",
            "entity_role": "PLANT_OCCURRENCE_CANDIDATE",
            "semantic_code_suggestion": code,
            "triangle_count": triangle_count,
            "handles": handles,
            "anchor_source": "BOUNDS_CENTER_FALLBACK",
            "position_wcs": {
                "x": (min(point.x for point in points) + max(point.x for point in points)) / 2,
                "y": (min(point.y for point in points) + max(point.y for point in points)) / 2,
                "z": 0.0,
            },
            "axis_deg": round(sum(candidate["axis_deg"] for candidate in group) / triangle_count, 6),
            "color_status": "NOT_AVAILABLE_IN_AAR",
            "decision": "PENDING",
            "required_review": ["COLOR", "LEGEND_MATCH", "ENVIRONMENT", "ORIENTATION"],
        })
    return sorted(output, key=lambda item: (item["position_wcs"]["y"], item["position_wcs"]["x"]))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inventory", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--layer", default="pontos")
    parser.add_argument("--include-bounds", nargs=4, type=float, metavar=("MIN_X", "MIN_Y", "MAX_X", "MAX_Y"))
    parser.add_argument("--side-min", type=float, default=34.0)
    parser.add_argument("--side-max", type=float, default=50.0)
    parser.add_argument("--side-ratio", type=float, default=1.12)
    parser.add_argument("--stem-min", type=float, default=5.0)
    parser.add_argument("--stem-max", type=float, default=16.0)
    parser.add_argument("--endpoint-tolerance", type=float, default=0.75)
    parser.add_argument("--stem-tolerance", type=float, default=1.0)
    parser.add_argument("--dedup-center-tolerance", type=float, default=3.0)
    parser.add_argument("--association-margin", type=float, default=1.5)
    parser.add_argument("--group-distance-min", type=float, default=30.0)
    parser.add_argument("--group-distance-max", type=float, default=60.0)
    parser.add_argument("--group-axis-tolerance", type=float, default=15.0)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    segments = parse_segments(args.inventory, args.layer, tuple(args.include_bounds) if args.include_bounds else None)
    triangles = triangle_candidates(segments, args)
    with_stems = attach_stems(triangles, segments, args)
    physical = group_physical(with_stems, segments, args)
    result = {
        "contract_version": "autoaibuilder-loose-outlet-scan/1.0",
        "inventory": str(args.inventory.resolve()),
        "coordinate_system": "WCS",
        "filter": {"layer": args.layer, "include_bounds": args.include_bounds},
        "signature": {
            "side_min": args.side_min,
            "side_max": args.side_max,
            "side_ratio": args.side_ratio,
            "stem_min": args.stem_min,
            "stem_max": args.stem_max,
        },
        "triangle_candidates": len(with_stems),
        "physical_candidates": len(physical),
        "candidates": physical,
        "final_state": "PENDING_VISUAL_REVIEW",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"triangle_candidates": len(with_stems), "physical_candidates": len(physical)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
