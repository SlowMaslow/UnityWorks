using System.Collections.Generic;
using UnityEngine;

/// <summary>Unscaled physics proxies drive the modular rig only during a fall.</summary>
public sealed class ClimbRagdoll : MonoBehaviour
{
    sealed class Part
    {
        public Transform bone, end;
        public Rigidbody body;
        public Joint joint;
        public CapsuleCollider collider;
        public Quaternion boneOffset;
        public Quaternion restRotation;
        public Vector3 restAim;
        public ForearmTwistRig skinTwist;
        public Vector3 entryLocalPosition;
        public int parent;
        public float mass, radius, swing;
    }
    readonly List<Part> parts = new List<Part>();
    Transform[] pose;
    Vector3[] positions;
    Quaternion[] rotations;
    GameObject physicsRoot;
    Rigidbody controllerBody;
    bool previousKinematic, previousCollisions;
    Vector3 trackingOffset;
    [Tooltip("Гасить колебания после контакта с опорой, сохраняя свободное падение.")]
    public bool settleOnContact = true;
    float supportTime;
    bool groundSolverEnabled;
    public float SupportBlend { get; private set; }
    public bool Active { get; private set; }
    public Vector3 TrackingPosition => Active ? parts[0].body.position + trackingOffset : controllerBody.position;

    public void Initialize(Transform rig, Rigidbody body)
    {
        if (pose != null) return;
        controllerBody = body;
        pose = rig.GetComponentsInChildren<Transform>(true);
        positions = new Vector3[pose.Length]; rotations = new Quaternion[pose.Length];
        var map = new Dictionary<string, Transform>();
        for (int i = 0; i < pose.Length; i++)
        {
            positions[i] = pose[i].localPosition; rotations[i] = pose[i].localRotation;
            map[pose[i].name] = pose[i];
        }
        void Add(string bone, string end, int parent, float mass, float radius, float swing)
        {
            if (!map.TryGetValue("mixamorig:" + bone, out var b) || !map.TryGetValue("mixamorig:" + end, out var e))
                throw new System.InvalidOperationException("Missing ragdoll bone: " + bone + "/" + end);
            parts.Add(new Part { bone=b, end=e, parent=parent, mass=mass, radius=radius, swing=swing,
                restRotation=b.localRotation, restAim=b.InverseTransformDirection(e.position-b.position).normalized,
                skinTwist=b.GetComponent<ForearmTwistRig>() });
        }
        Add("Hips", "Spine1", -1, 10, .18f, 25);
        Add("Spine1", "Neck", 0, 14, .21f, 30);
        Add("Head", "HeadTop_End", 1, 4, .15f, 35);
        foreach (string side in new[] { "Left", "Right" })
        {
            int arm = parts.Count;
            Add(side+"Arm", side+"ForeArm", 1, 2, .085f, 80);
            Add(side+"ForeArm", side+"Hand", arm, 1.5f, .065f, 75);
            Add(side+"Hand", side+"HandIndex3", arm+1, .5f, .055f, 25);
            int leg = parts.Count;
            Add(side+"UpLeg", side+"Leg", 0, 5, .11f, 60);
            Add(side+"Leg", side+"Foot", leg, 3, .08f, 75);
            Add(side+"Foot", side+"ToeBase", leg+1, 1, .07f, 25);
        }
    }

    public void Begin(Vector3 velocity, Vector3 angularVelocity)
    {
        if (Active || parts.Count == 0) return;
        supportTime = 0f;
        SupportBlend = 0f;
        groundSolverEnabled = false;
        previousKinematic = controllerBody.isKinematic;
        previousCollisions = controllerBody.detectCollisions;
        trackingOffset = controllerBody.position - parts[0].bone.position;
        physicsRoot = new GameObject("Fall physics");
        physicsRoot.transform.SetParent(transform, false);
        // World-scale proxies avoid PhysX joints under the model's import scale.
        physicsRoot.transform.localScale = new Vector3(1f/transform.lossyScale.x, 1f/transform.lossyScale.y, 1f/transform.lossyScale.z);
        int mask = LayerMask.GetMask("Platforms", "WallCollider", "FallArea");
        var colliders = new List<Collider>();
        foreach (var p in parts)
        {
            p.entryLocalPosition = p.bone.localPosition;
            var go = new GameObject(p.bone.name + " Physics");
            go.layer = 2; // Ignore Raycast: never becomes a selectable pad.
            go.transform.SetParent(physicsRoot.transform, false);
            Vector3 segment = p.end.position - p.bone.position;
            go.transform.SetPositionAndRotation(p.bone.position, Quaternion.FromToRotation(Vector3.up, segment));
            p.boneOffset = Quaternion.Inverse(go.transform.rotation) * p.bone.rotation;
            var capsule = go.AddComponent<CapsuleCollider>();
            p.collider = capsule;
            capsule.direction = 1;
            capsule.radius = Mathf.Min(p.radius, segment.magnitude * .45f);
            capsule.height = Mathf.Max(segment.magnitude, capsule.radius * 2f);
            capsule.center = Vector3.up * segment.magnitude * .5f;
            capsule.includeLayers = mask; capsule.excludeLayers = ~mask;
            capsule.layerOverridePriority = 10;
            colliders.Add(capsule);
            p.body = go.AddComponent<Rigidbody>();
            p.body.isKinematic = true;
            p.body.mass = p.mass;
            p.body.linearDamping = .1f; p.body.angularDamping = .6f;
            p.body.maxDepenetrationVelocity = 2f;
            p.body.solverIterations = 32; p.body.solverVelocityIterations = 12;
            p.body.interpolation = RigidbodyInterpolation.Interpolate;
            p.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            go.AddComponent<RagdollSupportContact>().owner = this;
        }
        // Disable self-collision and collisions with the control capsule/pads.
        var playerColliders = GetComponentsInChildren<Collider>(true);
        foreach (var a in colliders)
            foreach (var b in playerColliders) if (a != b) Physics.IgnoreCollision(a, b);
        for (int i = 1; i < parts.Count; i++)
        {
            var p = parts[i];
            if (p.bone.name.EndsWith("ForeArm") || p.bone.name.EndsWith(":LeftLeg") || p.bone.name.EndsWith(":RightLeg"))
            {
                var hingeJoint=p.body.gameObject.AddComponent<HingeJoint>();
                p.joint=hingeJoint;
                hingeJoint.connectedBody=parts[p.parent].body;
                hingeJoint.anchor=Vector3.zero;
                // The elbow is a hinge, not a ball socket. Its bend axis is the
                // normal of the upper-arm/forearm plane at release, mirrored
                // naturally for the two arms. Twist limits are relative to that
                // release angle so the elbow cannot fold backward or sideways.
                Vector3 upperDirection = p.bone.position - parts[p.parent].bone.position;
                Vector3 foreDirection = p.end.position - p.bone.position;
                Vector3 hinge = Vector3.Cross(upperDirection, foreDirection);
                if (hinge.sqrMagnitude < 1e-8f)
                    hinge = Vector3.Cross(foreDirection, controllerBody.transform.up);
                if (hinge.sqrMagnitude < 1e-8f)
                    hinge = Vector3.Cross(foreDirection, controllerBody.transform.right);
                float bend = Vector3.Angle(upperDirection, foreDirection);
                // Nearly straight knees have no reliable cross-product plane.
                // Use the character's left/right axis for their anatomical hinge.
                if (!p.bone.name.EndsWith("ForeArm"))
                {
                    var rig=pose[0];
                    hinge=rig.right;
                    bend=Vector3.SignedAngle(upperDirection,foreDirection,hinge);
                }
                hingeJoint.axis=p.body.transform.InverseTransformDirection(hinge.normalized);
                float minimumBend=p.bone.name.EndsWith("ForeArm") ? 3f : 8f;
                hingeJoint.limits=new JointLimits {min=minimumBend-bend,max=130f-bend,contactDistance=10f};
                hingeJoint.useLimits=true;
                continue;
            }
            var joint = p.body.gameObject.AddComponent<CharacterJoint>();
            p.joint = joint;
            joint.connectedBody = parts[p.parent].body;
            joint.anchor = Vector3.zero;
            joint.axis = Vector3.up; joint.swingAxis = Vector3.right;
            joint.lowTwistLimit = new SoftJointLimit { limit=-20f };
            joint.highTwistLimit = new SoftJointLimit { limit=20f };
            joint.swing1Limit = new SoftJointLimit { limit=p.swing };
            joint.swing2Limit = new SoftJointLimit { limit=p.swing > 65 ? 15f : p.swing };
            if (p.bone.name.EndsWith("Hand"))
            {
                joint.lowTwistLimit = new SoftJointLimit { limit=-10f };
                joint.highTwistLimit = new SoftJointLimit { limit=10f };
                joint.swing1Limit = new SoftJointLimit { limit=15f };
                joint.swing2Limit = new SoftJointLimit { limit=10f };
            }
            else if (p.bone.name.EndsWith(":LeftArm") || p.bone.name.EndsWith(":RightArm"))
            {
                joint.swing1Limit = new SoftJointLimit { limit=65f };
                joint.swing2Limit = new SoftJointLimit { limit=65f };
            }
            else if(p.bone.name.EndsWith("UpLeg"))
            {
                // More hip flexion than lateral opening: a symmetric cone
                // lets both thighs spread into a split on landing.
                joint.swingAxis=p.body.transform.InverseTransformDirection(pose[0].right);
                joint.lowTwistLimit=new SoftJointLimit {limit=-15f};
                joint.highTwistLimit=new SoftJointLimit {limit=15f};
                joint.swing1Limit=new SoftJointLimit {limit=65f};
                joint.swing2Limit=new SoftJointLimit {limit=25f};
            }
            else if(p.bone.name.EndsWith("Foot"))
            {
                joint.lowTwistLimit=new SoftJointLimit {limit=-10f};
                joint.highTwistLimit=new SoftJointLimit {limit=10f};
                joint.swing1Limit=new SoftJointLimit {limit=25f};
                joint.swing2Limit=new SoftJointLimit {limit=10f};
            }
            joint.enableProjection = true;
            joint.projectionDistance = .035f; joint.projectionAngle = 15f;
        }
        parts[0].body.constraints = RigidbodyConstraints.FreezePositionZ;
        controllerBody.isKinematic = true;
        controllerBody.detectCollisions = false;
        Physics.SyncTransforms();
        foreach (var p in parts)
        {
            p.body.isKinematic = false;
            p.body.linearVelocity = velocity + Vector3.Cross(angularVelocity, p.body.worldCenterOfMass - controllerBody.position);
            p.body.angularVelocity = angularVelocity;
        }
        Active = true;
    }

    internal void NotifySupport()
    {
        supportTime = .12f;
        if (Active && settleOnContact)
            foreach (var p in parts)
                if (!p.body.IsSleeping() && p.body.angularVelocity.sqrMagnitude > 9f)
                    p.body.angularVelocity = Vector3.ClampMagnitude(p.body.angularVelocity, 3f);
        if (!Active || !settleOnContact || groundSolverEnabled) return;
        groundSolverEnabled = true;
        // Once landing begins, non-adjacent limbs must not pass through the
        // torso or each other. Adjacent capsules overlap by design at joints.
        int selfLayer=1 << 2;
        for (int i=0;i<parts.Count;i++)
        {
            parts[i].collider.includeLayers |= selfLayer;
            parts[i].collider.excludeLayers &= ~selfLayer;
            for (int j=0;j<i;j++)
                if(parts[i].parent!=j && parts[j].parent!=i)
                    Physics.IgnoreCollision(parts[i].collider,parts[j].collider,false);
        }
        // The air-only depth lock conflicts with 3D floor contacts. Projection
        // then teleports joint frames against those contacts and injects energy.
        // Let the pelvis settle in depth. Solve the constraints iteratively;
        // projection at the floor is the source of violent snapping.
        // Keep this configuration through small bounces to avoid re-locking it.
        foreach (var p in parts)
        {
            p.body.constraints = RigidbodyConstraints.None;
            p.body.solverIterations = 64;
            p.body.solverVelocityIterations = 24;
            if (p.joint is CharacterJoint characterJoint)
            {
                characterJoint.enableProjection = true;
                characterJoint.projectionAngle = 180f;
                characterJoint.projectionDistance = .025f;
            }
            if(p.joint!=null) p.joint.enablePreprocessing=false;
        }
    }

    void FixedUpdate() => StepContactDamping(Time.fixedDeltaTime);

    // Also called by the isolated-physics validation before each simulation step.
    public void StepContactDamping(float deltaTime)
    {
        if (!Active) return;
        // Sleeping bodies receive no OnCollisionStay; do not wake them by
        // repeatedly changing damping after the whole ragdoll has settled.
        bool sleeping = true;
        foreach (var p in parts) sleeping &= p.body.IsSleeping();
        if (sleeping) return;
        bool supported = settleOnContact && supportTime > 0f;
        supportTime = Mathf.Max(0f, supportTime - deltaTime);
        SupportBlend = Mathf.MoveTowards(SupportBlend, supported ? 1f : 0f,
            deltaTime / (supported ? .16f : .1f));
        foreach (var p in parts)
        {
            p.body.angularDamping = Mathf.Lerp(.6f, 20f, SupportBlend);
        }
    }

    public void ApplyPose()
    {
        if (!Active) return;
        // Parent-first order prevents parent updates from moving an already solved child.
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            // Joint solver error must not stretch the skinned skeleton on impact.
            // Only the root translates; articulated children keep their length.
            if (i == 0) p.bone.position = p.body.position;
            else p.bone.localPosition = p.entryLocalPosition;
            p.bone.rotation = p.body.rotation * p.boneOffset;
        }
        // IK normally distributes roll through these skin-only bones. It is
        // disabled during a fall, so leaving its last helper rotations in place
        // twists the mesh even when the physical elbow is within its limits.
        foreach (var p in parts)
        {
            if (p.skinTwist == null) continue;
            Vector3 axis = (p.end.position-p.bone.position).normalized;
            Quaternion rest = p.bone.parent.rotation*p.restRotation;
            Quaternion neutral = Quaternion.FromToRotation(rest*p.restAim,axis)*rest;
            Vector3 reference = Vector3.Cross(p.restAim,Vector3.up);
            if(reference.sqrMagnitude<.01f) reference=Vector3.Cross(p.restAim,Vector3.right);
            float roll=Vector3.SignedAngle(Vector3.ProjectOnPlane(neutral*reference,axis),
                Vector3.ProjectOnPlane(p.bone.rotation*reference,axis),axis);
            p.skinTwist.Apply(neutral,axis,roll);
        }
        controllerBody.position = TrackingPosition;
    }

    public void Restore()
    {
        if (!Active) return;
        Active = false;
        supportTime = 0f;
        SupportBlend = 0f;
        physicsRoot.SetActive(false);
        if (Application.isPlaying) Destroy(physicsRoot);
        else DestroyImmediate(physicsRoot);
        for (int i = 0; i < pose.Length; i++)
            if (pose[i] != null) { pose[i].localPosition=positions[i]; pose[i].localRotation=rotations[i]; }
        controllerBody.isKinematic = previousKinematic;
        controllerBody.detectCollisions = previousCollisions;
        controllerBody.linearVelocity = Vector3.zero;
        controllerBody.angularVelocity = Vector3.zero;
    }
}
