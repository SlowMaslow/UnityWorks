using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>Imports Blender mesh data without replacing the established player rig.</summary>
public static class Skin01AssetBuilder
{
    private const string Root = "Assets/Models/Skins/Skin01";
    private const string Source = "SourceAssets/Skins";

    [MenuItem("ClimbUp/Skins/Import Current Character")]
    public static void BuildApproved()
    {
        string sourceDirectory = Source + "/Export";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Skin01.prefab");
        var sourceSmr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = sourceSmr.bones;
        var binds = sourceSmr.sharedMesh.bindposes;
        var names = bones.Select(b => b.name).ToArray();
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Accessories");
        AssetDatabase.Refresh();
        var materials = new Dictionary<string, Material>();
        foreach (var entry in JObject.Parse(File.ReadAllText(sourceDirectory + "/palette.json")))
        {
            var c=(JArray)entry.Value;
            var color = new Color((float)c[0],(float)c[1],(float)c[2]);
            materials[entry.Key]=MakeApprovedMaterial(entry.Key,color);
        }
        Mesh bodyMesh = null;
        Material[] bodyMaterials = null;
        int total = 0;
        int[] maximumSlotTriangles=new int[SkinWardrobe.SlotCount];
        var catalog=new List<SkinAccessory>();
        var slotNames=new Dictionary<string,SkinAccessorySlot> {
            {"HeadClean",SkinAccessorySlot.Head},{"Hair",SkinAccessorySlot.Headwear}
        };
        var displayNames=new Dictionary<string,string> {
            {"HeadClean","Clean-shaven"},{"Hair","Short hair"}
        };
        foreach (string name in new[]{"Skin01_Body","HeadClean","Hair"})
        {
            var data = JObject.Parse(File.ReadAllText(sourceDirectory + "/" + name + ".json"));
            var verts = (JArray)data["vertices"];
            var faces = (JArray)data["triangles"];
            if (faces.Count > 6000) throw new InvalidOperationException("Triangle budget: " + name);
            if(name=="Skin01_Body") total=faces.Count;
            else maximumSlotTriangles[(int)slotNames[name]]=Math.Max(maximumSlotTriangles[(int)slotNames[name]],faces.Count);
            var mesh = new Mesh { name = name };
            var positions = new Vector3[verts.Count];
            var normals = new Vector3[verts.Count];
            var uv = new Vector2[verts.Count];
            var regions = new Vector2[verts.Count];
            var weights = new BoneWeight[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                positions[i] = ConvertVector(v["position"]) * .01f;
                normals[i] = ConvertVector(v["normal"]).normalized;
                uv[i] = new Vector2((float)v["uv"][0], (float)v["uv"][1]);
                regions[i] = new Vector2((int)v["region"],0);
                int[] ids = new int[4]; float[] w = new float[4];
                for (int k = 0; k < v["bones"].Count(); k++)
                {
                    ids[k] = Array.IndexOf(names, (string)v["bones"][k]);
                    if (ids[k] < 0) throw new InvalidOperationException("Unknown bone " + v["bones"][k]);
                    w[k] = (float)v["weights"][k];
                }
                weights[i] = new BoneWeight { boneIndex0=ids[0],boneIndex1=ids[1],boneIndex2=ids[2],boneIndex3=ids[3],weight0=w[0],weight1=w[1],weight2=w[2],weight3=w[3] };
            }
            mesh.vertices=positions; mesh.normals=normals; mesh.uv=uv;mesh.uv2=regions;
            mesh.boneWeights=weights; mesh.bindposes=binds;
            SkinForearmRigBuilder.Distribute(mesh,bones);
            var mats = data["materials"].Select(t => materials[(string)t]).ToArray();
            mesh.subMeshCount=mats.Length;
            for (int s=0;s<mats.Length;s++)
            {
                var indices = new List<int>();
                foreach (var f in faces.Where(f => (int)f["material"] == s))
                {
                    indices.Add((int)f["indices"][0]);
                    indices.Add((int)f["indices"][2]);
                    indices.Add((int)f["indices"][1]);
                }
                mesh.SetTriangles(indices,s);
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string path=Root+"/Meshes/"+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            else
            {
                saved.Clear(); saved.vertices=mesh.vertices; saved.normals=mesh.normals;
                saved.uv=mesh.uv;saved.uv2=mesh.uv2; saved.boneWeights=mesh.boneWeights; saved.bindposes=mesh.bindposes;
                saved.subMeshCount=mesh.subMeshCount;
                for(int s=0;s<mesh.subMeshCount;s++) saved.SetTriangles(mesh.GetTriangles(s),s);
                saved.RecalculateBounds();saved.RecalculateTangents();saved.UploadMeshData(false);
                EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(mesh);
            }
            if(name=="Skin01_Body") { bodyMesh=saved;bodyMaterials=mats; }
            else
            {
                string accessoryPath=Root+"/Accessories/"+name+".asset";
                var accessory=AssetDatabase.LoadAssetAtPath<SkinAccessory>(accessoryPath);
                if(accessory==null) { accessory=ScriptableObject.CreateInstance<SkinAccessory>(); AssetDatabase.CreateAsset(accessory,accessoryPath); }
                accessory.slot=slotNames[name];
                if(string.IsNullOrEmpty(accessory.accessoryId))
                {
                    accessory.accessoryId="accessory_"+name.ToLowerInvariant();
                    accessory.displayName=displayNames[name];
                    accessory.price=0;
                }
                accessory.bodyRegion=SkinWardrobe.RegionForSlot(accessory.slot);
                accessory.mesh=saved;accessory.materials=mats;EditorUtility.SetDirty(accessory);
                catalog.Add(accessory);
            }
        }
        // Four replacement parts and two overlays. Base parts are included as alternatives.
        int[] baseCounts=new int[4];
        var tags=bodyMesh.uv2;var bodyIndices=bodyMesh.triangles;
        for(int i=0;i<bodyIndices.Length;i+=3) baseCounts[Mathf.RoundToInt(tags[bodyIndices[i]].x)]++;
        total=maximumSlotTriangles[(int)SkinAccessorySlot.Headwear]+maximumSlotTriangles[(int)SkinAccessorySlot.Glasses];
        foreach(var slot in new[]{SkinAccessorySlot.Head,SkinAccessorySlot.Outerwear,SkinAccessorySlot.Pants,SkinAccessorySlot.Footwear})
            total+=Math.Max(baseCounts[SkinWardrobe.RegionForSlot(slot)],maximumSlotTriangles[(int)slot]);
        if(total>6000) throw new InvalidOperationException("Worst-case outfit exceeds budget: "+total);
        var instance=PrefabUtility.LoadPrefabContents(Root+"/Skin01.prefab");
        try
        {
            var smr=instance.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.sharedMesh=bodyMesh;
            smr.sharedMaterials=bodyMaterials;
            var wardrobe=instance.GetComponent<SkinWardrobe>();
            if(wardrobe==null) wardrobe=instance.AddComponent<SkinWardrobe>();
            var so=new SerializedObject(wardrobe);so.FindProperty("body").objectReferenceValue=smr;
            so.FindProperty("baseBodyMesh").objectReferenceValue=bodyMesh;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(instance,Root+"/Skin01.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(instance); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Skin01] Current body imported; catalog and selections preserved.");
    }

    private static Vector3 ConvertVector(JToken p) => new Vector3(-(float)p[0],(float)p[2],-(float)p[1]);
    private static Material MakeApprovedMaterial(string name, Color color)
    {
        const string directory=Root+"/Materials/Toon";
        Directory.CreateDirectory(directory);
        var shader=Shader.Find("ClimbUp/Character Toon");
        if(shader==null) throw new InvalidOperationException("Character Toon shader missing");
        string path=directory+"/"+name+"_Toon.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(shader){name=name+"_Toon"};AssetDatabase.CreateAsset(mat,path); }
        mat.shader=shader;mat.color=color;
        mat.SetColor("_ShadowTint",new Color(.72f,.63f,.57f));
        mat.SetColor("_MidTint",new Color(.94f,.87f,.82f));
        mat.SetFloat("_Threshold",.06f);mat.SetFloat("_Softness",.18f);
        mat.SetFloat("_FacetLight",name=="Skin01_Body"?.8f:0f);
        EditorUtility.SetDirty(mat);return mat;
    }
}

