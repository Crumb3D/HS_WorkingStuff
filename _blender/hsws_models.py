# HS Working Stuff model generator (combi boiler, panel radiator, chest freezer).
# Run: blender --background --factory-startup --python hsws_models.py -- <fbx_out_dir> [<blend_out>]
#
# Conventions (7DTD ModelEntity, ModelOffset 0,0,0):
#   - 1 Blender unit = 1 block = 1 m. Blender Z is up.
#   - Origin = bottom centre of the block cell.
#   - Front faces Blender -Y. With Place=TowardsPlacerInverted that side faces the player,
#     so the back (+Y, y = 0.5) sits against the wall the block was placed on.
#   - Objects named COL_* become the root BoxCollider in Unity.
#   - Material slot names are mapped to Unity materials by name.

import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.abspath(argv[0]) if len(argv) > 0 else os.path.join(HERE, "..", "_unity", "Assets", "HSWorkingStuff", "Models")
BLEND_OUT = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.join(HERE, "hsws_models.blend")

MAT_COLORS = {
    "HS_White": (0.93, 0.93, 0.91, 1.0),
    "HS_Grey": (0.36, 0.37, 0.39, 1.0),
    "HS_Dark": (0.05, 0.05, 0.06, 1.0),
    "HS_Chrome": (0.82, 0.83, 0.85, 1.0),
    "HS_Copper": (0.72, 0.42, 0.22, 1.0),
    "HS_Display": (0.25, 0.65, 1.0, 1.0),
    "HS_Led": (0.25, 1.0, 0.35, 1.0),
    "HS_Screen": (0.02, 0.02, 0.025, 1.0),
    "HS_Red": (0.78, 0.08, 0.06, 1.0),
    "HS_Neon": (1.0, 0.25, 0.1, 1.0),
    "HS_Green": (0.30, 0.52, 0.16, 1.0),
    "HS_Straw": (0.78, 0.60, 0.28, 1.0),
    "HS_Wheat": (0.86, 0.64, 0.24, 1.0),
    "HS_Wood": (0.55, 0.38, 0.20, 1.0),
    "HS_Cord": (0.85, 0.84, 0.78, 1.0),
    "HS_ClothBlue": (0.16, 0.32, 0.70, 1.0),
    "HS_ClothYellow": (0.95, 0.78, 0.20, 1.0),
    "HS_ClothPink": (0.92, 0.50, 0.62, 1.0),
    "HS_Denim": (0.20, 0.27, 0.42, 1.0),
    "HS_Water": (0.25, 0.55, 0.82, 1.0),
    "HS_Soil": (0.32, 0.22, 0.10, 1.0),
    # icon-only colours (never exported to Unity)
    "HS_Bread": (1.0, 0.70, 0.30, 1.0),
    "HS_Crust": (0.62, 0.37, 0.15, 1.0),
    "HS_Toast": (0.55, 0.27, 0.07, 1.0),
    "HS_ToastCrust": (0.25, 0.10, 0.02, 1.0),
    "HS_Dough": (1.0, 0.85, 0.50, 1.0),
    "HS_Tea": (0.50, 0.29, 0.13, 1.0),
    "HS_Coffee": (0.16, 0.09, 0.05, 1.0),
    "HS_Paper": (0.86, 0.78, 0.60, 1.0),
    "HS_TeaGreen": (0.08, 0.40, 0.20, 1.0),
    "HS_Gold": (0.92, 0.70, 0.18, 1.0),
}

WALL = 0.5


def material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = MAT_COLORS.get(name, (0.5, 0.5, 0.5, 1.0))
    return m


class Piece:
    """One exported model: a single render mesh with several material slots, plus collider boxes."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.cols = []
        self.subs = []
        self.keep_normals = False
        # Each collider box stays its own BoxCollider (COLS_*) instead of one box round them all (COL_*).
        self.split_cols = False

    # A separate child object the game can show or hide by name, e.g. a plug in a socket.
    def sub(self, suffix):
        s = Piece(self.name + "_" + suffix)
        self.subs.append(s)
        return s

    def mat_index(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _finish(self, faces, mat, smooth=False):
        mi = self.mat_index(mat)
        for f in faces:
            f.material_index = mi
            f.smooth = smooth

    def box(self, mat, center, size):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        self._finish(list({f for v in verts for f in v.link_faces}), mat)

    # Round bar centred on `center`, lying along X, Y or standing on Z.
    def rod(self, mat, center, radius, length, axis, segs=16):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=radius, radius2=radius, depth=length)
        verts = res["verts"]
        if axis == "X":
            rot = Matrix.Rotation(math.radians(90.0), 4, "Y")
        elif axis == "Y":
            rot = Matrix.Rotation(math.radians(-90.0), 4, "X")
        else:
            rot = Matrix.Identity(4)
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(center)) @ rot, verts=verts)
        self._finish(list({f for v in verts for f in v.link_faces}), mat, smooth=True)

    # Round bar from p0 to p1, any direction; r1 tapers it towards p1.
    def seg(self, mat, p0, p1, r0, r1=None, segs=8, smooth=True):
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        if d.length < 1e-6:
            return
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=r0, radius2=r0 if r1 is None else r1, depth=d.length)
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((p0 + p1) / 2) @ rot, verts=res["verts"])
        self._finish(list({f for v in res["verts"] for f in v.link_faces}), mat, smooth=smooth)

    # Squashed sphere: scale (sx, sy, sz) times radius.
    def ball(self, mat, center, radius, scale=(1, 1, 1), segs=20):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=segs // 2, radius=radius)
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=res["verts"])
        self._finish(list({f for v in res["verts"] for f in v.link_faces}), mat, smooth=True)

    # Box turned by `rot` (a 3x3 or 4x4 rotation) about its centre.
    def obox(self, mat, center, size, rot):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        m = Matrix.Translation(Vector(center)) @ rot.to_4x4() @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=res["verts"])
        self._finish(list({f for v in res["verts"] for f in v.link_faces}), mat)

    # Flat quad facing -Y (the front) with UVs 0..1 left to right, bottom to top: a TV picture fills it.
    def screen(self, mat, x0, x1, z0, z1, y):
        uv = self.bm.loops.layers.uv.verify()
        vs = [self.bm.verts.new(v) for v in ((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1))]
        f = self.bm.faces.new(vs)
        for loop, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            loop[uv].uv = t
        self._finish([f], mat)
        self.keep_normals = True

    def collider(self, center, size):
        self.cols.append((tuple(center), tuple(size)))

    def build(self, collection):
        if not self.keep_normals:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(material(m))
        root = bpy.data.objects.new(self.name, me)
        collection.objects.link(root)
        for i, (center, size) in enumerate(self.cols):
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.0)
            cm = bpy.data.meshes.new("COL_%s_%d" % (self.name, i))
            bm.to_mesh(cm)
            bm.free()
            ob = bpy.data.objects.new(("COLS_%d" if self.split_cols else "COL_%d") % i, cm)
            ob.parent = root
            ob.location = Vector(center)
            ob.scale = Vector(size)
            collection.objects.link(ob)
        for s in self.subs:
            child = s.build(collection)
            child.parent = root
        return root


# ---------------------------------------------------------------- pieces

def combi_boiler():
    """Wall-hung UK combi boiler: white case, control strip along the bottom, pipes down, flue into the wall."""
    p = Piece("HSCombiBoiler")
    w, d = 0.44, 0.28
    z0, z1 = 0.18, 0.88
    yb = WALL - d / 2           # case centre depth
    yf = WALL - d               # front face
    p.box("HS_White", (0, yb, (z0 + z1) / 2), (w, d, z1 - z0))
    # front cover, a touch proud with a shadow line round it
    p.box("HS_White", (0, yf - 0.006, 0.595), (w - 0.02, 0.012, 0.53))
    p.box("HS_Grey", (0, yf - 0.002, 0.322), (w - 0.01, 0.004, 0.006))
    # control strip
    p.box("HS_Grey", (0, yf - 0.008, 0.25), (w, 0.016, 0.13))
    p.box("HS_Dark", (-0.07, yf - 0.017, 0.26), (0.13, 0.004, 0.06))
    p.box("HS_Display", (-0.07, yf - 0.0195, 0.26), (0.11, 0.002, 0.045))
    p.rod("HS_Dark", (0.08, yf - 0.024, 0.25), 0.022, 0.024, "Y", 20)
    p.rod("HS_Dark", (0.16, yf - 0.024, 0.25), 0.022, 0.024, "Y", 20)
    p.box("HS_Led", (0.005, yf - 0.018, 0.29), (0.012, 0.004, 0.008))
    # pressure gauge
    p.rod("HS_Chrome", (-0.175, yf - 0.02, 0.25), 0.027, 0.016, "Y", 24)
    p.rod("HS_White", (-0.175, yf - 0.029, 0.25), 0.022, 0.003, "Y", 24)
    p.box("HS_Dark", (-0.175, yf - 0.031, 0.258), (0.003, 0.002, 0.016))
    # badge
    p.box("HS_Chrome", (0, yf - 0.013, 0.81), (0.11, 0.003, 0.022))
    # pipes from the underside to the floor of the cell, with isolation valves
    py = WALL - 0.08
    for x in (-0.15, -0.075, 0.0, 0.075, 0.15):
        p.rod("HS_Copper", (x, py, z0 / 2), 0.011, z0, "Z", 12)
        p.rod("HS_Chrome", (x, py, 0.12), 0.017, 0.035, "Z", 12)
        p.box("HS_Dark", (x, py - 0.022, 0.12), (0.008, 0.02, 0.008))
    # flue: up out of the top, elbow, back into the wall
    fx, fy = 0.0, WALL - 0.13
    p.rod("HS_White", (fx, fy, z1 + 0.04), 0.045, 0.08, "Z", 24)
    p.rod("HS_White", (fx, (fy + WALL) / 2, z1 + 0.075), 0.045, WALL - fy + 0.045, "Y", 24)
    p.rod("HS_Grey", (fx, WALL - 0.006, z1 + 0.075), 0.06, 0.012, "Y", 24)
    p.collider((0, yb, 0.5), (w + 0.04, d + 0.04, 1.0))
    return p


def panel_radiator():
    """UK double panel convector radiator on short brackets, TRV left, lockshield right."""
    p = Piece("HSPanelRadiator")
    w = 0.90
    z0, z1 = 0.15, 0.70
    zc, h = (z0 + z1) / 2, z1 - z0
    yr = WALL - 0.05            # rear panel
    yfr = WALL - 0.13           # front panel
    p.box("HS_White", (0, yfr, zc), (w, 0.02, h))
    p.box("HS_White", (0, yr, zc), (w, 0.02, h))
    p.box("HS_Grey", (0, (yfr + yr) / 2, zc - 0.01), (w - 0.02, 0.06, h - 0.04))
    # pressed channels on the front panel
    n = 17
    for i in range(n):
        x = -w / 2 + 0.035 + i * (w - 0.07) / (n - 1)
        p.box("HS_White", (x, yfr - 0.012, zc), (0.016, 0.006, h - 0.03))
    # end caps and top grille
    for s in (-1, 1):
        p.box("HS_White", (s * (w / 2 + 0.004), (yfr + yr) / 2, zc), (0.008, yr - yfr + 0.02, h))
    p.box("HS_White", (0, (yfr + yr) / 2, z1 + 0.004), (w + 0.016, yr - yfr + 0.02, 0.008))
    for i in range(28):
        x = -w / 2 + 0.03 + i * (w - 0.06) / 27
        p.box("HS_Dark", (x, (yfr + yr) / 2, z1 + 0.0085), (0.012, yr - yfr - 0.02, 0.002))
    # wall brackets
    for x in (-0.30, 0.30):
        p.box("HS_Grey", (x, (yr + WALL) / 2, z1 - 0.08), (0.04, WALL - yr, 0.06))
    # valves and tails to the floor
    vy = (yfr + yr) / 2
    for s in (-1, 1):
        x = s * (w / 2 - 0.05)
        p.rod("HS_Copper", (x, vy, 0.04), 0.011, 0.08, "Z", 12)
        p.rod("HS_Chrome", (x, vy, 0.105), 0.016, 0.07, "Z", 16)
    # thermostatic head on the left, lockshield cap on the right
    p.rod("HS_White", (-(w / 2 - 0.05), vy, 0.175), 0.026, 0.07, "Z", 24)
    p.rod("HS_Grey", (-(w / 2 - 0.05), vy, 0.212), 0.02, 0.006, "Z", 24)
    p.rod("HS_White", ((w / 2 - 0.05), vy, 0.15), 0.019, 0.02, "Z", 16)
    p.collider((0, (yfr + WALL) / 2, 0.36), (w + 0.04, WALL - yfr + 0.04, 0.72))
    return p


def chest_freezer():
    """Domestic chest freezer two blocks wide (MultiBlockDim 2,1,1: the game centres the model on the pair)."""
    p = Piece("HSChestFreezer")
    w, d = 1.90, 0.70
    yb = WALL - 0.02 - d / 2    # cabinet centre depth, back nearly against the wall
    yf = yb - d / 2             # front face
    h0, h1 = 0.07, 0.80         # cabinet bottom and top
    lid = 0.09
    p.box("HS_Grey", (0, yb + 0.02, h0 / 2), (w - 0.08, d - 0.06, h0))
    p.box("HS_White", (0, yb, (h0 + h1) / 2), (w, d, h1 - h0))
    # rounded-off front corners
    for s in (-1, 1):
        p.rod("HS_White", (s * (w / 2 - 0.015), yf + 0.015, (h0 + h1) / 2), 0.015, h1 - h0, "Z", 12)
    # seal line, then the lid a touch proud of the cabinet
    p.box("HS_Grey", (0, yb, h1 + 0.004), (w - 0.01, d - 0.01, 0.008))
    p.box("HS_White", (0, yb, h1 + 0.008 + lid / 2), (w + 0.014, d + 0.014, lid))
    # recessed handle along the lid front
    p.box("HS_Grey", (0, yf - 0.013, h1 + 0.035), (0.60, 0.018, 0.032))
    p.box("HS_Dark", (0, yf - 0.023, h1 + 0.028), (0.54, 0.004, 0.012))
    # badge
    p.box("HS_Chrome", (-0.72, yf - 0.002, h1 - 0.07), (0.16, 0.004, 0.026))
    # controls: thermostat knob, power light, fast-freeze light
    p.box("HS_Grey", (0.72, yf - 0.004, 0.17), (0.26, 0.008, 0.09))
    p.rod("HS_White", (0.66, yf - 0.017, 0.17), 0.024, 0.018, "Y", 20)
    p.box("HS_Led", (0.75, yf - 0.0095, 0.18), (0.016, 0.004, 0.016))
    p.box("HS_Display", (0.80, yf - 0.0095, 0.18), (0.016, 0.004, 0.016))
    # condenser vents on the right side
    for i in range(7):
        p.box("HS_Dark", (w / 2 + 0.002, yb + 0.14, 0.15 + i * 0.03), (0.004, 0.26, 0.012))
    # lid hinges at the back
    for x in (-0.6, 0.0, 0.6):
        p.box("HS_Grey", (x, yb + d / 2 + 0.01, h1 + 0.035), (0.09, 0.02, 0.045))
    top = h1 + 0.008 + lid
    p.collider((0, yb, top / 2), (w + 0.02, d + 0.02, top))
    return p


def split_air_con():
    """Wall-mounted split air conditioner (indoor unit): place it on the wall at head height or above."""
    p = Piece("HSSplitAirCon")
    w, d, h = 0.88, 0.22, 0.29
    z0 = 0.55
    zc = z0 + h / 2
    yb = WALL - d / 2
    yf = WALL - d
    # body, with a softly rounded front made of a bar along the top and bottom edges
    p.box("HS_White", (0, yb + 0.01, zc), (w, d - 0.02, h - 0.04))
    p.rod("HS_White", (0, yf + 0.04, z0 + h - 0.04), 0.04, w, "X", 20)
    p.rod("HS_White", (0, yf + 0.04, z0 + 0.06), 0.04, w, "X", 20)
    p.box("HS_White", (0, yb + 0.02, z0 + h - 0.02), (w, d - 0.04, 0.04))
    # front panel a touch proud, with a shadow line
    p.box("HS_White", (0, yf + 0.004, zc + 0.03), (w - 0.03, 0.01, h - 0.12))
    p.box("HS_Grey", (0, yf + 0.006, z0 + 0.105), (w - 0.02, 0.006, 0.004))
    # air outlet slot and louvre flap underneath
    p.box("HS_Dark", (0, yf + 0.06, z0 + 0.012), (w - 0.10, 0.08, 0.024))
    p.box("HS_White", (0, yf + 0.025, z0 + 0.004), (w - 0.12, 0.05, 0.008))
    for i in range(9):
        x = -(w - 0.16) / 2 + i * (w - 0.16) / 8
        p.box("HS_Grey", (x, yf + 0.06, z0 + 0.012), (0.006, 0.07, 0.022))
    # intake grille on top
    for i in range(14):
        y = yf + 0.04 + i * (d - 0.07) / 13
        p.box("HS_Grey", (0, y, z0 + h + 0.001), (w - 0.08, 0.006, 0.002))
    # display and lights on the front, right side
    p.box("HS_Dark", (0.30, yf - 0.002, zc + 0.01), (0.10, 0.004, 0.035))
    p.box("HS_Display", (0.29, yf - 0.0045, zc + 0.01), (0.05, 0.002, 0.022))
    p.box("HS_Led", (0.335, yf - 0.0045, zc + 0.01), (0.008, 0.002, 0.008))
    # badge
    p.box("HS_Grey", (-0.30, yf - 0.001, zc + 0.03), (0.10, 0.003, 0.014))
    # refrigerant pipes and drain out of the back corner into the wall
    p.rod("HS_Copper", (-w / 2 + 0.06, WALL - 0.02, z0 + 0.03), 0.009, 0.04, "Y", 10)
    p.rod("HS_White", (-w / 2 + 0.06, WALL - 0.02, z0 + 0.03), 0.02, 0.03, "Y", 14)
    p.collider((0, yb, zc), (w + 0.02, d + 0.02, h + 0.02))
    return p


def ceiling_air_con():
    """Commercial ceiling cassette air conditioner: square grille flush with the ceiling, four louvred outlets."""
    p = Piece("HSCeilingAirCon")
    s = 0.84
    zt = 1.0
    # body in the ceiling void, then the face panel
    p.box("HS_Grey", (0, 0, zt - 0.05), (0.60, 0.60, 0.10))
    p.box("HS_White", (0, 0, zt - 0.115), (s, s, 0.03))
    # centre intake grille
    p.box("HS_Grey", (0, 0, zt - 0.132), (0.44, 0.44, 0.004))
    for i in range(16):
        x = -0.21 + i * 0.42 / 15
        p.box("HS_White", (x, 0, zt - 0.135), (0.008, 0.44, 0.004))
    # four outlet slots with louvres
    for a in range(4):
        rot = Matrix.Rotation(math.radians(90 * a), 4, "Z")
        c = rot @ Vector((0, -0.335, zt - 0.132))
        sz = (0.56, 0.07, 0.006) if a % 2 == 0 else (0.07, 0.56, 0.006)
        p.box("HS_Dark", tuple(c), sz)
        lc = rot @ Vector((0, -0.335, zt - 0.14))
        lz = (0.54, 0.05, 0.006) if a % 2 == 0 else (0.05, 0.54, 0.006)
        p.box("HS_White", tuple(lc), lz)
    # status lights in one corner
    p.box("HS_Led", (0.36, -0.36, zt - 0.132), (0.012, 0.012, 0.004))
    p.box("HS_Display", (0.33, -0.36, zt - 0.132), (0.012, 0.012, 0.004))
    p.collider((0, 0, zt - 0.075), (s, s, 0.15))
    return p


def wall_thermometer():
    """Digital room thermometer on the wall at eye height: white case, LCD window, dial thermometer tube beside it."""
    p = Piece("HSWallThermometer")
    w, d, h = 0.16, 0.035, 0.22
    zc = 0.55
    yb = WALL - d / 2
    yf = WALL - d
    p.box("HS_White", (0, yb, zc), (w, d, h))
    p.box("HS_Grey", (0, yf - 0.001, zc), (w - 0.012, 0.002, h - 0.012))
    p.box("HS_White", (0, yf - 0.003, zc), (w - 0.02, 0.003, h - 0.02))
    # LCD with segment bars
    p.box("HS_Dark", (-0.015, yf - 0.006, zc + 0.04), (0.10, 0.004, 0.07))
    for i, x in enumerate((-0.05, -0.022, 0.006)):
        p.box("HS_Display", (x, yf - 0.0085, zc + 0.04), (0.018, 0.002, 0.045))
    p.box("HS_Display", (0.025, yf - 0.0085, zc + 0.065), (0.008, 0.002, 0.008))
    # glass tube with the copper column, on the right
    p.rod("HS_Chrome", (0.055, yf - 0.008, zc - 0.005), 0.007, 0.17, "Z", 10)
    p.rod("HS_Copper", (0.055, yf - 0.012, zc - 0.04), 0.004, 0.09, "Z", 8)
    p.rod("HS_Copper", (0.055, yf - 0.012, zc - 0.088), 0.01, 0.012, "Y", 12)
    # buttons along the bottom
    for x in (-0.05, -0.02):
        p.box("HS_Grey", (x, yf - 0.006, zc - 0.07), (0.022, 0.006, 0.012))
    p.box("HS_Led", (0.0, yf - 0.006, zc - 0.07), (0.006, 0.004, 0.006))
    p.collider((0, yb - 0.01, zc), (w + 0.04, d + 0.04, h + 0.04))
    return p


def _white_good_case(p, w, d, h):
    """Freestanding 600 mm white-goods box, back against the wall, grey kickplate and feet."""
    yb = WALL - 0.02 - d / 2
    yf = yb - d / 2
    p.box("HS_White", (0, yb, (0.04 + h) / 2), (w, d, h - 0.04))
    p.box("HS_Grey", (0, yb + 0.02, 0.02), (w - 0.06, d - 0.06, 0.04))
    p.box("HS_White", (0, yf + 0.008, 0.055), (w - 0.01, 0.016, 0.05))
    for x in (-w / 2 + 0.05, w / 2 - 0.05):
        p.rod("HS_Dark", (x, yf + 0.06, 0.015), 0.018, 0.03, "Z", 12)
    p.box("HS_White", (0, yb, h - 0.006), (w + 0.006, d + 0.006, 0.012))
    return yb, yf


def washing_machine():
    """UK front-loading washing machine: drawer top left, programme dial, display, chrome porthole door."""
    p = Piece("HSWashingMachine")
    w, d, h = 0.60, 0.58, 0.85
    yb, yf = _white_good_case(p, w, d, h)
    zp = h - 0.075
    # control panel
    p.box("HS_White", (0, yf - 0.006, zp), (w - 0.01, 0.012, 0.11))
    p.box("HS_Grey", (0, yf - 0.002, zp - 0.058), (w - 0.01, 0.004, 0.004))
    p.box("HS_White", (-0.19, yf - 0.016, zp), (0.18, 0.008, 0.085))
    p.box("HS_Grey", (-0.19, yf - 0.021, zp - 0.025), (0.08, 0.004, 0.018))
    p.rod("HS_Chrome", (0.02, yf - 0.02, zp), 0.035, 0.02, "Y", 24)
    p.rod("HS_White", (0.02, yf - 0.033, zp), 0.028, 0.008, "Y", 24)
    p.box("HS_Dark", (0.02, yf - 0.038, zp + 0.018), (0.005, 0.003, 0.016))
    p.box("HS_Dark", (0.18, yf - 0.014, zp + 0.012), (0.12, 0.004, 0.045))
    p.box("HS_Display", (0.18, yf - 0.0165, zp + 0.012), (0.09, 0.002, 0.03))
    for i in range(4):
        p.box("HS_Grey", (0.135 + i * 0.03, yf - 0.016, zp - 0.03), (0.018, 0.008, 0.01))
    p.box("HS_Led", (0.245, yf - 0.015, zp - 0.03), (0.008, 0.004, 0.008))
    # porthole door: chrome ring, dark glass, white rim, handle
    zd = 0.40
    p.rod("HS_Chrome", (0, yf - 0.02, zd), 0.215, 0.03, "Y", 40)
    p.rod("HS_White", (0, yf - 0.036, zd), 0.19, 0.006, "Y", 40)
    p.rod("HS_Dark", (0, yf - 0.04, zd), 0.15, 0.006, "Y", 40)
    p.rod("HS_Grey", (0, yf - 0.03, zd), 0.13, 0.01, "Y", 40)
    p.box("HS_Grey", (0.2, yf - 0.042, zd), (0.03, 0.012, 0.09))
    p.box("HS_Grey", (-0.205, yf - 0.03, zd), (0.02, 0.02, 0.06))
    # filter flap bottom right
    p.box("HS_Grey", (0.2, yf - 0.002, 0.12), (0.1, 0.004, 0.07))
    p.collider((0, yb, h / 2), (w + 0.02, d + 0.02, h))
    return p


def tumble_dryer():
    """UK condenser tumble dryer: water tank drawer top left, dial, large white-rimmed door, vent grille."""
    p = Piece("HSTumbleDryer")
    w, d, h = 0.60, 0.58, 0.85
    yb, yf = _white_good_case(p, w, d, h)
    zp = h - 0.075
    p.box("HS_White", (0, yf - 0.006, zp), (w - 0.01, 0.012, 0.11))
    p.box("HS_Grey", (0, yf - 0.002, zp - 0.058), (w - 0.01, 0.004, 0.004))
    # water tank drawer with a blue window
    p.box("HS_White", (-0.17, yf - 0.016, zp), (0.22, 0.008, 0.085))
    p.box("HS_Display", (-0.12, yf - 0.021, zp + 0.01), (0.06, 0.003, 0.03))
    p.box("HS_Grey", (-0.22, yf - 0.021, zp - 0.025), (0.08, 0.004, 0.018))
    p.rod("HS_Chrome", (0.08, yf - 0.02, zp), 0.035, 0.02, "Y", 24)
    p.rod("HS_White", (0.08, yf - 0.033, zp), 0.028, 0.008, "Y", 24)
    p.box("HS_Dark", (0.08, yf - 0.038, zp + 0.018), (0.005, 0.003, 0.016))
    for i in range(3):
        p.box("HS_Grey", (0.17 + i * 0.035, yf - 0.016, zp), (0.022, 0.008, 0.012))
    p.box("HS_Led", (0.25, yf - 0.015, zp + 0.025), (0.008, 0.004, 0.008))
    # big door: white rim, grey ring, smoked glass
    zd = 0.42
    p.rod("HS_White", (0, yf - 0.02, zd), 0.24, 0.03, "Y", 40)
    p.rod("HS_Grey", (0, yf - 0.036, zd), 0.205, 0.006, "Y", 40)
    p.rod("HS_Dark", (0, yf - 0.04, zd), 0.17, 0.006, "Y", 40)
    p.box("HS_Grey", (0, yf - 0.044, zd + 0.215), (0.1, 0.012, 0.022))
    # vent grille along the bottom
    for i in range(10):
        p.box("HS_Dark", (-0.18 + i * 0.04, yf - 0.002, 0.12), (0.02, 0.004, 0.05))
    p.collider((0, yb, h / 2), (w + 0.02, d + 0.02, h))
    return p


def fridge_freezer():
    """UK tall white fridge-freezer, 60/40: fridge door on top, freezer door below, handles on the left."""
    p = Piece("HSFridgeFreezer")
    w, d, h = 0.60, 0.64, 1.80
    yb = WALL - 0.02 - d / 2
    yf = yb - d / 2
    p.box("HS_White", (0, yb + 0.03, (0.06 + h) / 2), (w, d - 0.06, h - 0.06))
    p.box("HS_Grey", (0, yb + 0.03, 0.03), (w - 0.04, d - 0.1, 0.06))
    # kick grille
    p.box("HS_Dark", (0, yf + 0.06, 0.035), (w - 0.06, 0.004, 0.04))
    for i in range(12):
        p.box("HS_Grey", (-0.25 + i * 0.045, yf + 0.058, 0.035), (0.012, 0.004, 0.036))
    # doors with a shadow gap between them
    zs = 0.78
    p.box("HS_White", (0, yf + 0.03, (0.065 + zs) / 2), (w, 0.06, zs - 0.065 - 0.006))
    p.box("HS_White", (0, yf + 0.03, (zs + h) / 2), (w, 0.06, h - zs - 0.006))
    p.box("HS_Grey", (0, yf + 0.035, zs), (w - 0.01, 0.05, 0.006))
    # recessed handles on the left edge
    p.box("HS_Grey", (-w / 2 + 0.02, yf - 0.006, zs + 0.2), (0.025, 0.012, 0.3))
    p.box("HS_Grey", (-w / 2 + 0.02, yf - 0.006, zs - 0.2), (0.025, 0.012, 0.26))
    # temperature display on the fridge door
    p.box("HS_Dark", (0.17, yf - 0.002, 1.55), (0.1, 0.004, 0.05))
    p.box("HS_Display", (0.155, yf - 0.0045, 1.55), (0.05, 0.002, 0.03))
    p.box("HS_Led", (0.2, yf - 0.0045, 1.55), (0.008, 0.002, 0.008))
    # badge
    p.box("HS_Chrome", (0, yf - 0.002, h - 0.05), (0.1, 0.004, 0.018))
    # hinges on the right
    for z in (h - 0.004, zs, 0.065):
        p.box("HS_Grey", (w / 2 - 0.04, yf + 0.03, z), (0.04, 0.05, 0.008))
    p.collider((0, yb, h / 2), (w + 0.02, d + 0.02, h))
    return p


# ---------------------------------------------------------------- wall plates
# Sockets sit low in the cell (place them in the cell above a floor or a worktop); switches are meant for the
# cell one up from the floor, so they end up at about light-switch height.

def _plate(p, w, h, zc, d=0.009):
    yf = WALL - d
    p.box("HS_White", (0, WALL - d / 2, zc), (w, d, h))
    p.box("HS_White", (0, yf - 0.001, zc), (w - 0.006, 0.002, h - 0.006))
    return yf - 0.002


def _rocker(p, tag, x, zc, yf, w, h, mat, flash=False):
    """Two rocker children: SwOn has the top proud (bottom pressed, UK style on), SwOff the bottom proud.
    The game shows whichever matches the switch. flash adds the red 'on' strip to the proud half."""
    hh = h / 2
    for state, top_out in (("SwOn", True), ("SwOff", False)):
        s = p.sub(state + tag)
        dt, db = (0.009, 0.004) if top_out else (0.004, 0.009)
        s.box(mat, (x, yf - dt / 2, zc + hh / 2), (w, dt, hh))
        s.box(mat, (x, yf - db / 2, zc - hh / 2), (w, db, hh))
        if flash and top_out:
            s.box("HS_Red", (x, yf - dt - 0.0005, zc + hh / 2), (w * 0.7, 0.001, hh * 0.5))


def _uk_outlet(p, x, zc, yf, tag=""):
    """BS 1363 face: vertical earth slot on top, live and neutral below, rocker switch with a red flash to the right."""
    p.box("HS_Dark", (x - 0.012, yf, zc + 0.012), (0.005, 0.002, 0.009))
    p.box("HS_Dark", (x - 0.023, yf, zc - 0.01), (0.008, 0.002, 0.005))
    p.box("HS_Dark", (x - 0.001, yf, zc - 0.01), (0.008, 0.002, 0.005))
    _rocker(p, tag, x + 0.024, zc + 0.012, yf, 0.013, 0.02, "HS_White", flash=True)


def _uk_plug(p, i, x, zc, yf):
    """Black BS 1363 plug pushed into the outlet, cable out of the bottom. Hidden until a wire leaves the socket."""
    s = p.sub("Plug%d" % i)
    s.box("HS_Dark", (x - 0.012, yf - 0.016, zc), (0.048, 0.03, 0.056))
    s.box("HS_Grey", (x - 0.012, yf - 0.0315, zc + 0.006), (0.034, 0.001, 0.03))
    s.rod("HS_Dark", (x - 0.012, yf - 0.018, zc - 0.034), 0.006, 0.014, "Z", 10)
    s.rod("HS_Dark", (x - 0.012, yf - 0.018, zc - 0.05), 0.0035, 0.022, "Z", 8)


def socket_uk_single():
    p = Piece("HSSocketUKSingle")
    zc = 0.3
    yf = _plate(p, 0.086, 0.086, zc)
    _uk_outlet(p, -0.004, zc, yf)
    _uk_plug(p, 0, -0.004, zc, yf)
    p.collider((0, WALL - 0.03, zc), (0.16, 0.06, 0.16))
    return p


def socket_uk_double():
    p = Piece("HSSocketUKDouble")
    zc = 0.3
    yf = _plate(p, 0.146, 0.086, zc)
    _uk_outlet(p, -0.04, zc, yf, "0")
    _uk_outlet(p, 0.032, zc, yf, "1")
    _uk_plug(p, 0, -0.04, zc, yf)
    _uk_plug(p, 1, 0.032, zc, yf)
    p.collider((0, WALL - 0.03, zc), (0.2, 0.06, 0.16))
    return p


def _us_plug(p, i, zc, yf):
    """Black NEMA plug in the outlet with the cord dropping out of it."""
    s = p.sub("Plug%d" % i)
    s.box("HS_Dark", (0, yf - 0.016, zc), (0.03, 0.026, 0.028))
    s.rod("HS_Dark", (0, yf - 0.02, zc - 0.018), 0.004, 0.012, "Z", 8)
    s.rod("HS_Dark", (0, yf - 0.02, zc - 0.032), 0.003, 0.018, "Z", 8)


def _us_outlet(p, zc, yf):
    """NEMA 5-15 face: raised oval-ish face, two vertical blades and a round ground hole."""
    p.box("HS_White", (0, yf - 0.002, zc), (0.034, 0.004, 0.03))
    p.box("HS_Dark", (-0.0065, yf - 0.0045, zc + 0.004), (0.0025, 0.002, 0.008))
    p.box("HS_Dark", (0.0065, yf - 0.0045, zc + 0.004), (0.0025, 0.002, 0.009))
    p.rod("HS_Dark", (0, yf - 0.0045, zc - 0.009), 0.003, 0.002, "Y", 12)


def socket_us_single():
    p = Piece("HSSocketUSSingle")
    zc = 0.3
    yf = _plate(p, 0.07, 0.115, zc, 0.006)
    _us_outlet(p, zc, yf)
    _us_plug(p, 0, zc, yf)
    for z in (zc + 0.042, zc - 0.042):
        p.rod("HS_Chrome", (0, yf - 0.001, z), 0.0035, 0.003, "Y", 10)
    p.collider((0, WALL - 0.03, zc), (0.14, 0.06, 0.18))
    return p


def socket_us_double():
    p = Piece("HSSocketUSDouble")
    zc = 0.3
    yf = _plate(p, 0.07, 0.115, zc, 0.006)
    _us_outlet(p, zc + 0.022, yf)
    _us_outlet(p, zc - 0.022, yf)
    _us_plug(p, 0, zc + 0.022, yf)
    _us_plug(p, 1, zc - 0.022, yf)
    p.rod("HS_Chrome", (0, yf - 0.001, zc), 0.0035, 0.003, "Y", 10)
    p.collider((0, WALL - 0.03, zc), (0.14, 0.06, 0.18))
    return p


def range_outlet_us():
    """US 240V range outlet (NEMA 14-50): big round face, two hot slots, ground on top, flat neutral below.
    The US has no cooker switch: the range plugs straight into this, always live."""
    p = Piece("HSRangeOutletUS")
    zc = 0.3
    yf = _plate(p, 0.09, 0.12, zc, 0.006)
    p.rod("HS_White", (0, yf - 0.004, zc), 0.028, 0.008, "Y", 24)
    yr = yf - 0.0085
    p.box("HS_Dark", (-0.012, yr, zc + 0.002), (0.0035, 0.002, 0.013))
    p.box("HS_Dark", (0.012, yr, zc + 0.002), (0.0035, 0.002, 0.013))
    p.rod("HS_Dark", (0, yr, zc + 0.016), 0.0035, 0.002, "Y", 12)
    p.box("HS_Dark", (0, yr, zc - 0.015), (0.012, 0.002, 0.0035))
    for z in (zc + 0.048, zc - 0.048):
        p.rod("HS_Chrome", (0, yf - 0.001, z), 0.0035, 0.003, "Y", 10)
    s = p.sub("Plug0")
    s.rod("HS_Dark", (0, yr - 0.015, zc), 0.025, 0.028, "Y", 24)
    s.rod("HS_Dark", (0, yr - 0.018, zc - 0.034), 0.008, 0.022, "Z", 12)
    s.rod("HS_Dark", (0, yr - 0.018, zc - 0.056), 0.0055, 0.024, "Z", 10)
    p.collider((0, WALL - 0.03, zc), (0.16, 0.06, 0.18))
    return p


def light_switch_uk():
    """Single gang UK rocker light switch."""
    p = Piece("HSLightSwitchUK")
    zc = 0.25
    yf = _plate(p, 0.086, 0.086, zc)
    p.box("HS_Grey", (0, yf - 0.0005, zc), (0.028, 0.001, 0.04))
    _rocker(p, "", 0, zc, yf - 0.001, 0.024, 0.036, "HS_White")
    p.collider((0, WALL - 0.03, zc), (0.16, 0.06, 0.16))
    return p


def light_switch_us():
    """US single pole toggle switch: tall narrow plate, slot, lever up for on, down for off."""
    p = Piece("HSLightSwitchUS")
    zc = 0.25
    yf = _plate(p, 0.07, 0.115, zc, 0.006)
    p.box("HS_White", (0, yf - 0.002, zc), (0.016, 0.004, 0.03))
    p.box("HS_Dark", (0, yf - 0.0045, zc), (0.007, 0.001, 0.018))
    for state, up in (("SwOn", 1), ("SwOff", -1)):
        s = p.sub(state)
        s.box("HS_White", (0, yf - 0.009, zc + up * 0.003), (0.006, 0.01, 0.008))
        s.box("HS_White", (0, yf - 0.016, zc + up * 0.008), (0.0065, 0.006, 0.01))
    for z in (zc + 0.042, zc - 0.042):
        p.rod("HS_Chrome", (0, yf - 0.001, z), 0.0035, 0.003, "Y", 10)
    p.collider((0, WALL - 0.03, zc), (0.14, 0.06, 0.18))
    return p


def cooker_switch_uk():
    """UK 45A double pole cooker switch: tall plate, big red rocker, neon, label."""
    p = Piece("HSCookerSwitchUK")
    zc = 0.3
    yf = _plate(p, 0.086, 0.146, zc)
    p.box("HS_Grey", (0, yf - 0.0005, zc + 0.03), (0.046, 0.001, 0.066))
    _rocker(p, "", 0, zc + 0.03, yf - 0.001, 0.04, 0.06, "HS_Red")
    p.rod("HS_Grey", (0, yf - 0.0015, zc - 0.025), 0.0055, 0.003, "Y", 12)
    p.sub("Lamp").rod("HS_Neon", (0, yf - 0.0035, zc - 0.025), 0.005, 0.002, "Y", 12)
    p.box("HS_Dark", (0, yf - 0.0005, zc - 0.05), (0.04, 0.001, 0.01))
    p.collider((0, WALL - 0.03, zc), (0.16, 0.06, 0.22))
    return p


# ---------------------------------------------------------------- worktop appliances
# These stand on a worktop: place them in the cell above it. They sit a little towards the wall.

def kettle():
    """Cordless jug kettle on its power base: water window at the front, handle right, spout left."""
    p = Piece("HSKettle")
    yc = 0.08
    p.rod("HS_Dark", (0, yc, 0.01), 0.088, 0.02, "Z", 32)
    p.rod("HS_White", (0, yc, 0.024), 0.084, 0.008, "Z", 32)
    p.seg("HS_White", (0, yc, 0.028), (0, yc, 0.235), 0.082, 0.066, 32)
    p.rod("HS_White", (0, yc, 0.241), 0.062, 0.012, "Z", 32)
    p.rod("HS_Grey", (0, yc, 0.25), 0.04, 0.006, "Z", 24)
    p.box("HS_Grey", (0.012, yc, 0.256), (0.03, 0.014, 0.012))
    # water window with the level marks
    p.obox("HS_Display", (0.0, yc - 0.075, 0.13), (0.022, 0.006, 0.15), Matrix.Rotation(math.radians(-4.5), 3, "X"))
    for i in range(4):
        p.box("HS_White", (0.0, yc - 0.0795, 0.08 + i * 0.035), (0.012, 0.002, 0.003))
    # handle on the right with the switch and light under it
    p.box("HS_White", (0.112, yc, 0.15), (0.022, 0.032, 0.17))
    p.box("HS_White", (0.09, yc, 0.225), (0.05, 0.032, 0.024))
    p.box("HS_White", (0.092, yc, 0.07), (0.05, 0.032, 0.024))
    p.box("HS_Grey", (0.105, yc, 0.045), (0.03, 0.026, 0.016))
    p.box("HS_Led", (0.105, yc - 0.014, 0.045), (0.012, 0.003, 0.006))
    # spout
    p.obox("HS_White", (-0.083, yc, 0.222), (0.05, 0.042, 0.024), Matrix.Rotation(math.radians(-18), 3, "Y"))
    p.box("HS_Grey", (-0.1, yc, 0.205), (0.012, 0.03, 0.02))
    p.collider((0, yc, 0.13), (0.26, 0.2, 0.27))
    return p


def toaster():
    """Brushed steel two-slice toaster: slots on top, lever and browning dial on the right end."""
    p = Piece("HSToaster")
    yc = 0.06
    w, d, h = 0.28, 0.17, 0.19
    p.box("HS_Chrome", (0, yc, 0.012 + h / 2), (w, d - 0.02, h))
    for s in (-1, 1):
        p.rod("HS_Chrome", (0, yc + s * (d / 2 - 0.01), 0.012 + h / 2), 0.01, w, "X", 12)
        p.rod("HS_Chrome", (0, yc + s * (d / 2 - 0.01), 0.012 + h - 0.01), 0.01, w, "X", 12)
    p.box("HS_Chrome", (0, yc, 0.012 + h - 0.002), (w, d - 0.02, 0.004))
    # black end caps
    for s in (-1, 1):
        p.box("HS_Dark", (s * (w / 2 + 0.006), yc, 0.012 + h / 2), (0.012, d, h))
    # bread slots
    for s in (-1, 1):
        p.box("HS_Dark", (0, yc + s * 0.035, 0.012 + h + 0.0005), (0.22, 0.024, 0.002))
        p.box("HS_Grey", (0, yc + s * 0.035, 0.012 + h + 0.0002), (0.226, 0.03, 0.001))
    # lever in its slot, browning dial, cancel button
    xe = w / 2 + 0.012
    p.box("HS_Grey", (xe + 0.001, yc - 0.02, 0.11), (0.002, 0.01, 0.13))
    p.box("HS_Dark", (xe + 0.016, yc - 0.02, 0.16), (0.03, 0.03, 0.018))
    p.rod("HS_Grey", (xe + 0.005, yc + 0.035, 0.07), 0.016, 0.01, "X", 20)
    p.box("HS_White", (xe + 0.0105, yc + 0.035, 0.08), (0.001, 0.003, 0.01))
    p.box("HS_Red", (xe + 0.004, yc + 0.035, 0.12), (0.008, 0.016, 0.01))
    # crumb tray and feet
    p.box("HS_Dark", (0, yc - d / 2 - 0.001, 0.025), (0.12, 0.004, 0.012))
    for x in (-0.11, 0.11):
        for y in (-0.06, 0.06):
            p.rod("HS_Dark", (x, yc + y, 0.006), 0.01, 0.012, "Z", 10)
    p.collider((0, yc, 0.1), (0.31, 0.19, 0.21))
    return p


# ---------------------------------------------------------------- wheat crop (Collide=melee: walk through it)

def _wheat(name, height, stalk_mat, ear_mat, n, ears, seed):
    p = Piece(name)
    rnd = random.Random(seed)
    for _ in range(n):
        base = Vector((rnd.uniform(-0.36, 0.36), rnd.uniform(-0.36, 0.36), 0.0))
        h = height * rnd.uniform(0.8, 1.1)
        lean = Vector((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), 1.0)).normalized()
        top = base + lean * h
        p.seg(stalk_mat, base, top, 0.0045, 0.003, 5)
        # two leaves curving out from the lower stalk
        for k in range(2):
            a = rnd.uniform(0, 2 * math.pi)
            lb = base + lean * (h * (0.15 + 0.2 * k))
            out = Vector((math.cos(a), math.sin(a), 0.0))
            lt = lb + out * h * 0.22 + Vector((0, 0, h * 0.18))
            p.seg(stalk_mat, lb, lt, 0.006, 0.001, 4)
        if ears:
            ear_top = top + lean * 0.09
            p.seg(ear_mat, top, ear_top, 0.011, 0.006, 6)
            for j in range(3):
                q = top + lean * (0.03 * j + 0.03)
                aw = q + Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 0.07))
                p.seg(ear_mat, q, aw, 0.0012, 0.0006, 3)
    p.collider((0, 0, height / 2), (0.8, 0.8, max(height, 0.2)))
    return p


def wheat_1():
    """Wheat seedlings."""
    return _wheat("HSWheat1", 0.16, "HS_Green", "HS_Green", 45, False, 11)


def wheat_2():
    """Green wheat in ear."""
    return _wheat("HSWheat2", 0.6, "HS_Green", "HS_Green", 55, True, 12)


def wheat_3():
    """Ripe golden wheat, ready to harvest."""
    return _wheat("HSWheat3", 0.9, "HS_Straw", "HS_Wheat", 60, True, 13)


# ---------------------------------------------------------------- icon-only pieces (inventory pictures, not exported)

def _mug(name, drink=None, tag=None):
    p = Piece(name)
    r, h = 0.12, 0.26
    p.seg("HS_White", (0, 0, 0), (0, 0, h), r, r * 1.04, 40)
    p.rod("HS_Grey" if drink is None else drink, (0, 0, h + 0.002), r * 0.9, 0.004, "Z", 40)
    # handle: a ring of short bars on the right
    pts = []
    for i in range(9):
        a = math.radians(-80 + i * 20)
        pts.append(Vector((r + 0.02 + 0.06 * math.cos(a), 0, h * 0.5 + 0.075 * math.sin(a))))
    for a_, b_ in zip(pts, pts[1:]):
        p.seg("HS_White", a_, b_, 0.016, None, 12)
    if tag is not None:
        # tea bag string over the rim and its paper tag hanging outside
        p.seg("HS_White", (-r * 0.5, -r * 0.3, h - 0.03), (-r * 1.02, -r * 0.4, h + 0.01), 0.002, None, 4)
        p.seg("HS_White", (-r * 1.02, -r * 0.4, h + 0.01), (-r * 1.1, -r * 0.45, h - 0.07), 0.002, None, 4)
        p.box(tag, (-r * 1.12, -r * 0.46, h - 0.1), (0.008, 0.05, 0.06))
    return p


def icon_mug():
    return _mug("HSIconMug")


def icon_mug_tea_pj():
    return _mug("HSIconMugTeaPJ", "HS_Tea", "HS_TeaGreen")


def icon_mug_tea_yorkie():
    return _mug("HSIconMugTeaYorkie", "HS_Tea", "HS_Red")


def icon_mug_coffee():
    return _mug("HSIconMugCoffee", "HS_Coffee")


def _tea_box(name, body, band, stripe):
    p = Piece(name)
    w, d, h = 0.3, 0.16, 0.36
    p.box(body, (0, 0, h / 2), (w, d, h))
    p.box(band, (0, -d / 2 - 0.001, h * 0.32), (w + 0.002, 0.002, 0.07))
    p.box(stripe, (0, -d / 2 - 0.002, h * 0.32), (w + 0.002, 0.002, 0.015))
    p.rod("HS_White", (0, -d / 2 - 0.003, h * 0.66), 0.075, 0.003, "Y", 32)
    # a leaf on the label
    p.obox(body, (0, -d / 2 - 0.0055, h * 0.66), (0.03, 0.002, 0.08), Matrix.Rotation(math.radians(30), 3, "Y"))
    p.box(band, (-d * 0.5, 0, h + 0.001), (0.12, 0.08, 0.002))
    return p


def icon_tea_pj():
    """PJ Tops: green box, red band."""
    return _tea_box("HSIconTeaPJ", "HS_TeaGreen", "HS_Red", "HS_Gold")


def icon_tea_yorkie():
    """Yorkie Brew: red box, gold band."""
    return _tea_box("HSIconTeaYorkie", "HS_Red", "HS_Gold", "HS_TeaGreen")


def _ear(p, base, d, mat, size=1.0):
    """Ear of wheat: two rows of plump grains up a stem, whiskers off the top grains."""
    side = d.cross(Vector((0, 1, 0)))
    if side.length < 1e-4:
        side = Vector((1, 0, 0))
    side.normalize()
    p.seg(mat, base, base + d * 0.13 * size, 0.003 * size, None, 6)
    for j in range(7):
        q = base + d * (0.012 + 0.017 * j) * size
        for s in (-1, 1):
            g = q + side * s * 0.009 * size + d * 0.004 * s * size
            p.ball(mat, tuple(g), 0.0105 * size * (1.0 - 0.06 * j), (0.8, 0.8, 1.35), 10)
            if j >= 3:
                p.seg(mat, g, g + (d * 1.0 + side * s * 0.35).normalized() * 0.07 * size, 0.0014 * size, 0.0006, 3)


def icon_wheat():
    """Sheaf of harvested wheat tied with twine."""
    p = Piece("HSIconWheat")
    for i in range(9):
        a = (i - 4) * 0.06
        d = Vector((math.sin(a), 0.0, math.cos(a)))
        base = Vector((math.sin(a) * 0.02, (i % 3 - 1) * 0.02, 0.0))
        top = base + d * 0.34
        p.seg("HS_Straw", base, top, 0.006, None, 6)
        _ear(p, top, d, "HS_Wheat")
    p.rod("HS_Paper", (0, 0, 0.12), 0.04, 0.03, "Z", 20)
    return p


def icon_flour():
    """Paper bag of flour with a wheat ear on the label."""
    p = Piece("HSIconFlour")
    w, d, h = 0.24, 0.14, 0.32
    p.box("HS_Paper", (0, 0, h / 2), (w, d, h))
    p.obox("HS_Paper", (0, 0, h + 0.02), (w, 0.02, 0.05), Matrix.Rotation(math.radians(8), 3, "X"))
    p.box("HS_White", (0, -d / 2 - 0.001, h * 0.45), (w * 0.8, 0.002, h * 0.5))
    p.box("HS_Display", (0, -d / 2 - 0.002, h * 0.75), (w * 0.8, 0.002, 0.025))
    _ear(p, Vector((0, -d / 2 - 0.01, h * 0.28)), Vector((0, 0, 1)), "HS_Wheat", 1.0)
    return p


def icon_dough():
    p = Piece("HSIconDough")
    p.ball("HS_Dough", (0, 0, 0.09), 0.17, (1.0, 0.9, 0.55), 32)
    p.ball("HS_White", (0.05, -0.05, 0.17), 0.02, (1, 1, 0.3), 10)
    return p


def icon_loaf():
    """Tin loaf: domed top, scored."""
    p = Piece("HSIconLoaf")
    w, d, h = 0.34, 0.18, 0.14
    p.box("HS_Crust", (0, 0, h / 2), (w, d, h))
    # the top rises over the tin edges
    p.seg("HS_Crust", (-w / 2 - 0.004, 0, h), (w / 2 + 0.004, 0, h), d / 2 + 0.006, None, 40)
    for i in range(3):
        p.obox("HS_ToastCrust", (-0.09 + i * 0.09, 0, h + d / 2 + 0.004), (0.018, d * 0.45, 0.006), Matrix.Rotation(math.radians(30), 3, "Z"))
    return p


def _slice(name, face, crust):
    p = Piece(name)
    s, t = 0.26, 0.03
    zt = s * 0.62
    p.box(crust, (0, 0, zt / 2), (s, t, zt))
    p.seg(crust, (0, -t / 2, zt), (0, t / 2, zt), s * 0.54, None, 48, smooth=False)
    p.box(face, (0, 0, zt / 2 + 0.008), (s - 0.024, t + 0.006, zt - 0.008))
    p.seg(face, (0, -t / 2 - 0.003, zt), (0, t / 2 + 0.003, zt), s * 0.54 - 0.013, None, 48, smooth=False)
    return p


def icon_slice():
    return _slice("HSIconSlice", "HS_Bread", "HS_Crust")


def icon_toast():
    return _slice("HSIconToast", "HS_Toast", "HS_ToastCrust")


def icon_wheat_seed():
    """Seed packet with a wheat ear on the front."""
    p = Piece("HSIconWheatSeed")
    w, d, h = 0.24, 0.03, 0.34
    p.box("HS_Paper", (0, 0, h / 2), (w, d, h))
    p.box("HS_Green", (0, -d / 2 - 0.001, h * 0.85), (w, 0.002, 0.06))
    p.box("HS_White", (0, -d / 2 - 0.001, h * 0.42), (w * 0.8, 0.002, h * 0.55))
    _ear(p, Vector((0, -d / 2 - 0.012, h * 0.2)), Vector((0, 0, 1)), "HS_Wheat", 1.3)
    p.seg("HS_Straw", (0, -d / 2 - 0.012, h * 0.15), (0, -d / 2 - 0.012, h * 0.2), 0.005, None, 6)
    return p


def _tv_panel(p, w, h, zc, yc):
    """Slim flat-screen panel centred at (0, yc, zc): black bezel, thin chin with a badge and standby light, picture quad in front."""
    d = 0.02
    chin = 0.018
    bezel = 0.008
    p.box("HS_Dark", (0, yc, zc), (w, d, h))
    # deeper electronics box on the back, lower half
    p.box("HS_Dark", (0, yc + d / 2 + 0.02, zc - h * 0.08), (w * 0.62, 0.04, h * 0.55))
    yf = yc - d / 2
    p.box("HS_Grey", (0, yf - 0.002, zc - h / 2 + chin / 2), (w, 0.004, chin))
    p.box("HS_Chrome", (0, yf - 0.0045, zc - h / 2 + chin / 2), (0.05, 0.001, 0.006))
    p.box("HS_Red", (w / 2 - 0.04, yf - 0.0045, zc - h / 2 + chin / 2), (0.005, 0.001, 0.003))
    s = p.sub("Screen")
    s.screen("HS_Screen", -w / 2 + bezel, w / 2 - bezel, zc - h / 2 + chin, zc + h / 2 - bezel, yf - 0.001)
    return yf


def tv_stand():
    """43 inch flat-screen TV on a pedestal stand, for a TV unit, worktop or the floor."""
    p = Piece("HSTVStand")
    w, h = 0.96, 0.56
    yc = 0.0
    z0 = 0.075
    _tv_panel(p, w, h, z0 + h / 2, yc)
    # pedestal: flat foot plate and a neck into the back of the panel
    p.box("HS_Grey", (0, yc + 0.02, 0.006), (0.34, 0.2, 0.012))
    p.box("HS_Dark", (0, yc + 0.02, 0.0135), (0.3, 0.17, 0.003))
    p.box("HS_Grey", (0, yc + 0.045, (0.012 + z0 + 0.12) / 2), (0.07, 0.025, z0 + 0.12 - 0.012))
    p.collider((0, yc + 0.02, (z0 + h) / 2 + 0.01), (w, 0.22, z0 + h))
    return p


def tv_wall():
    """43 inch flat-screen TV on a slim wall bracket, hung at sitting eye height."""
    p = Piece("HSTVWall")
    w, h = 0.96, 0.56
    yc = WALL - 0.06
    zc = 0.55
    _tv_panel(p, w, h, zc, yc)
    # bracket plate on the wall and arms to the back of the set
    p.box("HS_Grey", (0, WALL - 0.004, zc), (0.3, 0.008, 0.2))
    for x in (-0.1, 0.1):
        p.box("HS_Grey", (x, WALL - 0.02, zc), (0.02, 0.03, 0.18))
    p.collider((0, (yc - 0.02 + WALL) / 2, zc), (w, WALL - yc + 0.02, h))
    return p


# ---------------------------------------------------------------- consumer unit / breaker panel
# Each way i has hidden children the game picks between: W<i>Fuse, W<i>Blown (fuse fitted, good or blown),
# W<i>On, W<i>Off (breaker fitted, closed or off/tripped). Nothing fitted shows the bare slot. MainOn / MainOff
# are the main switch.

def _uk_fuse_carrier(s, x, zc, yf, blown):
    """BS 3036 rewireable fuse carrier: white body, grip ridges, coloured dot; blown ones are scorched."""
    s.box("HS_White", (x, yf - 0.014, zc), (0.03, 0.028, 0.06))
    for dz in (-0.018, 0.0, 0.018):
        s.box("HS_Grey", (x, yf - 0.0285, zc + dz), (0.026, 0.002, 0.004))
    if blown:
        s.box("HS_Dark", (x + 0.004, yf - 0.0292, zc - 0.006), (0.016, 0.001, 0.03))
    else:
        s.rod("HS_Red", (x, yf - 0.029, zc + 0.024), 0.004, 0.002, "Y", 10)


def _uk_mcb(s, x, zc, yf, up):
    """DIN rail MCB: white module, dark toggle up for on, down for off/tripped."""
    s.box("HS_White", (x, yf - 0.015, zc), (0.032, 0.03, 0.066))
    s.box("HS_Grey", (x, yf - 0.0305, zc), (0.022, 0.001, 0.03))
    s.box("HS_Dark", (x, yf - 0.036, zc + (0.009 if up else -0.009)), (0.012, 0.012, 0.016))
    s.box("HS_Red" if up else "HS_Green", (x, yf - 0.0305, zc - 0.024), (0.014, 0.001, 0.006))


def consumer_unit_uk():
    """UK consumer unit: white metal-clad box, 6 ways on a DIN rail and a red double-pole main switch."""
    p = Piece("HSConsumerUnitUK")
    zc = 0.5
    w, h, d = 0.4, 0.22, 0.1
    yf = WALL - d
    p.box("HS_White", (0, WALL - d / 2, zc), (w, d, h))
    p.box("HS_Grey", (0, yf - 0.001, zc), (w - 0.03, 0.002, 0.09))
    p.box("HS_White", (0, yf - 0.001, zc + 0.075), (w - 0.03, 0.002, 0.03))
    p.box("HS_Dark", (0, yf - 0.0025, zc - 0.07), (w - 0.06, 0.001, 0.012))
    for i in range(6):
        x = -0.15 + i * 0.036
        p.box("HS_Dark", (x, yf - 0.0025, zc), (0.03, 0.001, 0.06))
        _uk_fuse_carrier(p.sub("W%dFuse" % i), x, zc, yf, False)
        _uk_fuse_carrier(p.sub("W%dBlown" % i), x, zc, yf, True)
        _uk_mcb(p.sub("W%dOn" % i), x, zc, yf, True)
        _uk_mcb(p.sub("W%dOff" % i), x, zc, yf, False)
    xm = 0.12
    p.box("HS_White", (xm, yf - 0.015, zc), (0.07, 0.03, 0.066))
    for state, up in (("MainOn", True), ("MainOff", False)):
        s = p.sub(state)
        s.box("HS_Red", (xm, yf - 0.037, zc + (0.01 if up else -0.01)), (0.05, 0.014, 0.018))
    p.collider((0, WALL - d / 2, zc), (w, d, h))
    return p


def _us_breaker(s, x, zc, yf, left, on):
    """Plug-in breaker: black body across the row, lever towards the middle of the panel when on."""
    s.box("HS_Dark", (x, yf - 0.012, zc), (0.07, 0.024, 0.026))
    inward = 1 if left else -1
    lx = x + (0.01 * inward if on else -0.01 * inward)
    s.box("HS_Dark", (lx, yf - 0.03, zc), (0.012, 0.014, 0.012))
    s.box("HS_White", (x - 0.024 * inward, yf - 0.0245, zc), (0.012, 0.001, 0.008))


def _us_plug_fuse(s, x, zc, yf, blown):
    """Edison screw plug fuse: brass rim and a window, dark when blown."""
    s.rod("HS_Copper", (x, yf - 0.006, zc), 0.014, 0.012, "Y", 16)
    s.rod("HS_Chrome", (x, yf - 0.014, zc), 0.012, 0.006, "Y", 16)
    s.rod("HS_Dark" if blown else "HS_Copper", (x, yf - 0.0175, zc), 0.008, 0.002, "Y", 12)


def breaker_panel_us():
    """US load center: grey steel box, dead front, main breaker on top and 8 ways in two columns."""
    p = Piece("HSBreakerPanelUS")
    zc = 0.5
    w, h, d = 0.36, 0.56, 0.1
    yf = WALL - d
    p.box("HS_Grey", (0, WALL - d / 2, zc), (w, d, h))
    p.box("HS_Chrome", (0, yf - 0.001, zc), (w - 0.04, 0.002, h - 0.04))
    for i in range(8):
        col, row = i % 2, i // 2
        left = col == 0
        x = -0.05 if left else 0.05
        z = zc + 0.09 - row * 0.06
        p.box("HS_Dark", (x, yf - 0.0025, z), (0.07, 0.001, 0.026))
        _us_plug_fuse(p.sub("W%dFuse" % i), x, z, yf, False)
        _us_plug_fuse(p.sub("W%dBlown" % i), x, z, yf, True)
        _us_breaker(p.sub("W%dOn" % i), x, z, yf, left, True)
        _us_breaker(p.sub("W%dOff" % i), x, z, yf, left, False)
    zm = zc + 0.2
    p.box("HS_Dark", (0, yf - 0.014, zm), (0.09, 0.028, 0.05))
    for state, up in (("MainOn", True), ("MainOff", False)):
        s = p.sub(state)
        s.box("HS_Dark", (0, yf - 0.034, zm + (0.01 if up else -0.01)), (0.05, 0.014, 0.014))
    p.box("HS_White", (0, yf - 0.0025, zc - 0.2), (0.2, 0.001, 0.05))
    p.collider((0, WALL - d / 2, zc), (w, d, h))
    return p


def icon_fuse():
    """Cartridge fuse: pale ceramic body and metal end caps (the item icon is tinted per rating)."""
    p = Piece("HSIconFuse")
    p.rod("HS_White", (0, 0, 0.1), 0.05, 0.16, "X", 24)
    for x in (-0.095, 0.095):
        p.rod("HS_Chrome", (x, 0, 0.1), 0.055, 0.04, "X", 24)
    p.box("HS_Grey", (0, -0.05, 0.1), (0.08, 0.002, 0.03))
    return p


def icon_fuse_blown():
    p = Piece("HSIconFuseBlown")
    p.rod("HS_White", (0, 0, 0.1), 0.05, 0.16, "X", 24)
    for x in (-0.095, 0.095):
        p.rod("HS_Chrome", (x, 0, 0.1), 0.055, 0.04, "X", 24)
    p.ball("HS_Dark", (0.01, -0.035, 0.11), 0.035, (1.4, 0.5, 1.0), 16)
    return p


def icon_breaker():
    """DIN rail breaker module, pale so the icon tint shows the rating."""
    p = Piece("HSIconBreaker")
    p.box("HS_White", (0, 0, 0.1), (0.07, 0.11, 0.18))
    p.box("HS_Grey", (0, -0.056, 0.1), (0.05, 0.002, 0.08))
    p.box("HS_Dark", (0, -0.07, 0.13), (0.026, 0.03, 0.04))
    return p


# ---------------------------------------------------------------- washing line / clothes airer
# Hidden children "Cloth0".."Cloth<n>": the game shows as many as there are clothes hung up.

def _peg(p, x, z, y=0.0, s=1.0):
    """Wooden spring peg astride a line at height z: two legs and the wire spring."""
    for side in (-1, 1):
        p.box("HS_Wood", (x, y + side * 0.007 * s, z - 0.022 * s), (0.012 * s, 0.006 * s, 0.075 * s))
    p.box("HS_Chrome", (x, y, z - 0.012 * s), (0.015 * s, 0.02 * s, 0.008 * s))


LINE_HX = 2.25      # posts at +-LINE_HX
LINE_Z = 1.95       # line height at the posts
LINE_SAG = 0.05


def _line_z(x):
    return LINE_Z - LINE_SAG * (1.0 - (x / LINE_HX) ** 2)


def _shirt(s, x, zl, mat):
    s.box(mat, (x, 0, zl - 0.23), (0.32, 0.012, 0.42))
    s.box(mat, (x, 0, zl - 0.02), (0.20, 0.014, 0.04))
    for side in (-1, 1):
        s.obox(mat, (x + side * 0.19, 0, zl - 0.1), (0.09, 0.012, 0.2), Matrix.Rotation(math.radians(side * 35), 3, "Y"))
    for side in (-1, 1):
        _peg(s, x + side * 0.13, zl)


def _trousers(s, x, zl, mat):
    s.box(mat, (x, 0, zl - 0.03), (0.32, 0.016, 0.06))
    for side in (-1, 1):
        s.box(mat, (x + side * 0.085, 0, zl - 0.33), (0.15, 0.012, 0.56))
        _peg(s, x + side * 0.14, zl)


def _towel(s, x, zl, mat, band):
    s.box(mat, (x, 0, zl - 0.3), (0.38, 0.01, 0.6))
    for dz in (0.08, 0.5):
        s.box(band, (x, 0, zl - dz), (0.38, 0.013, 0.035))
    for side in (-1, 1):
        _peg(s, x + side * 0.16, zl)


def _socks(s, x, zl, mat):
    for side in (-1, 1):
        sx = x + side * 0.09
        s.box(mat, (sx, 0, zl - 0.12), (0.07, 0.012, 0.24))
        s.box(mat, (sx + side * 0.025, 0, zl - 0.26), (0.12, 0.012, 0.06))
        _peg(s, sx, zl)


def washing_line():
    """Garden washing line (MultiBlockDim 5,2,1): two wooden posts, a sagging line held up by a clothes prop, pegs."""
    p = Piece("HSWashingLine")
    p.split_cols = True
    for side in (-1, 1):
        x = side * LINE_HX
        p.box("HS_Grey", (x, 0, 0.03), (0.22, 0.22, 0.06))
        p.box("HS_Wood", (x, 0, 1.0), (0.09, 0.09, 2.0))
        p.box("HS_Wood", (x, 0, 2.01), (0.11, 0.11, 0.02))
        p.rod("HS_Chrome", (x - side * 0.055, 0, LINE_Z), 0.012, 0.02, "X", 12)
        p.box("HS_Wood", (x - side * 0.12, 0, 0.35), (0.04, 0.05, 0.3))
    n = 16
    for i in range(n):
        x0 = -LINE_HX + 2 * LINE_HX * i / n
        x1 = -LINE_HX + 2 * LINE_HX * (i + 1) / n
        p.seg("HS_Cord", (x0, 0, _line_z(x0)), (x1, 0, _line_z(x1)), 0.004, segs=6)
    # clothes prop: a long pole from the ground up to a fork under the middle of the line
    top = (0.0, 0.0, _line_z(0.0) - 0.01)
    p.seg("HS_Wood", (0.25, -0.42, 0.0), top, 0.018, 0.014, segs=8)
    for side in (-1, 1):
        p.seg("HS_Wood", top, (side * 0.03, 0.0, top[2] + 0.05), 0.008, segs=6)
    # spare pegs along the line
    for x in (-2.05, -1.98, -0.05, 0.05, 1.98, 2.05):
        _peg(p, x, _line_z(x))
    looks = [
        (_shirt, ("HS_White",)), (_trousers, ("HS_Denim",)), (_towel, ("HS_ClothYellow", "HS_White")),
        (_socks, ("HS_Dark",)), (_shirt, ("HS_ClothBlue",)), (_towel, ("HS_ClothPink", "HS_White")),
        (_trousers, ("HS_Grey",)), (_shirt, ("HS_Red",)),
    ]
    for i, (make, mats) in enumerate(looks):
        x = -1.75 + i * 0.5
        make(p.sub("Cloth%d" % i), x, _line_z(x) - 0.004, *mats)
    for side in (-1, 1):
        p.collider((side * LINE_HX, 0, 1.0), (0.16, 0.16, 2.0))
    p.collider((0, 0, LINE_Z - 0.03), (2 * LINE_HX, 0.1, 0.08))
    return p


def _drape(s, mat, x, y, z, w, h, band=None):
    """Something folded over a rail: a roll on top and a flap down each side."""
    s.rod(mat, (x, y, z), 0.012, w, "X", 10)
    for side in (-1, 1):
        s.box(mat, (x, y + side * 0.011, z - h / 2), (w, 0.006, h))
        if band:
            s.box(band, (x, y + side * 0.0125, z - h + 0.05), (w, 0.006, 0.03))


def clothes_airer():
    """Gullwing clothes airer (MultiBlockDim 2,1,1): white-coated rails on an A-frame, wings dropped a little."""
    p = Piece("HSClothesAirer")
    hx = 0.68
    zt = 0.95
    mat = "HS_White"
    for side in (-1, 1):
        x = side * hx
        for ys in (-1, 1):
            p.seg(mat, (x, ys * 0.27, 0.0), (x, ys * 0.03, zt), 0.01, segs=8)
            p.box("HS_Grey", (x, ys * 0.27, 0.01), (0.03, 0.04, 0.02))
        p.seg(mat, (x, -0.38, zt - 0.1), (x, 0.38, zt - 0.1), 0.008, segs=8)
    for y in (-0.12, -0.04, 0.04, 0.12):
        p.rod(mat, (0, y, zt), 0.006, 2 * hx, "X", 10)
    for ys in (-1, 1):
        for k, y in enumerate((0.2, 0.28, 0.36)):
            p.rod(mat, (0, ys * y, zt - 0.04 - k * 0.03), 0.006, 2 * hx, "X", 10)
        p.rod(mat, (0, ys * 0.15, 0.45), 0.006, 2 * hx, "X", 10)
    looks = [
        (-0.4, -0.12, "HS_ClothBlue", "HS_White", 0.3), (0.05, -0.04, "HS_White", None, 0.28),
        (0.42, -0.2, "HS_ClothPink", None, 0.25), (-0.38, 0.12, "HS_ClothYellow", "HS_White", 0.32),
        (0.02, 0.2, "HS_Denim", None, 0.35), (0.42, 0.04, "HS_Red", None, 0.2),
    ]
    for i, (x, y, cm, band, h) in enumerate(looks):
        z = zt if abs(y) < 0.15 else zt - 0.04 - [0.2, 0.28, 0.36].index(abs(y)) * 0.03
        _drape(p.sub("Cloth%d" % i), cm, x, y, z, 0.3, h, band)
    p.collider((0, 0, 0.5), (2 * hx + 0.06, 0.8, 1.0))
    return p


def toilet():
    """Close-coupled toilet: bowl and seat facing the room, cistern against the wall, flush on the cistern."""
    p = Piece("HSToilet")
    p.box("HS_White", (0, 0.02, 0.10), (0.18, 0.20, 0.20))
    p.box("HS_White", (0, -0.04, 0.28), (0.40, 0.50, 0.22))
    p.box("HS_White", (0, -0.04, 0.40), (0.46, 0.56, 0.06))
    p.box("HS_White", (0, -0.06, 0.445), (0.40, 0.48, 0.04))
    p.box("HS_White", (0, 0.18, 0.62), (0.40, 0.045, 0.32))
    p.box("HS_White", (0, 0.24, 0.62), (0.40, 0.18, 0.34))
    p.rod("HS_Chrome", (0.16, 0.12, 0.70), 0.012, 0.08, "Y", 10)
    p.box("HS_Chrome", (0.16, 0.08, 0.70), (0.03, 0.02, 0.05))
    w = p.sub("Water")
    w.box("HS_Water", (0, -0.04, 0.36), (0.26, 0.30, 0.025))
    s = p.sub("Soil")
    s.box("HS_Soil", (0, -0.04, 0.37), (0.24, 0.26, 0.03))
    p.collider((0, 0.02, 0.40), (0.50, 0.64, 0.78))
    return p


def urinal():
    """Wall-hung urinal. The back sits on the wall the block was placed against."""
    p = Piece("HSUrinal")
    y = WALL - 0.10
    p.box("HS_White", (0, y, 0.78), (0.32, 0.16, 0.42))
    p.box("HS_White", (0, y - 0.08, 0.58), (0.30, 0.14, 0.14))
    p.box("HS_White", (0, y - 0.02, 0.50), (0.34, 0.06, 0.04))
    p.rod("HS_Chrome", (0, y + 0.02, 1.02), 0.018, 0.10, "Z", 12)
    p.box("HS_Chrome", (0.12, y - 0.09, 0.88), (0.05, 0.025, 0.07))
    w = p.sub("Water")
    w.box("HS_Water", (0, y - 0.06, 0.56), (0.16, 0.08, 0.02))
    s = p.sub("Soil")
    s.box("HS_Soil", (0, y - 0.06, 0.57), (0.14, 0.07, 0.02))
    p.collider((0, WALL - 0.12, 0.72), (0.38, 0.30, 0.70))
    return p


def icon_pegs():
    """A few clothes pegs, big enough to read as an icon."""
    p = Piece("HSIconPegs")
    for i, (x, y, rz) in enumerate(((-0.08, 0.0, 10), (0.02, 0.05, -15), (0.1, -0.03, 25))):
        rot = Matrix.Rotation(math.radians(rz), 3, "Y")
        for side in (-1, 1):
            p.obox("HS_Wood", (x, y + side * 0.018, 0.1), (0.03, 0.016, 0.22), rot)
        p.obox("HS_Chrome", (x, y, 0.13), (0.036, 0.05, 0.02), rot)
    return p


PIECES = [combi_boiler, panel_radiator, chest_freezer, split_air_con, ceiling_air_con, wall_thermometer,
          washing_machine, tumble_dryer, fridge_freezer,
          socket_uk_single, socket_uk_double, socket_us_single, socket_us_double, light_switch_uk, light_switch_us, cooker_switch_uk, range_outlet_us,
          kettle, toaster, wheat_1, wheat_2, wheat_3, tv_stand, tv_wall, consumer_unit_uk, breaker_panel_us,
          washing_line, clothes_airer, toilet, urinal]

ICON_PIECES = [icon_mug, icon_mug_tea_pj, icon_mug_tea_yorkie, icon_mug_coffee, icon_tea_pj, icon_tea_yorkie,
               icon_wheat, icon_flour, icon_dough, icon_loaf, icon_slice, icon_toast, icon_wheat_seed,
               icon_fuse, icon_fuse_blown, icon_breaker, icon_pegs]


def clear_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def export_piece(root, path):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for c in root.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        bake_anim=False,
    )


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    log = []
    for i, make in enumerate(PIECES + ICON_PIECES):
        piece = make()
        coll = bpy.data.collections.new(piece.name)
        scene.collection.children.link(coll)
        root = piece.build(coll)
        bpy.context.view_layer.update()
        if make in PIECES:
            path = os.path.join(OUT_DIR, piece.name + ".fbx")
            export_piece(root, path)
            log.append("%s verts=%d cols=%d -> %s" % (piece.name, len(root.data.vertices), len(root.children), path))
        else:
            log.append("%s verts=%d (icon only)" % (piece.name, len(root.data.vertices)))
        root.location.x = i * 3.0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
        f.write("\n".join(log) + "\nDONE\n")


try:
    main()
except Exception:
    import traceback
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
        f.write("FAILED\n" + traceback.format_exc())
    raise
