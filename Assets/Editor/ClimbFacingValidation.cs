using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ClimbFacingValidation
{
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    [MenuItem("ClimbUp/Validation/Climbing Turn Controller")]
    public static void RunController()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        var preview=new PreviewRenderUtility();
        Transform[] targets=null;
        float maxGripError=0;
        try
        {
            preview.AddSingleGO(go);
            var c=go.GetComponent<ClimbController>();c.enabled=false;c.hidePadVisual=false;
            var type=typeof(ClimbController);
            type.GetMethod("SetupArmIK",flags).Invoke(c,null);
            targets=(Transform[])type.GetField("_ikTarget",flags).GetValue(c);
            var iks=(TwoBoneArmIK[])type.GetField("_armIK",flags).GetValue(c);
            var facing=(ClimbFacing)type.GetField("facing",flags).GetValue(c);
            var late=type.GetMethod("LateUpdate",flags);
            for(int frame=0;frame<180;frame++)
            {
                // Spread hands, cross them, then restore. Rotate the capsule in-plane too.
                float spread=frame<45?.45f:frame<115?-.45f:.45f;
                c.bodyRb.rotation=Quaternion.Euler(0,0,Mathf.Sin(frame*.03f)*15);
                c.bodyRb.transform.rotation=c.bodyRb.rotation;
                for(int i=0;i<2;i++) c.padRb[i].position=c.bodyRb.transform.TransformPoint(new Vector3(i==0?-spread:spread,1.15f,0));
                facing.Step(spread*2,true,c.shoulderOffsetX*c.turnSeparation,c.turnHoldTime,c.turnSpeed,.02f);
                var before=c.padRb.Select(p=>p.position).ToArray();
                late.Invoke(c,null);
                for(int i=0;i<2;i++)
                {
                    var grip=c.handBallLocalPos;if(i==1) grip.x=-grip.x;
                    float depth=iks[i].hand.TransformVector(Vector3.forward).magnitude*grip.z;
                    grip.z=0f;
                    var palm=iks[i].hand.TransformPoint(grip);
                    maxGripError=Mathf.Max(maxGripError,Vector3.Distance(palm,before[i]-Vector3.forward*depth));
                    Require(palm.z<before[i].z,"Palm moved behind the sphere during a turn");
                    Require(c.padRb[i].position==before[i],"Visual turning moved a grip anchor");
                }
                if(frame==110) Require(facing.Yaw==180,"Controller did not reach face-on orientation");
            }
            Require(maxGripError<.0001f,"Palm detached: "+maxGripError);
            Require(facing.Yaw==0,"Controller did not return to back-facing orientation");
            for(int i=0;i<60;i++) facing.Step(-1,true,.1f,0,240,.02f);
            c.MoveTo(Vector3.zero);late.Invoke(c,null);
            Require(facing.Yaw==0,"Teleport retained old facing");
            var rest=c.visualRig.rotation;
            c.MoveTo(Vector3.zero);late.Invoke(c,null);
            Require(Quaternion.Angle(rest,c.visualRig.rotation)<.001f,"Repeated reset changed facing");
            File.WriteAllText("Temp/SkinWork/Unity/climb-controller-validation.txt","180 controller frames; maxGripError="+maxGripError+"; anchors preserved; forward/back turn and teleport reset passed");
        }
        finally
        {
            if(targets!=null) foreach(var target in targets) if(target!=null) UnityEngine.Object.DestroyImmediate(target.gameObject);
            preview.Cleanup();
        }
    }
    [MenuItem("ClimbUp/Validation/Climbing Turns")]
    public static void Run()
    {
        var facing=new ClimbFacing();
        for(int i=0;i<4;i++) facing.Step(-.5f,true,.2f,.12f,240,.02f);
        Require(!facing.Reversed,"Turn reacted before hold time");
        for(int i=0;i<60;i++) facing.Step(-.5f,true,.2f,.12f,240,.02f);
        Require(facing.Reversed && facing.Yaw==180,"Crossed hands did not turn");
        for(int i=0;i<50;i++) facing.Step(i%2==0?.19f:-.19f,true,.2f,.12f,240,.02f);
        Require(facing.Reversed,"Dead zone caused flicker");
        for(int i=0;i<60;i++) facing.Step(.5f,false,.2f,.12f,240,.02f);
        Require(facing.Reversed,"Dangling hand triggered turn");
        for(int i=0;i<60;i++) facing.Step(.5f,true,.2f,.12f,240,.02f);
        Require(!facing.Reversed && facing.Yaw==0,"Hands did not restore facing");
        facing.Reset();Require(facing.Yaw==0 && !facing.Reversed,"Reset failed");
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Skins/Skin01/Skin01.prefab"));
        var preview=new PreviewRenderUtility();
        float maxError=0,maxLengthError=0,maxElbowStep=0,maxWristTwist=0;int poses=0;
        var previousElbows=new System.Collections.Generic.Dictionary<string,Vector3>();
        try
        {
            preview.AddSingleGO(go);
            var wardrobe=go.GetComponent<SkinWardrobe>();wardrobe.EnsureSlots();
            foreach(SkinAccessorySlot s in Enum.GetValues(typeof(SkinAccessorySlot))) wardrobe.Unequip(s);
            wardrobe.Equip(AssetDatabase.LoadAssetAtPath<SkinAccessory>("Assets/Models/Skins/Skin01/Accessories/Hair.asset"));
            var bones=wardrobe.Body.bones;
            var solvers=new TwoBoneArmIK[2];
            for(int i=0;i<2;i++)
            {
                string side=i==0?"Left":"Right";
                var ik=go.AddComponent<TwoBoneArmIK>();solvers[i]=ik;
                ik.upper=bones.First(b=>b.name=="mixamorig:"+side+"Arm");
                ik.lower=bones.First(b=>b.name=="mixamorig:"+side+"ForeArm");
                ik.hand=bones.First(b=>b.name=="mixamorig:"+side+"Hand");
                ik.target=new GameObject("Test target").transform;ik.target.SetParent(go.transform);
                ik.bendReference=go.transform;ik.bendSign=i==0?1:-1;ik.Init();
            }
            for(int target=0;target<14;target++)
            {
                foreach(var ik in solvers) ik.ResetSolveHistory();
                for(int step=0;step<=360;step++)
                {
                    // Two complete outward/return cycles without resetting solve history.
                    int phase=step%180;
                    int yaw=phase<=90?phase*2:(180-phase)*2;
                    go.transform.rotation=Quaternion.Euler(0,yaw,0);
                    foreach(var ik in solvers)
                    {
                        ik.ResetPose();
                        Quaternion restHand=ik.hand.localRotation;
                        float a=Vector3.Distance(ik.upper.position,ik.lower.position),b=Vector3.Distance(ik.lower.position,ik.hand.position);
                        var direction=new Vector3(-ik.bendSign*.6f,1f,(target-2)*.3f).normalized;
                        if(target>=6)
                        {
                            float angle=(target-6)*Mathf.PI/4f;
                            direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),.2f).normalized;
                        }
                        float distance=target==4?0:target==5?(a+b)*3:(a+b)*.7f;
                        ik.target.position=ik.upper.position+direction*distance;
                        ik.palmWorldFacing=go.transform.forward;
                        ik.Solve(1f/120f);poses++;
                        Vector3 forearm=(ik.hand.position-ik.lower.position).normalized;
                        Require(Vector3.Dot(ik.hand.TransformDirection(ik.palmLocalAxis),go.transform.forward)>.999f,"Palm is edge-on to grip frame");
                        Vector3 restPalm=Vector3.ProjectOnPlane(ik.lower.rotation*restHand*ik.palmLocalAxis,forearm);
                        Vector3 palm=Vector3.ProjectOnPlane(ik.hand.rotation*ik.palmLocalAxis,forearm);
                        if(restPalm.sqrMagnitude>1e-8f && palm.sqrMagnitude>1e-8f)
                            maxWristTwist=Mathf.Max(maxWristTwist,Vector3.Angle(restPalm,palm));
                        if(target!=4 && target!=5)
                        {
                            string key=ik.hand.name+target;
                            Vector3 elbow=(ik.lower.position-ik.upper.position).normalized;
                            if(previousElbows.TryGetValue(key,out var previous)) maxElbowStep=Mathf.Max(maxElbowStep,Vector3.Angle(previous,elbow));
                            previousElbows[key]=elbow;
                        }
                        Require(!float.IsNaN(ik.hand.position.x) && !float.IsInfinity(ik.hand.position.y),"Invalid IK output");
                        maxLengthError=Mathf.Max(maxLengthError,Mathf.Abs(Vector3.Distance(ik.upper.position,ik.lower.position)-a),Mathf.Abs(Vector3.Distance(ik.lower.position,ik.hand.position)-b));
                        if(target!=4 && target!=5) maxError=Mathf.Max(maxError,Vector3.Distance(ik.hand.position,ik.target.position));
                    }
                }
            }
            Require(maxError<.0001f,"Reachable target error: "+maxError);
            Require(maxLengthError<.0001f,"Bone length changed: "+maxLengthError);
            Require(maxElbowStep<35f,"Elbow jumped during turn: "+maxElbowStep);
            Require(maxWristTwist<.1f,"Axial twist concentrated at wrist: "+maxWristTwist);
            foreach(int yaw in new[]{0,90,180})
            {
                go.transform.rotation=Quaternion.Euler(0,yaw,0);
                foreach(var ik in solvers)
                {
                    ik.ResetSolveHistory();
                    float side=-ik.bendSign*Mathf.Cos(yaw*Mathf.Deg2Rad);
                    ik.palmWorldFacing=go.transform.forward;
                    ik.target.position=new Vector3(side*.22f,.92f,.06f);
                    for(int frame=0;frame<120;frame++) {ik.ResetPose();ik.Solve(1f/60f);}
                }
                var snapshots=new System.Collections.Generic.List<Mesh>();
                var snapshotObjects=new System.Collections.Generic.List<GameObject>();
                var renderers=go.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled && r.sharedMesh!=null).ToArray();
                foreach(var r in renderers)
                {
                    var mesh=new Mesh();r.BakeMesh(mesh);snapshots.Add(mesh);
                    var obj=new GameObject("Snapshot");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj,r.gameObject.scene);
                    obj.transform.SetParent(r.transform,false);obj.transform.localScale=Vector3.one*.01f;
                    obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;r.enabled=false;snapshotObjects.Add(obj);
                }
                typeof(Skin01Validation).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{preview,"climb-turn-"+yaw,new Vector3(0,.6f,-3),(Vector3?)new Vector3(0,.5f,0),.6f});
                foreach(var obj in snapshotObjects) UnityEngine.Object.DestroyImmediate(obj);
                foreach(var mesh in snapshots) UnityEngine.Object.DestroyImmediate(mesh);
                foreach(var r in renderers) r.enabled=true;
            }
            File.WriteAllText("Temp/SkinWork/Unity/climb-turn-validation.txt", "Poses="+poses+"; maxTargetError="+maxError+"; maxLengthError="+maxLengthError+"; maxElbowStep="+maxElbowStep+"; maxWristTwist="+maxWristTwist+"; repeated turns/hysteresis/reset passed");
        }
        finally {preview.Cleanup();}
        Debug.Log("Climbing turns: "+poses+" IK poses passed");
    }
}
