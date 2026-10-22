"""Measures the outline of a flat piece (a chop, a round, a wedge) from the game's mesh.

The outline is how far the piece reaches from the middle of its bounds, in each
direction, looking down its thin side. A net, a collider or a hull is checked against it.

    py -3 scripts/measure_outline.py porkchop
    py -3 scripts/measure_outline.py porkchop --table 0.2034,0.1991,...
    py -3 scripts/measure_outline.py lamb_chop --csharp

Needs UnityPy and the sailwind-modding skill's sw_assets.py. Set SW_ASSETS to its path
if it is not in the default place. Mesh names come from `sw_assets.py find <text>`.
"""
import argparse
import importlib.util
import math
import os
import sys

import numpy as np

SW_ASSETS = os.environ.get(
    "SW_ASSETS", "C:/Users/dutya/.cursor/skills/sailwind-modding/scripts/assets/sw_assets.py")
DIRECTIONS = 720
CELL = 0.0005
REACH = 0.3
FAR = 0.015


def load_tool():
    spec = importlib.util.spec_from_file_location("sw_assets", SW_ASSETS)
    tool = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(tool)
    return tool


def find_mesh(tool, args):
    for _, env in tool.environments(args):
        for obj in env.objects:
            if obj.type.name == "Mesh" and tool.name_of(obj) == args.mesh:
                return obj.read()
    sys.exit("No mesh named %r. Try: sw_assets.py find %s" % (args.mesh, args.mesh))


def read_triangles(mesh):
    """Triangles as indices into the points sw_assets.mesh_vertices returns."""
    triangles = []
    for line in mesh.export().splitlines():
        if line.startswith("f "):
            corners = [int(part.split("/")[0]) - 1 for part in line.split()[1:]]
            for k in range(1, len(corners) - 1):
                triangles.append((corners[0], corners[k], corners[k + 1]))
    return triangles


def silhouette_edges(points, triangles):
    """Every edge of the flat triangles, once. The piece's far edge, in any direction, is
    on one of them, so the furthest an edge is crossed is the reach. Keeping only the
    edges with a triangle on one side left out a part of the real edge, 1.6 mm short."""
    seen = set()
    for tri in triangles:
        q = [points[i] for i in tri]
        if abs((q[1][0] - q[0][0]) * (q[2][1] - q[0][1]) - (q[2][0] - q[0][0]) * (q[1][1] - q[0][1])) < 1e-9:
            continue
        for i in range(3):
            a, b = q[i], q[(i + 1) % 3]
            seen.add(tuple(sorted(((round(float(a[0]), 5), round(float(a[1]), 5)), (round(float(b[0]), 5), round(float(b[1]), 5))))))
    return sorted(seen)


def cover(points, triangles):
    """A grid with every cell the piece covers, looking down the thin side."""
    size = int(2 * REACH / CELL)
    grid = np.zeros((size, size), bool)
    for tri in triangles:
        a, b, c = points[list(tri)]
        low = np.floor((points[list(tri)].min(0) + REACH) / CELL).astype(int)
        high = np.ceil((points[list(tri)].max(0) + REACH) / CELL).astype(int)
        xs, ys = np.meshgrid(np.arange(low[0], high[0] + 1), np.arange(low[1], high[1] + 1))
        q = np.stack([xs.ravel(), ys.ravel()], 1) * CELL - REACH + CELL / 2
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-12:
            continue
        l1 = ((b[1] - c[1]) * (q[:, 0] - c[0]) + (c[0] - b[0]) * (q[:, 1] - c[1])) / d
        l2 = ((c[1] - a[1]) * (q[:, 0] - c[0]) + (a[0] - c[0]) * (q[:, 1] - c[1])) / d
        inside = q[(l1 >= -1e-6) & (l2 >= -1e-6) & (1 - l1 - l2 >= -1e-6)]
        cells = np.floor((inside + REACH) / CELL).astype(int)
        grid[cells[:, 1], cells[:, 0]] = True
    return grid


def reach_by_direction(grid):
    """How far the piece reaches in each direction: along each ray from the centre, the
    covered cell furthest out. A notch cut into the edge is followed down to its floor.
    Only a place where the edge doubles back over itself is bridged."""
    angles = np.arange(DIRECTIONS) * 2 * math.pi / DIRECTIONS
    steps = np.arange(0, REACH - 0.02, CELL)
    far = np.zeros(DIRECTIONS)
    for i, angle in enumerate(angles):
        gx = ((steps * math.cos(angle) + REACH) / CELL).astype(int)
        gy = ((steps * math.sin(angle) + REACH) / CELL).astype(int)
        covered = grid[gy, gx]
        far[i] = steps[covered].max() if covered.any() else 0.0
    return angles, far


def table_at(table, angle):
    """The code's MeatBagLayout.OutlineAt: slot i is the value at angle i/n of a turn."""
    n = len(table)
    pos = (angle / (2 * math.pi) % 1.0) * n
    i = int(math.floor(pos)) % n
    t = pos - math.floor(pos)
    return table[i] * (1 - t) + table[(i + 1) % n] * t


def ideal_table(far, bins):
    """The best table of this size: each slot is the furthest reach within half a slot
    either side of its angle, so interpolating between slots never cuts into the piece."""
    per = DIRECTIONS / bins
    table = []
    for i in range(bins):
        centre = i * per
        picks = [int(round(centre + k)) % DIRECTIONS for k in range(-int(per / 2) - 1, int(per / 2) + 2)]
        table.append(float(max(far[p] for p in picks)))
    return table


def describe(label, table, angles, far, cord, gap):
    net = np.array([table_at(table, a) for a in angles]) + cord + gap
    clear = net - far
    runs, start = [], None
    for i in range(DIRECTIONS):
        if clear[i] > FAR:
            start = i if start is None else start
        elif start is not None:
            runs.append((start, i - 1))
            start = None
    if start is not None:
        runs.append((start, DIRECTIONS - 1))
    degrees = lambda i: round(i * 360 / DIRECTIONS)
    print("  %s" % label)
    print("    net minus meat: least %.1f cm, average %.1f cm, most %.1f cm" % (
        clear.min() * 100, clear.mean() * 100, clear.max() * 100))
    print("    inside the meat (closer than one cord): %d of %d directions" % ((clear < cord).sum(), DIRECTIONS))
    print("    more than %.1f cm off: %d directions" % (FAR * 100, (clear > FAR).sum()))
    print("    runs more than %.1f cm off (from deg, to deg, worst cm): %s" % (
        FAR * 100, [(degrees(a), degrees(b), round(float(clear[a:b + 1].max()) * 100, 1)) for a, b in runs]))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("mesh", help="mesh name, such as porkchop or lamb_chop")
    parser.add_argument("--flat", default="y", choices="xyz", help="the thin axis; the outline is the other two (default y)")
    parser.add_argument("--bins", type=int, default=48, help="slots in a table (default 48)")
    parser.add_argument("--table", help="a table from the code, as comma-separated numbers, to check")
    parser.add_argument("--cord", type=float, default=0.00375, help="cord radius in metres (default 0.0025 x 1.5)")
    parser.add_argument("--gap", type=float, default=0.004, help="gap between net and meat in metres (default 0.004)")
    parser.add_argument("--csharp", action="store_true", help="print the table as a C# array")
    parser.add_argument("--edges", metavar="NAME", help="print the silhouette edges as C# data, in a field called NAME")
    parser.add_argument("--fixture", metavar="NAME", help="print the flat triangles as C# test data, in a field called NAME")
    parser.add_argument("--data", default=None, help="Sailwind_Data folder")
    args = parser.parse_args()

    tool = load_tool()
    args.data = args.data or tool.DEFAULT_DATA
    args.bundle = None
    args.levels = False

    mesh = find_mesh(tool, args)
    points = np.array(tool.mesh_vertices(mesh), dtype=float)
    triangles = read_triangles(mesh)
    box = mesh.m_LocalAABB
    middle = np.array([box.m_Center.x, box.m_Center.y, box.m_Center.z])
    extent = np.array([box.m_Extent.x, box.m_Extent.y, box.m_Extent.z])
    flat = "xyz".index(args.flat)
    axes = [a for a in range(3) if a != flat]
    flat_points = points[:, axes] - middle[axes]

    print("%s: %d points, %d triangles, thickness %.1f cm along %s" % (
        args.mesh, len(points), len(triangles), extent[flat] * 200, args.flat))
    print("  bounds centre %s (the outline is measured from here)" % np.round(middle, 4))

    grid = cover(flat_points, triangles)
    angles, far = reach_by_direction(grid)
    print("  furthest reach: least %.1f cm, most %.1f cm, over %d directions" % (
        far.min() * 100, far.max() * 100, DIRECTIONS))

    ideal = ideal_table(far, args.bins)
    if args.table:
        table = [float(v) for v in args.table.split(",")]
        describe("the table from the code (%d slots)" % len(table), table, angles, far, args.cord, args.gap)
    describe("the best table of %d slots" % args.bins, ideal, angles, far, args.cord, args.gap)

    if args.edges:
        edges = silhouette_edges(flat_points, triangles)
        print()
        print("// The silhouette of %s as x0, z0, x1, z1 from the bounds centre. Made by scripts/measure_outline.py." % args.mesh)
        print("public static readonly float[] %s =" % args.edges)
        print("{")
        for i, ((x0, z0), (x1, z1)) in enumerate(edges):
            print("    %.5ff, %.5ff, %.5ff, %.5ff%s" % (x0, z0, x1, z1, "," if i + 1 < len(edges) else ""))
        print("};")

    if args.fixture:
        print()
        print("// Flat triangles of %s as x, z pairs from the bounds centre. Made by scripts/measure_outline.py." % args.mesh)
        print("public static readonly float[] %s =" % args.fixture)
        print("{")
        rows = []
        for tri in triangles:
            a, b, c = flat_points[list(tri)]
            if abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])) < 1e-9:
                continue
            rows.append(", ".join("%.5ff" % v for p in (a, b, c) for v in p))
        for i, row in enumerate(rows):
            print("    " + row + ("," if i + 1 < len(rows) else ""))
        print("};")

    if args.csharp:
        print()
        print("public static readonly float[] Outline =")
        print("{")
        for i in range(0, len(ideal), 8):
            row = ", ".join("%.4ff" % v for v in ideal[i:i + 8])
            print("    " + row + ("," if i + 8 < len(ideal) else ""))
        print("};")


if __name__ == "__main__":
    main()
