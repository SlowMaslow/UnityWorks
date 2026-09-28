using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Skin01Validation
{
    [MenuItem("ClimbUp/Skins/Validate Approved Rig")]
    public static void ValidateApprovedRig()
    {
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Skins/Skin01/Skin01.prefab"));
        var preview=new PreviewRenderUtility();
        var snapshots=new System.Collections.Generic.List<Mesh>();
        try
        {
            preview.AddSingleGO(instance);
            var wardrobe=instance.GetComponent<SkinWardrobe>();
            wardrobe.EnsureSlots();
            foreach(SkinAccessorySlot slot in Enum.GetValues(typeof(SkinAccessorySlot))) wardrobe.Unequip(slot);
            foreach(string item in new[]{"HeadClean","Hair"})
                if(!wardrobe.Equip(AssetDatabase.LoadAssetAtPath<SkinAccessory>("Assets/Models/Skins/Skin01/Accessories/"+item+".asset"))) throw new Exception(item);
            var body=wardrobe.Body;
            var active=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled && r.sharedMesh!=null).ToArray();
            int triangles=active.Sum(r=>r.sharedMesh.triangles.Length/3);
            Render(preview,"approved-game-body",new Vector3(.8f,.7f,3));
            var groups=new System.Collections.Generic.Dictionary<Vector3Int,System.Collections.Generic.List<Tuple<int,int>>>();
            for(int ri=0;ri<active.Length;ri++)
            {
                var r=active[ri];var v=r.sharedMesh.vertices;
                foreach(int vi in r.sharedMesh.triangles.Distinct())
                {
                    Vector3 p=r.transform.TransformPoint(v[vi]);
                    var key=new Vector3Int(Mathf.RoundToInt(p.x*1000000),Mathf.RoundToInt(p.y*1000000),Mathf.RoundToInt(p.z*1000000));
                    if(!groups.ContainsKey(key)) groups[key]=new System.Collections.Generic.List<Tuple<int,int>>();
                    groups[key].Add(Tuple.Create(ri,vi));
                }
            }
            var seams=groups.Values.Where(g=>g.Count>1).ToArray();
            var iks=new TwoBoneArmIK[2];
            for(int i=0;i<2;i++)
            {
                string side=i==0?"Left":"Right";
                var ik=instance.AddComponent<TwoBoneArmIK>();iks[i]=ik;
                ik.upper=body.bones.First(b=>b.name=="mixamorig:"+side+"Arm");
                ik.lower=body.bones.First(b=>b.name=="mixamorig:"+side+"ForeArm");
                ik.hand=body.bones.First(b=>b.name=="mixamorig:"+side+"Hand");
                ik.target=new GameObject("Validation target").transform;ik.target.SetParent(instance.transform);
                ik.bendSign=i==0?1:-1;ik.Init();
            }
            float maxGap=0,maxHandError=0;
            for(int pose=0;pose<4;pose++)
            {
                for(int i=0;i<2;i++)
                {
                    var ik=iks[i];ik.ResetPose();
                    float reach=Vector3.Distance(ik.upper.position,ik.lower.position)+Vector3.Distance(ik.lower.position,ik.hand.position);
                    ik.target.position=ik.upper.position+new Vector3((i==0?-1:1)*(.4f+pose*.08f),.7f,0).normalized*reach*.7f;
                    ik.Solve();maxHandError=Mathf.Max(maxHandError,Vector3.Distance(ik.hand.position,ik.target.position));
                }
                foreach(string name in new[]{"Neck","Head","LeftFoot","RightFoot","LeftUpLeg"})
                    body.bones.First(b=>b.name=="mixamorig:"+name).localRotation*=Quaternion.Euler(7,3,2);
                var baked=new Vector3[active.Length][];
                for(int ri=0;ri<active.Length;ri++)
                {
                    CheckMesh(active[ri]);var mesh=new Mesh();active[ri].BakeMesh(mesh);
                    baked[ri]=mesh.vertices.Select(v=>active[ri].transform.TransformPoint(v*.01f)).ToArray();
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                foreach(var group in seams)
                {
                    var first=group[0];var p=baked[first.Item1][first.Item2];
                    foreach(var entry in group) maxGap=Mathf.Max(maxGap,Vector3.Distance(p,baked[entry.Item1][entry.Item2]));
                }
            }
            if(maxGap>0.00002f) throw new Exception("Shared border split: "+maxGap);
            if(maxHandError>.001f) throw new Exception("IK target error: "+maxHandError);
            foreach(var r in active)
            {
                var baked=new Mesh();r.BakeMesh(baked);snapshots.Add(baked);
                var go=new GameObject("IK snapshot");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,r.gameObject.scene);
                go.transform.SetParent(r.transform,false);go.transform.localScale=Vector3.one*.01f;
                go.AddComponent<MeshFilter>().sharedMesh=baked;go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;
            }
            Render(preview,"approved-game-ik",new Vector3(.8f,.7f,3));
            File.WriteAllText("Temp/SkinWork/Unity/approved-rig-validation.json", "{\"triangles\":"+triangles+",\"poses\":4,\"sharedPositionGroups\":"+seams.Length+",\"maxSeamGap\":"+maxGap.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"maxHandTargetError\":"+maxHandError.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"}");
        }
        finally { preview.Cleanup();foreach(var mesh in snapshots) UnityEngine.Object.DestroyImmediate(mesh); }
    }
    [MenuItem("ClimbUp/Skins/Render Accessory Catalog")]
    public static void RenderCatalog()
    {
        const string folder="Assets/Models/Skins/Skin01/Previews";
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory("Temp/SkinWork/Unity");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Skins/Skin01/Skin01.prefab");
        var instance=UnityEngine.Object.Instantiate(source);
        var preview=new PreviewRenderUtility();
        Mesh headPreview=null;
        try
        {
            preview.AddSingleGO(instance);
            var wardrobe=instance.GetComponent<SkinWardrobe>();
            wardrobe.EnsureSlots();
            var full=wardrobe.Body.sharedMesh;
            headPreview=UnityEngine.Object.Instantiate(full);
            for(int sm=0;sm<full.subMeshCount;sm++)
            {
                var indices=full.GetTriangles(sm);var kept=new System.Collections.Generic.List<int>();
                for(int i=0;i<indices.Length;i+=3)
                    if(Mathf.RoundToInt(full.uv2[indices[i]].x)==0) {kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}
                headPreview.SetTriangles(kept,sm,false);
            }
            foreach(var item in SkinAccessoryDatabase.Instance.accessories)
            {
                foreach(SkinAccessorySlot slot in Enum.GetValues(typeof(SkinAccessorySlot))) wardrobe.Unequip(slot);
                if(!wardrobe.Equip(item)) throw new Exception("Cannot equip "+item.name);
                if(item.slot==SkinAccessorySlot.Headwear || item.slot==SkinAccessorySlot.Glasses) wardrobe.Body.sharedMesh=headPreview;
                Vector3 target;float size;
                switch(item.slot)
                {
                    case SkinAccessorySlot.Headwear:
                    case SkinAccessorySlot.Head:
                    case SkinAccessorySlot.Glasses: target=new Vector3(0,.868f,0);size=.115f;break;
                    case SkinAccessorySlot.Outerwear: target=new Vector3(0,.61f,0);size=.43f;break;
                    case SkinAccessorySlot.Pants: target=new Vector3(0,.28f,0);size=.26f;break;
                    default: target=new Vector3(0,.055f,.015f);size=.16f;break;
                }
                string name="item_"+item.name;
                Render(preview,name,target+new Vector3(.45f,.18f,2),target,size);
                string path=folder+"/"+item.name+".png";
                File.Copy("Temp/SkinWork/Unity/"+name+".png",path,true);
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.maxTextureSize=512;importer.mipmapEnabled=false;importer.SaveAndReimport();
                item.previewSprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);EditorUtility.SetDirty(item);
            }
            AssetDatabase.SaveAssets();
        }
        finally { preview.Cleanup();if(headPreview!=null) UnityEngine.Object.DestroyImmediate(headPreview); }
    }

    [MenuItem("ClimbUp/Skins/Validate and Render Skin01")]
    public static void Validate()
    {
        const string root="Assets/Models/Skins/Skin01/";
        Directory.CreateDirectory("Temp/SkinWork/Unity");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Skin01.prefab");
        var instance=UnityEngine.Object.Instantiate(source);
        var preview=new PreviewRenderUtility();
        var snapshots=new System.Collections.Generic.List<Mesh>();
        try
        {
            preview.AddSingleGO(instance);
            var body=instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var wardrobe=instance.GetComponent<SkinWardrobe>();
            if(body==null || wardrobe==null) throw new Exception("Missing shared body/wardrobe");
            int bodyTriangleCount=body.sharedMesh.triangles.Length/3;
            int bones=instance.GetComponentsInChildren<Transform>().Length;
            CheckMesh(body);
            Render(preview,"body-front",new Vector3(0,.6f,3));
            Render(preview,"body-side",new Vector3(3,.6f,0));
            Render(preview,"body-back",new Vector3(0,.6f,-3));
            Render(preview,"head-side",new Vector3(3,.85f,0),new Vector3(0,.85f,0),.14f);
            Render(preview,"feet",new Vector3(.7f,.3f,1),new Vector3(0,.07f,.02f),.14f);
            Render(preview,"underwear",new Vector3(0,.43f,3),new Vector3(0,.43f,0),.16f);
            foreach(var slot in Enum.GetValues(typeof(SkinAccessorySlot)).Cast<SkinAccessorySlot>())
                if(SkinAccessoryDatabase.Instance.GetSlot(slot).Length>0 && !wardrobe.Equip(SkinAccessoryDatabase.Instance.GetSlot(slot)[0]))
                    throw new Exception("Could not equip "+slot);
            var renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            int triangles=renderers.Where(r=>r.sharedMesh!=null).Sum(r=>r.sharedMesh.triangles.Length/3);
            if(triangles>6000) throw new Exception("Outfit over budget");
            foreach(var r in renderers)
            {
                if(r.sharedMesh==null) continue;
                CheckMesh(r);
                if(!r.bones.SequenceEqual(body.bones)) throw new Exception("Accessory uses a different skeleton");
            }
            if(instance.GetComponentsInChildren<Transform>().Length!=bones+SkinWardrobe.SlotCount) throw new Exception("Unexpected extra bones");
            Render(preview,"equipped",new Vector3(1.6f,.8f,3));
            // Exercise all combinations of the two available items in each slot.
            var choices=Enum.GetValues(typeof(SkinAccessorySlot)).Cast<SkinAccessorySlot>().Select(s=>new SkinAccessory[]{null}.Concat(SkinAccessoryDatabase.Instance.GetSlot(s)).ToArray()).ToArray();
            int combinations=0;
            void CheckCombinations(int slot)
            {
                if(slot==SkinWardrobe.SlotCount)
                {
                    var active=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.sharedMesh!=null && r.enabled).ToArray();
                    if(active.Sum(r=>r.sharedMesh.triangles.Length/3)>SkinWardrobe.TriangleBudget) throw new Exception("Combination exceeds budget");
                    combinations++;return;
                }
                foreach(var item in choices[slot])
                {
                    if(item==null) wardrobe.Unequip((SkinAccessorySlot)slot);
                    else if(!wardrobe.Equip(item)) throw new Exception("Invalid combination: "+item.name);
                    CheckCombinations(slot+1);
                }
            }
            CheckCombinations(0);
            foreach(var slot in Enum.GetValues(typeof(SkinAccessorySlot)).Cast<SkinAccessorySlot>())
            {
                var first=SkinAccessoryDatabase.Instance.GetSlot(slot).FirstOrDefault();
                if(first!=null) wardrobe.Equip(first); else wardrobe.Unequip(slot);
            }
            if(instance.GetComponentsInChildren<Transform>().Length!=bones+SkinWardrobe.SlotCount) throw new Exception("Slot replacement created transforms");
            wardrobe.Unequip(SkinAccessorySlot.Footwear);
            var restoredVertices=body.sharedMesh.vertices;
            if(!body.sharedMesh.triangles.Any(i=>restoredVertices[i].y<.0005f)) throw new Exception("Feet were not restored");
            renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.sharedMesh!=null && r.enabled).ToArray();
            var restMesh=new Mesh();body.BakeMesh(restMesh);
            var restVertices=restMesh.vertices;UnityEngine.Object.DestroyImmediate(restMesh);
            var arm=body.bones.First(b=>b.name=="mixamorig:LeftForeArm");
            arm.localRotation*=Quaternion.Euler(0,0,65);
            var leg=body.bones.First(b=>b.name=="mixamorig:RightUpLeg");
            leg.localRotation*=Quaternion.Euler(30,0,0);
            var posedMesh=new Mesh();body.BakeMesh(posedMesh);
            float deformation=restVertices.Zip(posedMesh.vertices,(a,b)=>(a-b).sqrMagnitude).Max();
            UnityEngine.Object.DestroyImmediate(posedMesh);
            if(deformation<0.00000001f) throw new Exception("Pose did not deform mesh");
            foreach(var r in renderers) CheckMesh(r);
            // Bake an explicit snapshot so edit-mode previews do not reuse the last GPU skinning frame.
            foreach(var r in renderers)
            {
                var baked=new Mesh();r.BakeMesh(baked);
                snapshots.Add(baked);
                var go=new GameObject("Pose snapshot");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,r.gameObject.scene);
                go.transform.SetParent(r.transform,false);
                // Skin01's historical renderer has scale 100; BakeMesh includes that bind scale.
                go.transform.localScale=Vector3.one*.01f;
                go.AddComponent<MeshFilter>().sharedMesh=baked;
                go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
                r.enabled=false;
            }
            Render(preview,"posed",new Vector3(1.6f,.8f,3));
            File.WriteAllText("Temp/SkinWork/Unity/validation.json", JsonUtility.ToJson(new Report {
                bodyTriangles=bodyTriangleCount,totalTriangles=triangles,bones=body.bones.Length,
                renderers=renderers.Length,sharedSkeleton=true,poseBakePassed=true,combinations=combinations
            },true));
            Debug.Log("[Skin01] Validation passed. Body + four accessories: "+triangles+" triangles.");
        }
        finally
        {
            preview.Cleanup();
            foreach(var mesh in snapshots) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
    private static void CheckMesh(SkinnedMeshRenderer renderer)
    {
        if(renderer.sharedMesh==null || renderer.bones.Any(b=>b==null)) throw new Exception("Missing mesh or bone");
        foreach(var w in renderer.sharedMesh.boneWeights)
            if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>0.001f) throw new Exception("Invalid skin weights");
        var baked=new Mesh();renderer.BakeMesh(baked);
        try
        {
            if(baked.vertices.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)))
                throw new Exception("Invalid deformed vertex");
            if(baked.bounds.size.magnitude>3) throw new Exception("Exploded skinning");
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); }
    }
    private static void Render(PreviewRenderUtility preview,string name,Vector3 position,Vector3? target=null,float size=.64f)
    {
        preview.camera.orthographic=true;preview.camera.orthographicSize=size;
        preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
        preview.camera.transform.position=position;
        preview.camera.transform.LookAt(target ?? new Vector3(0,.48f,0));
        preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.16f,.18f,.20f);
        preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);
        preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(20,30,0);
        preview.ambientColor=new Color(.4f,.4f,.4f);
        preview.BeginStaticPreview(new Rect(0,0,1000,1000));
        preview.Render();
        var image=preview.EndStaticPreview();
        File.WriteAllBytes("Temp/SkinWork/Unity/"+name+".png",image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
    [Serializable] private class Report
    {
        public int bodyTriangles,totalTriangles,bones,renderers,combinations;
        public bool sharedSkeleton,poseBakePassed;
    }
}
