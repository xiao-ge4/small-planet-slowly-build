"""Blender: build reusable, deterministic botanical meshes, not billboard cutouts."""
import bpy
import math
import random
import json
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/garden-detail-kit/v1'
OUT.mkdir(parents=True, exist_ok=True)
random.seed(4617)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = .86
    return m

greens = [mat('Leaf '+str(i), c) for i,c in enumerate([
    (.07,.18,.018),(.13,.28,.033),(.22,.37,.060),(.29,.43,.08),
    (.35,.49,.11),(.17,.32,.047),(.40,.53,.14)])]
petals = [mat('Cream petals',(.98,.88,.64)),mat('Ivory petals',(.98,.97,.84)),
          mat('Apricot petals',(.94,.57,.40)),mat('Pink petals',(.93,.55,.61)),
          mat('Lilac petals',(.58,.52,.79))]
gold = mat('Pollen',(.92,.59,.14))
stems = mat('Stems',(.24,.37,.065))
stones = [mat('Weathered stone '+str(i), c) for i,c in enumerate([
    (.48,.44,.34),(.57,.54,.43),(.65,.61,.49),(.71,.68,.56),(.52,.53,.39)])]
parts=[]

def mesh(name, verts, faces, materials, face_mats=None, smooth=True):
    me=bpy.data.meshes.new(name)
    me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    for m in materials:me.materials.append(m)
    for p in me.polygons:
        p.use_smooth=smooth
        if face_mats:p.material_index=face_mats[p.index]
    parts.append(ob)
    return ob

def leaf(base, direction, length, width, material, name='Curved broad leaf'):
    base=Vector(base);f=Vector(direction).normalized()
    side=f.cross(Vector((0,0,1)))
    if side.length<.01:side=Vector((1,0,0))
    side.normalize();up=side.cross(f).normalized()
    verts=[];n=12;m=8
    # A rounded, closed lamina has volume, softly lit edges and valid normals on
    # both sides. Slightly blunt end caps avoid a paper-cutout silhouette.
    for k in range(n+1):
        t=.015+.97*k/n
        center=base+f*(length*t)+Vector((0,0,math.sin(t*math.pi)*length*.23))
        w=math.sin(t*math.pi)**.50*width*.5;thick=width*.14*math.sin(t*math.pi)
        for j in range(m):
            a=j*math.tau/m;verts.append(tuple(center+side*(math.cos(a)*w)+up*(math.sin(a)*thick)))
    faces=[]
    for k in range(n):
        for j in range(m):
            a=k*m+j;b=k*m+(j+1)%m;faces.append((a,a+m,b+m,b))
    faces.extend([tuple(range(m)),tuple(n*m+j for j in reversed(range(m)))])
    return mesh(name,verts,faces,[material])

def sphere(name,p,scale,material,segments=10,rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=p)
    ob=bpy.context.object;ob.name=name;ob.scale=scale;ob.data.materials.append(material)
    for f in ob.data.polygons:f.use_smooth=True
    parts.append(ob);return ob

def flower(x,y,z,r,color):
    sphere('Golden flower heart',(x,y,z),(r*.25,r*.25,r*.13),gold)
    for k in range(6):
        a=k*math.tau/6
        p=(x+math.cos(a)*r*.49,y+math.sin(a)*r*.49,z-.012)
        ob=sphere('Rounded flower petal',p,(r*.56,r*.29,r*.10),color)
        ob.rotation_euler.z=a
    leaf((x,y,z-.12),(.6,.2,.3),.15,.055,greens[2])

def bush(center=(0,0,0),radius=.6,height=.5,leaves=150):
    cx,cy,cz=center
    for i in range(leaves):
        a=random.random()*math.tau;r=radius*math.sqrt(random.random())
        h=height*(.15+.8*max(0,1-(r/radius)**2))
        base=(cx+math.cos(a)*r,cy+math.sin(a)*r,cz+h*random.uniform(.72,1))
        direction=(math.cos(a)*.7,math.sin(a)*.7,random.uniform(-.1,.65))
        leaf(base,direction,random.uniform(.11,.23),random.uniform(.065,.13),random.choice(greens))

def rock(center,scale,index):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=center)
    ob=bpy.context.object;ob.name='Rounded fractured fieldstone';ob.scale=scale
    for v in ob.data.vertices:
        n=v.co.normalized();f=1+.10*math.sin(n.x*7+index)*math.sin(n.y*9-index)+random.uniform(-.035,.035)
        v.co*=f
    for m in stones:ob.data.materials.append(m)
    for face in ob.data.polygons:
        face.material_index=random.choices(range(len(stones)),[1,4,7,3,1])[0];face.use_smooth=False
    bevel=ob.modifiers.new('Soft weathered edges','BEVEL');bevel.width=.045;bevel.segments=2
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=bevel.name)
    parts.append(ob)

def finish(aid,name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.convert(target='MESH');bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    # Bottom-centered pivot is a library contract, not an editor-only adjustment.
    world=[ob.matrix_world@v.co for v in ob.data.vertices]
    mn=Vector(tuple(min(p[i] for p in world) for i in range(3)))
    mx=Vector(tuple(max(p[i] for p in world) for i in range(3)))
    offset=Vector(((mn.x+mx.x)/2,(mn.y+mx.y)/2,mn.z))
    for v in ob.data.vertices:v.co=ob.matrix_world@v.co-offset
    ob.matrix_world.identity()
    folder=OUT/aid;folder.mkdir(exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(folder/'lod0.glb'),export_format='GLB',use_selection=True)
    count=sum(len(p.vertices)-2 for p in ob.data.polygons)
    size=mx-mn
    result={'id':aid,'name':name,'size':{'x':size.x,'y':size.z,'z':size.y},'center':{'x':0,'y':size.z/2,'z':0},'triangles':count,'model':'lod0.glb','preview':'preview.png'}
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=20
    scene.render.resolution_x=640;scene.render.resolution_y=480;scene.render.resolution_percentage=100
    scene.world.color=(.28,.28,.28)
    camera=bpy.data.objects.new('Preview camera',bpy.data.cameras.new('Preview camera'))
    bpy.context.collection.objects.link(camera);scene.camera=camera
    radius=max(size.x,size.y)*1.35
    camera.location=(radius,-radius,radius*.85)
    camera.rotation_euler=(Vector((0,0,size.z*.42))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=max(size.x,size.y)*1.4
    lights=[]
    for loc,power,area in [((-.9,-1.8,3),210,3),((2,1,2),110,3)]:
        data=bpy.data.lights.new('Soft daylight','AREA');data.energy=power;data.shape='DISK';data.size=area
        light=bpy.data.objects.new('Soft daylight',data);bpy.context.collection.objects.link(light);light.location=loc;light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler();lights.append(light)
    scene.view_settings.view_transform='AgX';scene.render.filepath=str(folder/'preview.png');bpy.ops.render.render(write_still=True)
    (folder/'asset.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    for temp in [ob,camera,*lights]:bpy.data.objects.remove(temp,do_unlink=True)
    parts.clear();return result

assets=[]
bush(radius=.62,height=.48,leaves=250)
assets.append(finish('garden-understory-cluster','晨露阔叶灌木丛'))
bush(radius=.57,height=.22,leaves=135)
for i in range(23):
    a=random.random()*math.tau;r=random.uniform(.08,.52)
    flower(math.cos(a)*r,math.sin(a)*r,random.uniform(.34,.49),random.uniform(.085,.12),random.choices(petals,[2,3,3,4,1])[0])
assets.append(finish('garden-flower-cluster','晨露混色花丛'))
for i,(x,y,s) in enumerate([(0,0,.46),(.49,.13,.24),(-.42,-.17,.21),(.1,-.45,.15)]):
    rock((x,y,s*.40),(s,s*.75,s*.57),i)
for i in range(36):
    a=random.random()*math.tau;r=random.uniform(.28,.68)
    leaf((math.cos(a)*r,math.sin(a)*r,.025),(math.cos(a),math.sin(a),.4),random.uniform(.1,.20),.06,random.choice(greens))
assets.append(finish('garden-rock-cluster','苔石与地被组合'))
for i in range(135):
    a=random.random()*math.tau;r=.42*math.sqrt(random.random())
    leaf((math.cos(a)*r,math.sin(a)*r,.005),(math.cos(a),math.sin(a),2),random.uniform(.07,.19),random.uniform(.018,.035),random.choice(greens))
assets.append(finish('garden-grass-tuft','细叶草簇'))
(OUT/'manifest.json').write_text(json.dumps({'schemaVersion':1,'seed':4617,'assets':assets},ensure_ascii=False,indent=2))
print('Garden detail kit completed:',len(assets),'reusable meshes')
