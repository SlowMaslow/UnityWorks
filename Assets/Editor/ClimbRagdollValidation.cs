using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClimbRagdollValidation
{
    [MenuItem("ClimbUp/Validation/Fall Ragdoll")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode (uses an isolated physics scene)");
        var scene = SceneManager.CreateScene("Ragdoll validation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics = scene.GetPhysicsScene();
        var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        SceneManager.MoveGameObjectToScene(player, scene);
        Transform[] targets = null;
        float maxJointGap=0, maxSpeed=0, motion=0;
        float baselineMotion=0, settledMotion=0, airError=0;
        var baselinePositions=new Vector3[600][];
        float lateAngularSpeed=0;
        float maxElbowBend=0;
        float minKnee=180,maxKnee=-180;
        string worstElbow="",worstKnee="";
        try
        {
            var c=player.GetComponent<ClimbController>(); c.enabled=false;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(ClimbController).GetMethod("SetupArmIK",flags).Invoke(c,null);
            targets=(Transform[])typeof(ClimbController).GetField("_ikTarget",flags).GetValue(c);
            var rag=player.GetComponent<ClimbRagdoll>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(floor,scene);
            floor.layer=LayerMask.NameToLayer("Platforms");
            floor.transform.position=new Vector3(0,-.25f,0);floor.transform.localScale=new Vector3(30,.5f,10);
            var bones=c.visualRig.GetComponentsInChildren<Transform>();
            var rest= bones.Select(t=>t.localRotation).ToArray();
            for(int cycle=0;cycle<6;cycle++)
            {
                c.bodyRb.position=new Vector3(0,3,0);c.bodyRb.transform.position=c.bodyRb.position;
                c.bodyRb.rotation=Quaternion.identity;
                var facing=(ClimbFacing)typeof(ClimbController).GetField("facing",flags).GetValue(c);
                facing.Reset();
                if(cycle>=2) for(int f=0;f<90;f++) facing.Step(-1,true,.1f,0,180,.02f);
                foreach(var ik in c.visualRig.GetComponents<TwoBoneArmIK>()) ik.ResetSolveHistory();
                typeof(LegSwing).GetField("_curSwing",flags).SetValue(c.visualRig.GetComponent<LegSwing>(),0f);
                rag.settleOnContact=cycle!=0;
                for(int i=0;i<2;i++) c.padRb[i].position=new Vector3((i==0?-.65f:.65f)*(cycle>=2?-1:1),4.2f,0);
                typeof(ClimbController).GetMethod("LateUpdate",flags).Invoke(c,null);
                var before=bones.Select(t=>t.position).ToArray();
                if(cycle==2) c.ForceBreak();
                else rag.Begin(new Vector3(.6f,1,0),cycle<3?new Vector3(0,0,1.5f):new Vector3(cycle==5?2:0,0,cycle==4?-4:4));
                rag.ApplyPose();
                for(int i=0;i<bones.Length;i++)
                    if(Vector3.Distance(before[i],bones[i].position)>.0001f) throw new Exception("Ragdoll entry popped: "+bones[i].name);
                var bodies=player.GetComponentsInChildren<Rigidbody>().Where(b=>b.gameObject.name.EndsWith(" Physics")).ToArray();
                if(bodies.Length!=15 || !c.bodyRb.isKinematic)throw new Exception("Invalid physics ownership");
                var joints=player.GetComponentsInChildren<Joint>().Where(j=>j.name.EndsWith(" Physics")).ToArray();
                var start=rag.TrackingPosition;
                var kneeAxes=new System.Collections.Generic.Dictionary<string,Vector3>();
                foreach(string side in new[]{"Left","Right"})
                {
                    var thigh=bones.First(t=>t.name=="mixamorig:"+side+"UpLeg");
                    kneeAxes[side]=thigh.InverseTransformDirection(c.visualRig.right);
                }
                bool touched=false;
                for(int step=0;step<600;step++)
                {
                    rag.StepContactDamping(.02f);
                    physics.Simulate(.02f);rag.ApplyPose();
                    if(cycle>0)
                        foreach(string side in new[]{"Left","Right"})
                        {
                            var upper=bones.First(t=>t.name=="mixamorig:"+side+"Arm");
                            var lower=bones.First(t=>t.name=="mixamorig:"+side+"ForeArm");
                            var hand=bones.First(t=>t.name=="mixamorig:"+side+"Hand");
                            float angle=Vector3.Angle(lower.position-upper.position,hand.position-lower.position);
                            if(angle>maxElbowBend) {maxElbowBend=angle;worstElbow=$"cycle {cycle} step {step} {side}";}
                            var thigh=bones.First(t=>t.name=="mixamorig:"+side+"UpLeg");
                            var shin=bones.First(t=>t.name=="mixamorig:"+side+"Leg");
                            var foot=bones.First(t=>t.name=="mixamorig:"+side+"Foot");
                            float knee=Vector3.SignedAngle(shin.position-thigh.position,foot.position-shin.position,thigh.TransformDirection(kneeAxes[side]));
                            if(knee<minKnee) {minKnee=knee;worstKnee=$"cycle {cycle} step {step} {side}";}
                            maxKnee=Mathf.Max(maxKnee,knee);
                        }
                    touched |= rag.SupportBlend>0;
                    if(cycle==0) baselinePositions[step]=bodies.Select(b=>b.position).ToArray();
                    if(cycle==1 && !touched) for(int b=0;b<bodies.Length;b++) airError=Mathf.Max(airError,Vector3.Distance(baselinePositions[step][b],bodies[b].position));
                    if(step>=130)
                    {
                        float energy=bodies.Sum(b=>b.linearVelocity.sqrMagnitude+b.angularVelocity.sqrMagnitude);
                        if(cycle==0) baselineMotion+=energy;
                        if(cycle==1) settledMotion+=energy;
                    }
                    if(cycle>0 && step>=500) lateAngularSpeed=Mathf.Max(lateAngularSpeed,bodies.Max(b=>b.angularVelocity.magnitude));
                    if(cycle==1 && (step==25 || step==100)) Snapshot(player,rag.TrackingPosition,"ragdoll-"+step);
                    if(cycle>=2 && (step==50 || step==100)) Snapshot(player,rag.TrackingPosition,$"ragdoll-{cycle}-{step}");
                    foreach(var b in bodies)
                    {
                        if(float.IsNaN(b.position.x)||b.position.y < -1)throw new Exception("Invalid physics/floor penetration");
                        if(cycle>0) maxSpeed=Mathf.Max(maxSpeed,b.linearVelocity.magnitude);
                    }
                    if(cycle>0) foreach(var j in joints) maxJointGap=Mathf.Max(maxJointGap,Vector3.Distance(j.GetComponent<Rigidbody>().position+j.GetComponent<Rigidbody>().rotation*j.anchor,j.connectedBody.position+j.connectedBody.rotation*j.connectedAnchor));
                }
                if(cycle==1 && (!touched || airError>.001f || settledMotion>=baselineMotion))
                    throw new Exception($"Contact damping regression: touched={touched}, air={airError}, motion={baselineMotion}->{settledMotion}");
                motion=Mathf.Max(motion,Vector3.Distance(start,rag.TrackingPosition));
                if(cycle>0 && (maxSpeed>40 || maxJointGap>.25f || motion<.5f))throw new Exception("Unstable/inactive ragdoll cycle "+cycle+": "+maxJointGap+" / "+maxSpeed);
                if(cycle==2) c.Revive(new Vector3(0,3,0));
                else rag.Restore();
                if(rag.Active||c.bodyRb.isKinematic||player.GetComponentsInChildren<Joint>().Any(j=>j.name.EndsWith(" Physics")))throw new Exception("Ragdoll did not restore");
                for(int i=0;i<bones.Length;i++)
                    if(Quaternion.Angle(rest[i],bones[i].localRotation)>.05f)throw new Exception("Pose not restored: "+bones[i].name);
            }
            Debug.Log($"Ragdoll anatomy: elbow={maxElbowBend} at {worstElbow}; knee={minKnee}..{maxKnee} at {worstKnee}");
            if(lateAngularSpeed>3.001f)throw new Exception("Ragdoll kept thrashing after landing: "+lateAngularSpeed);
            if(maxElbowBend>145f)throw new Exception("Elbow overfolded: "+maxElbowBend);
            if(minKnee < -10f || maxKnee>145f)throw new Exception($"Knee out of range: {minKnee}..{maxKnee}");
            System.IO.File.WriteAllText("Temp/SkinWork/Unity/ragdoll-validation.txt",$"6 falls/restores; 15 bodies; 3600 physics steps; max joint gap={maxJointGap}; max speed={maxSpeed}; travel={motion}; air difference={airError}; late motion={baselineMotion}->{settledMotion}; late angular speed={lateAngularSpeed}; floor collision and pose restore passed");
        }
        finally
        {
            if(targets!=null)foreach(var t in targets)if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
            player.SetActive(false);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    static void Snapshot(GameObject player, Vector3 centre, string name)
    {
        var preview=new PreviewRenderUtility();
        var meshes=new System.Collections.Generic.List<Mesh>();
        try
        {
            foreach(var r in player.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled && r.sharedMesh!=null))
            {
                var mesh=new Mesh();r.BakeMesh(mesh);meshes.Add(mesh);
                var go=new GameObject("Ragdoll snapshot");preview.AddSingleGO(go);
                go.transform.SetPositionAndRotation(r.transform.position,r.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);preview.AddSingleGO(floor);
            floor.transform.position=new Vector3(0,-.25f,0);floor.transform.localScale=new Vector3(30,.5f,10);
            typeof(Skin01Validation).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,
                new object[]{preview,name,centre+new Vector3(0,.3f,-6),(Vector3?)centre,1.9f});
        }
        finally {preview.Cleanup();foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);}
    }
}

