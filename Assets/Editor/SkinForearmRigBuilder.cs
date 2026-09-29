using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SkinForearmRigBuilder
{
    const string Root="Assets/Models/Skins/Skin01/";
    [MenuItem("ClimbUp/Skins/Install Forearm Twist Sections")]
    public static void Install()
    {
        var instance=PrefabUtility.LoadPrefabContents(Root+"Skin01.prefab");
        try
        {
            var body=instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var bones=body.bones.ToList();
            var originalBind=body.sharedMesh.bindposes;
            var binds=originalBind.ToList();
            foreach(string side in new[]{"Left","Right"})
            {
                var lower=bones.First(b=>b.name=="mixamorig:"+side+"ForeArm");
                int index=bones.IndexOf(lower);
                var rig=lower.GetComponent<ForearmTwistRig>();
                if(rig==null) rig=lower.gameObject.AddComponent<ForearmTwistRig>();
                for(int section=0;section<3;section++)
                {
                    string name=side+"ForearmTwist"+section;
                    var t=lower.Find(name);
                    if(t==null) {t=new GameObject(name).transform;t.SetParent(lower,false);}
                    rig.sections[section]=t;
                    if(!bones.Contains(t)) {bones.Add(t);binds.Add(originalBind[index]);}
                }
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{Root+"Meshes"}))
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                mesh.bindposes=binds.ToArray();Distribute(mesh,bones.ToArray());EditorUtility.SetDirty(mesh);
            }
            foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.bones=bones.ToArray();
            PrefabUtility.SaveAsPrefabAsset(instance,Root+"Skin01.prefab");
            AssetDatabase.SaveAssets();
        }
        finally {PrefabUtility.UnloadPrefabContents(instance);}
    }

    public static void Distribute(Mesh mesh,Transform[] bones)
    {
        var weights=mesh.boneWeights;var vertices=mesh.vertices;var bindposes=mesh.bindposes;
        foreach(string side in new[]{"Left","Right"})
        {
            int lower=Array.FindIndex(bones,b=>b.name=="mixamorig:"+side+"ForeArm");
            var hand=bones.First(b=>b.name=="mixamorig:"+side+"Hand");
            var indices=Enumerable.Range(0,3).Select(i=>Array.FindIndex(bones,b=>b.name==side+"ForearmTwist"+i)).Concat(new[]{lower}).ToArray();
            if(indices.Any(i=>i<0)) continue;
            Vector3 axis=bones[lower].InverseTransformPoint(hand.position);
            for(int vi=0;vi<weights.Length;vi++)
            {
                var w=weights[vi];
                var entries=new[]{Tuple.Create(w.boneIndex0,w.weight0),Tuple.Create(w.boneIndex1,w.weight1),Tuple.Create(w.boneIndex2,w.weight2),Tuple.Create(w.boneIndex3,w.weight3)};
                float total=entries.Where(e=>indices.Contains(e.Item1)).Sum(e=>e.Item2);
                if(total<1e-7f) continue;
                var result=new Dictionary<int,float>();
                foreach(var e in entries) if(e.Item2>0 && !indices.Contains(e.Item1)) {if(!result.ContainsKey(e.Item1)) result[e.Item1]=0;result[e.Item1]+=e.Item2;}
                Vector3 p=bindposes[lower].MultiplyPoint3x4(vertices[vi]);
                float along=Vector3.Dot(p,axis)/axis.sqrMagnitude;
                float section=Mathf.Clamp01((along-.15f)/.7f)*3;
                int a=Mathf.Min(2,Mathf.FloorToInt(section));float f=section-a;
                result[indices[a]]=total*(1-f);result[indices[a+1]]=total*f;
                var sorted=result.Where(e=>e.Value>1e-7f).OrderByDescending(e=>e.Value).Take(4).ToArray();
                float sum=sorted.Sum(e=>e.Value);int[] ids=new int[4];float[] ws=new float[4];
                for(int i=0;i<sorted.Length;i++){ids[i]=sorted[i].Key;ws[i]=sorted[i].Value/sum;}
                weights[vi]=new BoneWeight {boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=ws[0],weight1=ws[1],weight2=ws[2],weight3=ws[3]};
            }
        }
        mesh.boneWeights=weights;
    }
}
