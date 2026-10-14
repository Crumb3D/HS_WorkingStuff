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
    "HS_Red": (0.78, 0.08, 0.06, 1.0),
    "HS_Neon": (1.0, 0.25, 0.1, 1.0),
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

    def collider(self, center, size):
        self.cols.append((tuple(center), tuple(size)))

    def build(self, collection):
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
            ob = bpy.data.objects.new("COL_%d" % i, cm)
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


PIECES = [combi_boiler, panel_radiator, chest_freezer, split_air_con, ceiling_air_con, wall_thermometer,
          washing_machine, tumble_dryer, fridge_freezer,
          socket_uk_single, socket_uk_double, socket_us_single, socket_us_double, light_switch_uk, light_switch_us, cooker_switch_uk, range_outlet_us]


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
    for i, make in enumerate(PIECES):
        piece = make()
        coll = bpy.data.collections.new(piece.name)
        scene.collection.children.link(coll)
        root = piece.build(coll)
        bpy.context.view_layer.update()
        path = os.path.join(OUT_DIR, piece.name + ".fbx")
        export_piece(root, path)
        log.append("%s verts=%d cols=%d -> %s" % (piece.name, len(root.data.vertices), len(root.children), path))
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
