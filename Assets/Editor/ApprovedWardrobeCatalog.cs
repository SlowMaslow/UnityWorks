using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ApprovedWardrobeCatalog
{
    const string Root="Assets/Models/Skins/Skin01/";
    [MenuItem("ClimbUp/Skins/Rebuild Current Catalog and Previews")]
    public static void Rebuild()
    {
        var db=SkinAccessoryDatabase.Instance;
        db.accessories=new[]{"Hair","Glasses","Sunglasses"}.Select(n=>AssetDatabase.LoadAssetAtPath<SkinAccessory>(Root+"Accessories/"+n+".asset")).ToArray();
        if(db.accessories.Any(a=>a==null)) throw new Exception("Missing approved catalog item");
        db.emptySlotPreview=null;
        db.baseSlotPreviews=new Sprite[SkinWardrobe.SlotCount];
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Skin01.prefab"));
        var preview=new PreviewRenderUtility();
        try
        {
            preview.AddSingleGO(instance);
            var wardrobe=instance.GetComponent<SkinWardrobe>();wardrobe.EnsureSlots();
            foreach(SkinAccessorySlot slot in Enum.GetValues(typeof(SkinAccessorySlot))) wardrobe.Unequip(slot);
            var body=wardrobe.Body;var full=body.sharedMesh;
            foreach(SkinAccessorySlot slot in Enum.GetValues(typeof(SkinAccessorySlot)))
            {
                int region=Math.Max(0,SkinWardrobe.RegionForSlot(slot));
                var part=UnityEngine.Object.Instantiate(full);
                var regions=full.uv2;
                for(int sm=0;sm<full.subMeshCount;sm++)
                {
                    var indices=full.GetTriangles(sm);var kept=new System.Collections.Generic.List<int>();
                    for(int i=0;i<indices.Length;i+=3)
                        if(Mathf.RoundToInt(regions[indices[i]].x)==region) { kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]); }
                    part.SetTriangles(kept,sm,false);
                }
                body.sharedMesh=part;
                var arms=body.bones.Where(b=>b.name=="mixamorig:LeftArm" || b.name=="mixamorig:RightArm").ToArray();
                var rotations=arms.Select(b=>b.rotation).ToArray();
                if(region==1) foreach(var arm in arms) arm.rotation=Quaternion.AngleAxis(arm.position.x<0?60:-60,Vector3.forward)*arm.rotation;
                Vector3 target;float size;
                switch(region)
                {
                    case 0:target=new Vector3(0,.868f,0);size=.115f;break;
                    case 1:target=new Vector3(0,.57f,0);size=.33f;break;
                    case 2:target=new Vector3(0,.28f,0);size=.235f;break;
                    default:target=new Vector3(0,.045f,.025f);size=.12f;break;
                }
                preview.camera.orthographic=true;preview.camera.orthographicSize=size;
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
                preview.camera.transform.position=target+new Vector3(.45f,.18f,2);
                preview.camera.transform.LookAt(target);
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.16f,.18f,.20f);
                preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);
                preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(20,30,0);
                preview.ambientColor=new Color(.4f,.4f,.4f);
                preview.BeginStaticPreview(new Rect(0,0,768,768));preview.Render();var image=preview.EndStaticPreview();
                string path=Root+"Previews/Base_"+slot+".png";
                File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                body.sharedMesh=full;UnityEngine.Object.DestroyImmediate(part);
                for(int i=0;i<arms.Length;i++) arms[i].rotation=rotations[i];
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.maxTextureSize=512;importer.mipmapEnabled=false;importer.SaveAndReimport();
                db.baseSlotPreviews[(int)slot]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            EditorUtility.SetDirty(db);AssetDatabase.SaveAssets();
        }
        finally { preview.Cleanup(); }
        Skin01Validation.RenderCatalog();
    }
}

