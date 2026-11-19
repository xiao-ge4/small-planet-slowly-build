"""Normalize one generated GLB and create conservative game LODs in Blender."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


def arguments():
    values = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--height", type=float, required=True)
    parser.add_argument("--result", type=Path, required=True)
    return parser.parse_args(values)


def mesh_objects():
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    if not points:
        raise ValueError("Imported GLB contains no mesh geometry")
    return Vector(tuple(min(p[i] for p in points) for i in range(3))), Vector(tuple(max(p[i] for p in points) for i in range(3)))


def stats():
    objects = mesh_objects()
    triangles = 0
    materials = set()
    textures = set()
    for obj in objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        materials.update(slot.material.name for slot in obj.material_slots if slot.material)
        for slot in obj.material_slots:
            mat = slot.material
            if mat and mat.use_nodes:
                for node in mat.node_tree.nodes:
                    if node.type == "TEX_IMAGE" and node.image:
                        textures.add(node.image.name)
    lo, hi = bounds(objects)
    return {
        "triangles": triangles,
        "meshCount": len(objects),
        "materialCount": len(materials),
        "textureCount": len(textures),
        "boundsMin": list(lo),
        "boundsMax": list(hi),
        "dimensions": list(hi - lo),
    }


def export(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(path), export_format="GLB", export_apply=True,
        export_cameras=False, export_lights=False, export_animations=False,
        export_materials="EXPORT", export_image_format="AUTO",
    )


def main():
    args = arguments()
    if not math.isfinite(args.height) or not 0.05 <= args.height <= 5:
        raise ValueError("Height must be between 0.05 and 5 metres")
    args.out.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    bpy.ops.import_scene.gltf(filepath=str(args.source))

    objects = mesh_objects()
    if not objects:
        raise ValueError("Imported GLB contains no mesh geometry")
    # Bake all imported node transforms into vertices before normalizing. glTF
    # importer converts glTF Y-up into Blender Z-up; exporting reverses that.
    for obj in objects:
        world = obj.matrix_world.copy()
        obj.parent = None
        obj.data.transform(world)
        obj.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()
    lo, hi = bounds(objects)
    original_dimensions = hi - lo
    if original_dimensions.z <= 1e-6:
        raise ValueError("Could not identify a positive model height")
    scale = args.height / original_dimensions.z
    center_x, center_y, base_z = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z
    for obj in objects:
        for vertex in obj.data.vertices:
            co = vertex.co
            vertex.co = ((co.x - center_x) * scale, (co.y - center_y) * scale, (co.z - base_z) * scale)
        obj.data.update()
        obj.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()
    normalized = stats()
    if abs(normalized["dimensions"][2] - args.height) > 0.001:
        raise ValueError("Normalized height is outside tolerance")
    if any(abs(normalized["boundsMin"][i] + normalized["boundsMax"][i]) > 0.001 for i in (0, 1)) or abs(normalized["boundsMin"][2]) > 0.001:
        raise ValueError("Normalized pivot is not centered on the base")

    # Render a neutral, low-contrast preview for human review. No material edits.
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE" if bpy.app.version >= (5, 0, 0) else "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(args.out / "preview.png")
    scene.world.color = (0.48, 0.48, 0.48)
    center = Vector((0, 0, args.height * 0.5))
    extent = max(normalized["dimensions"])
    bpy.ops.object.camera_add(location=center + Vector((1.7, -2.5, 1.1)) * extent)
    camera = bpy.context.object
    camera.name = "Asset review camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = extent * 1.75
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    for name, offset, energy, size in [
        ("Key", (1.3, -1.8, 2.6), 360, 2.2),
        ("Fill", (-1.6, -0.4, 1.5), 170, 2.8),
        ("Rim", (0.3, 1.8, 2.2), 220, 2.0),
    ]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset) * extent)
        light = bpy.context.object
        light.name = "Review " + name
        light.data.energy = energy * extent * extent
        light.data.shape = "DISK"
        light.data.size = size * extent
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.render.render(write_still=True)
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()

    lods = []
    for level, ratio in enumerate((1.0, 0.5, 0.2)):
        # Re-import the exact normalized mesh for each tier so decimation never
        # compounds from one LOD to the next and the source remains untouched.
        if level:
            # Restore geometry from the previously exported normalized LOD0.
            bpy.ops.object.select_all(action="SELECT")
            bpy.ops.object.delete(use_global=False)
            bpy.ops.import_scene.gltf(filepath=str(args.out / "lod0.glb"))
            objects = mesh_objects()
        else:
            objects = mesh_objects()
        if ratio < 1:
            for obj in objects:
                if len(obj.data.polygons) < 4:
                    continue
                bpy.context.view_layer.objects.active = obj
                obj.select_set(True)
                modifier = obj.modifiers.new("Distant LOD simplification", "DECIMATE")
                modifier.ratio = ratio
                modifier.use_collapse_triangulate = True
                bpy.ops.object.modifier_apply(modifier=modifier.name)
                obj.select_set(False)
        path = args.out / ("lod%d.glb" % level)
        export(path)
        tier = stats()
        tier.update(level=level, ratio=ratio, file=path.name)
        lods.append(tier)

    result = {
        "success": True,
        "normalization": {
            "scale": scale,
            "upAxis": "Y",
            "heightMetres": args.height,
            "pivot": [0, 0, 0],
            "originalDimensionsBlenderZUp": list(original_dimensions),
            "dimensionsMetres": [normalized["dimensions"][0], normalized["dimensions"][2], normalized["dimensions"][1]],
        },
        "source": normalized,
        "lods": lods,
        "collider": {
            "recipe": "box",
            "center": [0, args.height / 2, 0],
            "size": [normalized["dimensions"][0] + 0.02, args.height, normalized["dimensions"][1] + 0.02],
            "trigger": False,
        },
        "sockets": [],
        "preview": "preview.png",
        "notes": [
            "Original GLB retained unchanged outside prepared outputs.",
            "LOD0 preserves full mesh; LOD1/2 use independent 50%/20% decimation and retain UV/material data.",
            "No interaction socket is needed for this static decorative bench.",
            "LOD settings are an initial desktop-demo profile; device performance still requires profiling.",
        ],
    }
    args.result.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__":
    main()
