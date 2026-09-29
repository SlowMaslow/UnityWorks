using UnityEngine;

/// <summary>Skin-only forearm sections. Does not change IK joints or grip transforms.</summary>
[DisallowMultipleComponent]
public sealed class ForearmTwistRig : MonoBehaviour
{
    public Transform[] sections = new Transform[3];

    public void Apply(Quaternion elbowRotation, Vector3 forearmAxis, float twist)
    {
        for (int i = 0; i < sections.Length; i++)
            if (sections[i] != null)
                sections[i].rotation = Quaternion.AngleAxis(twist * i / 3f, forearmAxis) * elbowRotation;
    }
}
