import bpy
import math
from mathutils import Vector
from pathlib import Path

# -----------------------------------------------------------------------------
# Cute Ankylosaurus prototype
# Run this file in Blender's Scripting workspace or with:
# blender --background --python create_dino.py
# -----------------------------------------------------------------------------

EXPORT_GLB = False
RENDER_SPRITES = False
RENDER_PREVIEWS = True
PROJECT_DINO_DIR = Path(__file__).resolve().parent.parent
EXPORT_DIR = PROJECT_DINO_DIR / "model"
BLEND_PATH = PROJECT_DINO_DIR / "source" / "Dino.blend"
SPRITE_DIR = Path(__file__).resolve().parents[2] / "Sprites" / "Dino"
MODEL_COLLECTION_NAME = "Cute_Ankylosaurus"

# A small, readable palette that works well in a game engine.
COLORS = {
    "body": (0.22, 0.68, 0.60, 1.0),
    "body_light": (0.40, 0.82, 0.72, 1.0),
    "belly": (0.88, 0.80, 0.62, 1.0),
    "shell": (0.64, 0.53, 0.34, 1.0),
    "shell_light": (0.93, 0.82, 0.60, 1.0),
    "spot": (0.10, 0.45, 0.42, 1.0),
    "cheek": (0.92, 0.48, 0.48, 1.0),
    "eye": (0.012, 0.018, 0.022, 1.0),
    "eye_iris": (0.18, 0.30, 0.32, 1.0),
    "eye_glint": (1.0, 1.0, 0.92, 1.0),
    "mouth": (0.22, 0.08, 0.09, 1.0),
    "tongue": (0.92, 0.38, 0.42, 1.0),
    "claw": (0.15, 0.24, 0.20, 1.0),
}


def material(name, color, roughness=0.72):
    """Create a simple Principled material, or reuse it on reruns."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = roughness
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.22
    return mat


def apply_mesh_setup(obj, mat, bevel=0.0):
    """Apply scale so the asset imports with predictable dimensions and UVs."""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if bevel > 0:
        modifier = obj.modifiers.new("Soft_edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return obj


def uv_sphere(name, location, scale, mat, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    result = apply_mesh_setup(obj, mat)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def ico_sphere(name, location, scale, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions,
        radius=1.0,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    result = apply_mesh_setup(obj, mat)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def cube(name, location, scale, mat, bevel=0.08):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return apply_mesh_setup(obj, mat, bevel=bevel)


def cone(name, location, radius1, radius2, depth, mat, vertices=8, rotation=None):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        location=location,
        rotation=rotation or (0.0, 0.0, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    return apply_mesh_setup(obj, mat, bevel=0.025)


def parent_to(obj, root):
    obj.parent = root
    return obj


def create_armature(root_collection, root):
    """Build a small deformation rig and give each primitive a rough bone weight."""
    armature_data = bpy.data.armatures.new("Dino_Rig")
    armature = bpy.data.objects.new("Dino_Rig", armature_data)
    root_collection.objects.link(armature)
    armature.parent = root
    armature.show_in_front = True
    armature_data.display_type = "BBONE"

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bone_specs = {
        "Root": ((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), None),
        "Body": ((0.0, -0.70, 0.90), (0.0, 0.75, 1.60), "Root"),
        "Head": ((0.0, 0.70, 1.55), (0.0, 1.75, 2.45), "Body"),
        "Eye_L": ((-0.54, 2.25, 2.35), (-0.54, 2.25, 2.85), "Head"),
        "Eye_R": ((0.54, 2.25, 2.35), (0.54, 2.25, 2.85), "Head"),
        "Front_Leg_L": ((-0.78, 0.62, 1.05), (-0.78, 0.62, 0.16), "Body"),
        "Front_Leg_R": ((0.78, 0.62, 1.05), (0.78, 0.62, 0.16), "Body"),
        "Back_Leg_L": ((-0.78, -0.70, 1.05), (-0.78, -0.70, 0.16), "Body"),
        "Back_Leg_R": ((0.78, -0.70, 1.05), (0.78, -0.70, 0.16), "Body"),
        "Tail": ((0.0, -0.70, 1.25), (0.0, -2.40, 2.05), "Body"),
    }
    edit_bones = {}
    for name, (head, tail, parent_name) in bone_specs.items():
        bone = armature_data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent_name:
            bone.parent = edit_bones[parent_name]
            bone.use_connect = False
        edit_bones[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")

    def bone_for_object(obj):
        name = obj.name
        if name in {"Dino_Root", "Dino_Rig"} or name.startswith(("Armor_", "Spot_")):
            return "Body"
        if name.startswith(("Big_Head", "Rounded_Snout", "Muzzle_", "Nostril_", "Smile_", "Mouth_", "Cheek_", "Neck", "Tongue")):
            return "Head"
        if name.startswith(("Eye_Main_L", "Eye_Iris_L", "Eye_Pupil_L", "Eye_Glint_L")):
            return "Eye_L"
        if name.startswith(("Eye_Main_R", "Eye_Iris_R", "Eye_Pupil_R", "Eye_Glint_R")):
            return "Eye_R"
        if name.startswith("Front_Leg_L") or name.startswith("Front_Foot_L"):
            return "Front_Leg_L"
        if name.startswith("Front_Leg_R") or name.startswith("Front_Foot_R"):
            return "Front_Leg_R"
        if name.startswith("Back_Leg_L") or name.startswith("Back_Foot_L"):
            return "Back_Leg_L"
        if name.startswith("Back_Leg_R") or name.startswith("Back_Foot_R"):
            return "Back_Leg_R"
        if name.startswith(("Tail_", "Short_Tail")):
            return "Tail"
        return "Body"

    for obj in root_collection.objects:
        if obj.type != "MESH":
            continue
        bone_name = bone_for_object(obj)
        group = obj.vertex_groups.new(name=bone_name)
        group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
        modifier = obj.modifiers.new("Dino_Rig_Weights", "ARMATURE")
        modifier.object = armature

    armature.select_set(False)
    return armature


def make_rig_action(armature, name, frames):
    """Create one self-contained pose Action on the armature."""
    action = bpy.data.actions.new(name)
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, pose in frames:
        bpy.context.scene.frame_set(frame)
        for bone_name, values in pose.items():
            pose_bone = armature.pose.bones.get(bone_name)
            if not pose_bone:
                continue
            location = values.get("location")
            rotation = values.get("rotation")
            scale = values.get("scale")
            if location is not None:
                pose_bone.location = location
                pose_bone.keyframe_insert(data_path="location", frame=frame, group=bone_name)
            if rotation is not None:
                pose_bone.rotation_mode = "XYZ"
                pose_bone.rotation_euler = rotation
                pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)
            if scale is not None:
                pose_bone.scale = scale
                pose_bone.keyframe_insert(data_path="scale", frame=frame, group=bone_name)
    action.use_fake_user = True
    armature.animation_data.action = None
    return action


def create_rig_actions(armature):
    """Create the soft desktop-pet action set on the reusable armature."""
    zero = (0.0, 0.0, 0.0)
    full = (1.0, 1.0, 1.0)
    sleepy = (1.0, 1.0, 0.04)
    idle = [
        (1, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}, "Eye_L": {"scale": full}, "Eye_R": {"scale": full}}),
        (18, {"Body": {"location": (0.0, 0.0, 0.055), "rotation": (0.0, 0.0, 0.018)}, "Head": {"rotation": (0.0, 0.018, -0.018)}, "Eye_L": {"scale": full}, "Eye_R": {"scale": full}}),
        (25, {"Body": {"location": (0.0, 0.0, 0.035), "rotation": (0.0, 0.0, -0.012)}, "Head": {"rotation": (0.0, -0.032, 0.030)}, "Eye_L": {"scale": sleepy}, "Eye_R": {"scale": sleepy}}),
        (29, {"Body": {"location": (0.0, 0.0, 0.055), "rotation": (0.0, 0.0, 0.018)}, "Head": {"rotation": (0.0, 0.018, -0.018)}, "Eye_L": {"scale": full}, "Eye_R": {"scale": full}}),
        (40, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}, "Eye_L": {"scale": full}, "Eye_R": {"scale": full}}),
    ]
    make_rig_action(armature, "Idle", idle)

    walk = []
    for frame, left_forward in ((1, True), (9, False), (17, True), (25, False), (33, True)):
        left_angle = math.radians(18 if left_forward else -18)
        right_angle = math.radians(-18 if left_forward else 18)
        walk.append((frame, {
            "Body": {"location": (0.0, 0.0, 0.05 if frame in (9, 25) else 0.0), "rotation": (0.0, 0.0, 0.0)},
            "Head": {"rotation": (0.0, 0.015 if left_forward else -0.015, 0.0)},
            "Front_Leg_L": {"rotation": (left_angle, 0.0, 0.0)},
            "Back_Leg_L": {"rotation": (left_angle, 0.0, 0.0)},
            "Front_Leg_R": {"rotation": (right_angle, 0.0, 0.0)},
            "Back_Leg_R": {"rotation": (right_angle, 0.0, 0.0)},
            "Tail": {"rotation": (0.0, 0.0, math.radians(8 if left_forward else -8))},
        }))
    make_rig_action(armature, "Walk", walk)

    dig = []
    for frame, scrape in ((1, -18), (10, 24), (20, -18), (30, 24), (40, -18)):
        dig.append((frame, {
            "Body": {"location": (0.0, 0.08 if scrape > 0 else 0.0, 0.02), "rotation": (math.radians(-5), 0.0, 0.0)},
            "Head": {"location": (0.0, 0.06, -0.10), "rotation": (math.radians(10), 0.0, 0.0)},
            "Front_Leg_L": {"rotation": (math.radians(scrape), 0.0, 0.0)},
            "Front_Leg_R": {"rotation": (math.radians(scrape), 0.0, 0.0)},
            "Tail": {"rotation": (0.0, 0.0, math.radians(-5 if scrape > 0 else 5))},
        }))
    make_rig_action(armature, "Dig", dig)

    hop = [
        (1, {"Body": {"location": zero, "rotation": zero, "scale": (1.0, 1.0, 0.92)}, "Head": {"rotation": (math.radians(4), 0.0, 0.0)}}),
        (8, {"Body": {"location": (0.0, 0.0, 0.58), "rotation": zero, "scale": (1.0, 1.0, 1.04)}, "Head": {"rotation": (math.radians(-3), 0.0, 0.0)}}),
        (16, {"Body": {"location": (0.0, 0.0, 0.62), "rotation": zero, "scale": full}, "Head": {"rotation": zero}}),
        (25, {"Body": {"location": (0.0, 0.0, 0.08), "rotation": zero, "scale": (1.0, 1.0, 0.94)}, "Head": {"rotation": (math.radians(3), 0.0, 0.0)}}),
        (32, {"Body": {"location": zero, "rotation": zero, "scale": full}, "Head": {"rotation": zero}}),
    ]
    make_rig_action(armature, "Hop", hop)

    happy = [
        (1, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}, "Tail": {"rotation": zero}}),
        (8, {"Body": {"location": (0.0, 0.0, 0.18), "rotation": (0.0, 0.0, math.radians(-4))}, "Head": {"rotation": (math.radians(-7), 0.0, math.radians(-4))}, "Tail": {"rotation": (0.0, 0.0, math.radians(10))}}),
        (16, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}, "Tail": {"rotation": zero}}),
        (24, {"Body": {"location": (0.0, 0.0, 0.18), "rotation": (0.0, 0.0, math.radians(4))}, "Head": {"rotation": (math.radians(-7), 0.0, math.radians(4))}, "Tail": {"rotation": (0.0, 0.0, math.radians(-10))}}),
        (32, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}, "Tail": {"rotation": zero}}),
    ]
    make_rig_action(armature, "Happy", happy)

    sleep_left = [
        (1, {"Body": {"location": (0.0, 0.0, 0.20), "rotation": (0.0, 0.0, math.radians(-62))}, "Head": {"location": (0.18, 0.0, -0.15), "rotation": (0.0, 0.0, math.radians(-10))}, "Front_Leg_L": {"location": (0.0, 0.0, -0.10), "rotation": (math.radians(22), 0.0, 0.0)}, "Front_Leg_R": {"location": (0.0, 0.0, -0.10), "rotation": (math.radians(22), 0.0, 0.0)}, "Back_Leg_L": {"location": (0.0, 0.0, -0.08), "rotation": (math.radians(18), 0.0, 0.0)}, "Back_Leg_R": {"location": (0.0, 0.0, -0.08), "rotation": (math.radians(18), 0.0, 0.0)}, "Tail": {"rotation": (0.0, 0.0, math.radians(-8))}, "Eye_L": {"scale": sleepy}, "Eye_R": {"scale": sleepy}}),
        (20, {"Body": {"location": (0.0, 0.0, 0.24), "rotation": (0.0, 0.0, math.radians(-62))}, "Head": {"location": (0.18, 0.0, -0.15), "rotation": (0.0, 0.0, math.radians(-10))}, "Eye_L": {"scale": (1.0, 1.0, 0.06)}, "Eye_R": {"scale": (1.0, 1.0, 0.06)}}),
        (40, {"Body": {"location": (0.0, 0.0, 0.20), "rotation": (0.0, 0.0, math.radians(-62))}, "Head": {"location": (0.18, 0.0, -0.15), "rotation": (0.0, 0.0, math.radians(-10))}, "Eye_L": {"scale": sleepy}, "Eye_R": {"scale": sleepy}}),
    ]
    make_rig_action(armature, "SleepLeft", sleep_left)

    sleep_right = [
        (1, {"Body": {"location": (0.0, 0.0, 0.20), "rotation": (0.0, 0.0, math.radians(62))}, "Head": {"location": (-0.18, 0.0, -0.15), "rotation": (0.0, 0.0, math.radians(10))}, "Front_Leg_L": {"location": (0.0, 0.0, -0.10), "rotation": (math.radians(22), 0.0, 0.0)}, "Front_Leg_R": {"location": (0.0, 0.0, -0.10), "rotation": (math.radians(22), 0.0, 0.0)}, "Back_Leg_L": {"location": (0.0, 0.0, -0.08), "rotation": (math.radians(18), 0.0, 0.0)}, "Back_Leg_R": {"location": (0.0, 0.0, -0.08), "rotation": (math.radians(18), 0.0, 0.0)}, "Tail": {"rotation": (0.0, 0.0, math.radians(8))}, "Eye_L": {"scale": sleepy}, "Eye_R": {"scale": sleepy}}),
        (20, {"Body": {"location": (0.0, 0.0, 0.24), "rotation": (0.0, 0.0, math.radians(62))}, "Head": {"location": (-0.18, 0.0, -0.15), "rotation": (0.0, 0.0, math.radians(10))}, "Eye_L": {"scale": (1.0, 1.0, 0.06)}, "Eye_R": {"scale": (1.0, 1.0, 0.06)}}),
        (40, {"Body": {"location": (0.0, 0.0, 0.20), "rotation": (0.0, 0.0, math.radians(62))}, "Head": {"location": (-0.18, 0.0, -0.15), "rotation": (0.0, 0.0, math.radians(10))}, "Eye_L": {"scale": sleepy}, "Eye_R": {"scale": sleepy}}),
    ]
    make_rig_action(armature, "SleepRight", sleep_right)

    curious = [
        (1, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}}),
        (12, {"Body": {"location": (0.0, 0.0, 0.04), "rotation": zero}, "Head": {"location": (0.0, 0.04, 0.04), "rotation": (math.radians(-4), math.radians(-14), math.radians(-10))}}),
        (24, {"Body": {"location": (0.0, 0.0, 0.04), "rotation": zero}, "Head": {"location": (0.0, 0.04, 0.04), "rotation": (math.radians(-4), math.radians(14), math.radians(10))}}),
        (36, {"Body": {"location": zero, "rotation": zero}, "Head": {"rotation": zero}}),
    ]
    make_rig_action(armature, "Curious", curious)
    armature.animation_data_create()
    armature.animation_data.action = bpy.data.actions.get("Idle")


def setup_render_camera():
    """Create a temporary camera for transparent sprite renders."""
    bpy.ops.object.camera_add(location=(4.8, 7.2, 3.1))
    camera = bpy.context.object
    camera.name = "Dino_Render_Camera"
    direction = Vector((0.0, 0.25, 1.25)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 58
    bpy.context.scene.camera = camera
    return camera


def render_sprite_sequence(armature):
    """Render all approved actions into the existing Dino sprite folders."""
    sequence_specs = {
        "Idle": ("idle", range(1, 41, 4)),
        "Walk": ("walk", range(1, 34, 4)),
        "Dig": ("dig", range(1, 41, 4)),
        "Hop": ("hop", range(1, 33, 4)),
        "Happy": ("happy", range(1, 33, 4)),
        "SleepLeft": ("sleep_left", range(1, 41, 4)),
        "SleepRight": ("sleep_right", range(1, 41, 4)),
        "Curious": ("curious", range(1, 37, 4)),
    }
    camera = setup_render_camera()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.8
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.film_transparent = True
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "rim.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.film_transparent = True
    for action_name, (folder_name, frames) in sequence_specs.items():
        action = bpy.data.actions.get(action_name)
        if not action:
            continue
        output_dir = SPRITE_DIR / folder_name
        output_dir.mkdir(parents=True, exist_ok=True)
        armature.animation_data.action = action
        for index, frame in enumerate(frames, 1):
            scene.frame_set(frame)
            scene.render.filepath = str(output_dir / f"{folder_name}_{index:03d}.png")
            bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def render_previews(armature):
    """Render only review images in the model's own renders folder."""
    preview_dir = PROJECT_DINO_DIR / "renders"
    preview_dir.mkdir(parents=True, exist_ok=True)
    camera = setup_render_camera()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.8
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.film_transparent = True
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "rim.sl"
    scene.display.shading.color_type = "MATERIAL"
    target = Vector((0.0, 0.10, 1.35))
    views = {
        "preview_front_angle.png": ((4.8, 7.8, 3.2), "Idle", 20),
        "preview_side.png": ((7.5, 0.2, 2.8), "Idle", 20),
        "preview_idle.png": ((4.8, 7.8, 3.2), "Idle", 25),
        "preview_sleep_left.png": ((4.8, 7.8, 3.2), "SleepLeft", 20),
        "preview_sleep_right.png": ((4.8, 7.8, 3.2), "SleepRight", 20),
        "preview_curious.png": ((4.8, 7.8, 3.2), "Curious", 12),
    }
    for filename, (location, action_name, frame) in views.items():
        camera.location = location
        view_target = target.copy()
        camera.data.ortho_scale = 7.0 if "sleep_" in filename else 4.8
        camera.rotation_euler = (view_target - camera.location).to_track_quat("-Z", "Y").to_euler()
        action = bpy.data.actions.get(action_name)
        if action:
            armature.animation_data.action = action
        scene.frame_set(frame)
        scene.render.filepath = str(preview_dir / filename)
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def create_dino():
    # Clear the default scene so the generated asset is the only selected content.
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)

    root_collection = bpy.data.collections.new(MODEL_COLLECTION_NAME)
    bpy.context.scene.collection.children.link(root_collection)

    # The root is at ground contact and makes moving the whole character easy.
    root = bpy.data.objects.new("Dino_Root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.35
    root.location = (0.0, 0.0, 0.0)
    root_collection.objects.link(root)

    mats = {
        name: material(name.title().replace("_", "_"), color)
        for name, color in COLORS.items()
    }

    def add(obj):
        # Operators create objects in the active collection; move them to our asset collection.
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        root_collection.objects.link(obj)
        return parent_to(obj, root)

    # Coordinate convention: head is +Y, feet touch Z=0.
    body = add(uv_sphere("Body", (0.0, 0.0, 1.20), (1.30, 1.38, 0.98), mats["body"], 20, 14))
    belly = add(uv_sphere("Light_Belly", (0.0, 0.82, 0.98), (1.00, 0.58, 0.66), mats["belly"], 18, 12))
    neck = add(uv_sphere("Neck", (0.0, 0.88, 1.62), (0.90, 0.78, 0.80), mats["body_light"], 18, 12))
    head = add(uv_sphere("Big_Head", (0.0, 1.40, 2.30), (1.28, 1.02, 1.20), mats["body"], 20, 14))
    muzzle = add(uv_sphere("Muzzle_Cream", (0.0, 2.15, 2.02), (0.88, 0.48, 0.44), mats["belly"], 18, 12))
    add(uv_sphere("Cheek_L", (-0.78, 2.28, 1.96), (0.20, 0.10, 0.15), mats["cheek"], 12, 8))
    add(uv_sphere("Cheek_R", (0.78, 2.28, 1.96), (0.20, 0.10, 0.15), mats["cheek"], 12, 8))
    mouth = add(uv_sphere("Mouth_Open", (0.0, 2.62, 1.84), (0.62, 0.10, 0.30), mats["mouth"], 16, 8))
    tongue = add(uv_sphere("Tongue", (0.0, 2.72, 1.76), (0.28, 0.07, 0.16), mats["tongue"], 14, 8))

    # Short, soft legs and oversized feet give the reference's mascot silhouette.
    for side, x in (("L", -0.78), ("R", 0.78)):
        for leg_name, y in (("Front", 0.62), ("Back", -0.70)):
            leg = add(uv_sphere(f"{leg_name}_Leg_{side}", (x, y, 0.62), (0.45, 0.48, 0.58), mats["body"], 14, 10))
            foot = add(uv_sphere(f"{leg_name}_Foot_{side}", (x, y + 0.12, 0.27), (0.56, 0.64, 0.28), mats["body_light"], 16, 10))
            for claw_index, claw_x in enumerate((-0.18, 0.0, 0.18), 1):
                claw = add(uv_sphere(f"{leg_name}_Claw_{side}_{claw_index}", (x + claw_x, y + 0.57, 0.20), (0.085, 0.15, 0.07), mats["claw"], 10, 6))

    # A rising segmented tail ends in a large, rounded club rather than a weapon-like spike.
    add(uv_sphere("Tail_Base", (0.0, -1.12, 1.28), (0.58, 0.72, 0.48), mats["body"], 16, 10))
    add(uv_sphere("Tail_Mid", (0.0, -1.68, 1.54), (0.48, 0.64, 0.42), mats["body"], 16, 10))
    add(uv_sphere("Tail_Tip", (0.0, -2.08, 1.86), (0.38, 0.54, 0.36), mats["body_light"], 14, 9))
    add(ico_sphere("Tail_Club", (0.0, -2.35, 2.10), (0.62, 0.58, 0.52), mats["shell"], 2))

    # Soft cream armor beads read as Ankylosaurus plates without sharp insect-like spikes.
    dorsal_specs = [(0.92, 2.02, 0.30, 0.22, 0.38), (0.50, 2.18, 0.34, 0.24, 0.42), (0.05, 2.12, 0.38, 0.27, 0.46), (-0.42, 2.00, 0.34, 0.25, 0.40), (-0.84, 1.80, 0.28, 0.22, 0.34), (-1.20, 1.62, 0.22, 0.18, 0.28)]
    for index, (y, z, x_scale, y_scale, z_scale) in enumerate(dorsal_specs, 1):
        add(ico_sphere(f"Armor_Dorsal_{index}", (0.0, y, z), (x_scale, y_scale, z_scale), mats["shell_light"], 2))
    for side, x in (("L", -1.06), ("R", 1.06)):
        for index, (y, z, scale) in enumerate(((0.72, 1.65, 0.32), (0.12, 1.72, 0.30), (-0.48, 1.62, 0.25)), 1):
            add(ico_sphere(f"Armor_Side_{side}_{index}", (x, y, z), (scale, 0.20, scale * 0.85), mats["shell_light"], 2))

    # Very large dark eyes with oversized white highlights establish the friendly face.
    for side, x in (("L", -0.54), ("R", 0.54)):
        add(uv_sphere(f"Eye_Main_{side}", (x, 2.26, 2.56), (0.38, 0.16, 0.46), mats["eye"], 18, 12))
        add(uv_sphere(f"Eye_Iris_{side}", (x, 2.40, 2.55), (0.25, 0.07, 0.32), mats["eye_iris"], 16, 10))
        add(uv_sphere(f"Eye_Pupil_{side}", (x, 2.46, 2.55), (0.12, 0.035, 0.17), mats["eye"], 12, 8))
        add(uv_sphere(f"Eye_Glint_{side}", (x - 0.10, 2.50, 2.73), (0.105, 0.035, 0.13), mats["eye_glint"], 12, 8))
        add(uv_sphere(f"Eye_Glint_Small_{side}", (x + 0.10, 2.41, 2.42), (0.045, 0.025, 0.055), mats["eye_glint"], 8, 6))
    for side, x in (("L", -0.29), ("R", 0.29)):
        add(uv_sphere(f"Nostril_{side}", (x, 2.67, 2.10), (0.065, 0.035, 0.045), mats["mouth"], 10, 6))

    # A few embedded darker turquoise spots add warmth and surface detail.
    for index, (x, y, z, scale) in enumerate(((-0.92, 1.78, 2.34, 0.11), (0.94, 1.78, 2.32, 0.10), (-1.08, 0.72, 1.30, 0.12), (1.08, 0.35, 1.34, 0.10), (-0.88, 0.10, 1.76, 0.09), (0.88, -0.35, 1.72, 0.10)), 1):
        add(ico_sphere(f"Spot_{index}", (x, y, z), (scale, scale * 0.35, scale), mats["spot"], 1))

    # Add the rig after all meshes exist so every part receives a deterministic rough weight.
    armature = create_armature(root_collection, root)
    create_rig_actions(armature)

    # Select the complete asset, with the root active for convenient transforms.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in root_collection.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    # Save the complete editable source scene before exporting derived files.
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    if RENDER_PREVIEWS:
        render_previews(armature)

    if RENDER_SPRITES:
        render_sprite_sequence(armature)

    # Optional one-file export for a game pipeline.
    if EXPORT_GLB:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in root_collection.objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = armature
        EXPORT_DIR.mkdir(parents=True, exist_ok=True)
        output_path = EXPORT_DIR / "Dino.glb"
        bpy.ops.export_scene.gltf(
            filepath=str(output_path),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_animations=True,
            export_skins=True,
        )
        print(f"Exported GLB: {output_path}")

    print("Created cute Ankylosaurus prototype with simple UV spheres and materials.")
    return root


if __name__ == "__main__":
    create_dino()
