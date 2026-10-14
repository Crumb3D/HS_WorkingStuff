# Inventory icons: blender --background hsws_models.blend --python hsws_icons.py -- <out_dir>
import bpy
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.path.join(os.path.dirname(bpy.data.filepath), "..", "UIAtlases", "ItemIconAtlas")
os.makedirs(OUT, exist_ok=True)

ICONS = [
    ("HSCombiBoiler", "hsCombiBoiler"),
    ("HSPanelRadiator", "hsPanelRadiator"),
    ("HSChestFreezer", "hsChestFreezer"),
    ("HSSplitAirCon", "hsSplitAirCon"),
    ("HSCeilingAirCon", "hsCeilingAirCon", -35),
    ("HSWallThermometer", "hsWallThermometer"),
    ("HSWashingMachine", "hsWashingMachine"),
    ("HSTumbleDryer", "hsTumbleDryer"),
    ("HSFridgeFreezer", "hsFridgeFreezer"),
    ("HSSocketUKSingle", "hsSocketUKSingle"),
    ("HSSocketUKDouble", "hsSocketUKDouble"),
    ("HSSocketUSSingle", "hsSocketUSSingle"),
    ("HSSocketUSDouble", "hsSocketUSDouble"),
    ("HSLightSwitchUK", "hsLightSwitchUK"),
    ("HSLightSwitchUS", "hsLightSwitchUS"),
    ("HSCookerSwitchUK", "hsCookerSwitchUK"),
    ("HSRangeOutletUS", "hsRangeOutletUS"),
    ("HSKettle", "hsKettle"),
    ("HSToaster", "hsToaster"),
    ("HSIconWheatSeed", "hsPlantedWheat1"),
    ("HSIconWheat", "hsCropWheat"),
    ("HSIconFlour", "hsFlour"),
    ("HSIconDough", "hsDough", 30),
    ("HSIconLoaf", "hsBreadLoaf"),
    ("HSIconSlice", "hsBreadSlice"),
    ("HSIconToast", "hsToast"),
    ("HSIconMug", "hsMug", 40),
    ("HSIconMugTeaPJ", "hsMugTeaPJ", 40),
    ("HSIconMugTeaYorkie", "hsMugTeaYorkie", 40),
    ("HSIconMugCoffee", "hsMugCoffee", 40),
    ("HSIconTeaPJ", "hsTeaBagsPJ"),
    ("HSIconTeaYorkie", "hsTeaBagsYorkie"),
]

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 232
scene.render.resolution_y = 160
scene.render.film_transparent = True
# Blender's default AgX view greys out light colours (white goods, bread, wheat).
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"

for ob in bpy.data.objects:
    if ob.name.startswith("COL_"):
        ob.hide_render = True
        ob.hide_viewport = True

cam_data = bpy.data.cameras.new("icon_cam")
cam_data.lens = 45
cam = bpy.data.objects.new("icon_cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def mesh_objects(root):
    out = []
    stack = [root]
    while stack:
        ob = stack.pop()
        if ob.type == "MESH" and not ob.name.startswith("COL_") and "_Plug" not in ob.name and "_SwOff" not in ob.name:
            out.append(ob)
        stack.extend(list(ob.children))
    return out


def world_bounds(obs):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    anyv = False
    for ob in obs:
        for corner in ob.bound_box:
            w = ob.matrix_world @ Vector(corner)
            mn.x, mn.y, mn.z = min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)
            mx.x, mx.y, mx.z = max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)
            anyv = True
    if not anyv:
        return Vector((0, 0, 0)), 1.0, 1.0
    return (mn + mx) * 0.5, max((mx - mn).length, 0.35), mx.z - mn.z


def hide_all():
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            ob.hide_render = True


log = []
for entry in ICONS:
    obj_name, icon_name = entry[0], entry[1]
    pitch_deg = entry[2] if len(entry) > 2 else 15
    root = bpy.data.objects.get(obj_name)
    if root is None:
        log.append("MISSING " + obj_name)
        continue
    hide_all()
    for ob in mesh_objects(root):
        ob.hide_render = False
    center, size, height = world_bounds(mesh_objects(root))
    # The frame is landscape (232x160): tall pieces like the fridge freezer need the camera further back to fit top to bottom.
    dist = max(size * 1.35, height * 2.2)
    yaw, pitch = math.radians(-60), math.radians(pitch_deg)
    d = Vector((math.cos(pitch) * math.cos(yaw), math.cos(pitch) * math.sin(yaw), math.sin(pitch)))
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, icon_name + ".png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log.append(icon_name + " -> " + path)

with open(os.path.join(os.path.dirname(bpy.data.filepath), "_icons_log.txt"), "w") as f:
    f.write("\n".join(log) + "\nDONE\n")
