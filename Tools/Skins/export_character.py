"""Export approved review geometry to the existing Unity mesh/bind-pose contract.
Does not alter the review meshes. Corner normals and module-border weights preserved.
"""
import bpy,json,math
from pathlib import Path
OUT=Path(__file__).resolve().parents[2]/'SourceAssets/Skins/Export';OUT.mkdir(exist_ok=True)
palette={}
def srgb(x):return 12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055
mapping={'Landmark Skin':'Skin01_Body','Landmark White':'Skin01_EyeWhite','Landmark Iris':'Skin01_Iris','Landmark Dark':'Skin01_Dark','Landmark Lips':'Skin01_Lips','Hair_Chestnut':'Skin01_Hair'}
def material(m):
    name=next((v for k,v in mapping.items() if m.name.startswith(k)),m.name.split('.')[0])
    palette[name]=[srgb(c) for c in m.diffuse_color[:3]]
    return name
def weights(o,v,region):
    if region==0:
        f=max(0,min(1,(v.co.z-.775)/.045))
        return {k:w for k,w in [('mixamorig:Neck',1-f),('mixamorig:Head',f)] if w>1e-8}
    if region==-1:return {'mixamorig:Head':1.}
    if region==1 and abs(v.co.z-.775)<1e-6:return {'mixamorig:Neck':1.}
    if region in (1,2) and abs(v.co.z-.47)<1e-6:return {'mixamorig:Hips':1.}
    ws=sorted([(o.vertex_groups[g.group].name,g.weight) for g in v.groups if g.weight>1e-8],key=lambda p:-p[1])[:4]
    assert ws, (o.name,v.index)
    total=sum(w for k,w in ws);return {k:w/total for k,w in ws}
def export(name,parts):
    vertices=[];triangles=[];mats=[]
    for oname,region in parts:
        o=bpy.data.objects[oname];m=o.data;m.calc_loop_triangles();lookup={}
        slots=[material(mat) for mat in m.materials]
        for mat in slots:
            if mat not in mats:mats.append(mat)
        for tri in m.loop_triangles:
            ids=[]
            # These flat face inlays were authored facing inward; Workbench
            # showed both sides, while the game correctly culls backfaces.
            flip=slots[tri.material_index] in ('Skin01_Iris','Skin01_Lips') and tri.normal.y>0
            for vi,li in zip(tri.vertices,tri.loops):
                v=m.vertices[vi];n=m.corner_normals[li].vector
                if flip:n=-n
                key=(vi,tuple(round(x,6) for x in n))
                if key not in lookup:
                    ws=weights(o,v,region);lookup[key]=len(vertices)
                    vertices.append({'position':list(v.co),'normal':list(n),'uv':[v.co.x,v.co.z],'region':max(0,region),'bones':list(ws),'weights':list(ws.values())})
                ids.append(lookup[key])
            triangles.append({'indices':list(reversed(ids)) if flip else ids,'material':mats.index(slots[tri.material_index])})
    data={'name':name,'vertices':vertices,'triangles':triangles,'materials':mats}
    OUT.joinpath(name+'.json').write_text(json.dumps(data,separators=(',',':')))
    return data
parts=[('Skin01_Head',0),('Skin01_Torso',1),('Skin01_Legs',2),('Skin01_Feet',3)]
body=export('Skin01_Body',parts)
head=export('HeadClean',[parts[0]])
hair=export('Hair',[('Skin01_Hair',-1)])
OUT.joinpath('palette.json').write_text(json.dumps(palette,indent=2))
report={name:len(data['triangles']) for name,data in [('body',body),('head',head),('hair',hair)]}
# All duplicate positions on module seams must deform identically.
for z,label in [(.775,'neck'),(.47,'waist'),(.09,'ankle')]:
    seam={}
    for v in body['vertices']:
        if abs(v['position'][2]-z)>1e-6:continue
        key=tuple(round(x,6) for x in v['position'])
        ws={k:round(w,6) for k,w in zip(v['bones'],v['weights'])}
        if key in seam:assert seam[key]==ws,(label,key,seam[key],ws)
        seam[key]=ws
    report[label+'SharedPositions']=len(seam)
OUT.joinpath('export-report.json').write_text(json.dumps(report,indent=2));print(report)

