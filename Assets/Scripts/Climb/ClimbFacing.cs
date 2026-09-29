using UnityEngine;

/// <summary>Hysteretic facing choice from anatomical hand order in the body's plane.</summary>
public sealed class ClimbFacing
{
    public float Yaw { get; private set; }
    public bool Reversed { get; private set; }
    private float pendingTime;

    public void Reset()
    {
        Yaw = 0f;
        Reversed = false;
        pendingTime = 0f;
    }

    public void Step(float handSeparation, bool hasTwoSupports, float threshold,
        float holdTime, float speed, float deltaTime)
    {
        bool wantsChange = hasTwoSupports && (Reversed
            ? handSeparation > threshold : handSeparation < -threshold);
        pendingTime = wantsChange ? pendingTime + deltaTime : 0f;
        if (wantsChange && pendingTime >= holdTime)
        {
            Reversed = !Reversed;
            pendingTime = 0f;
        }
        // Use one explicit half-turn path; Quaternion's 180-degree tie cannot flip it.
        Yaw = Mathf.MoveTowards(Yaw, Reversed ? 180f : 0f, Mathf.Max(0f, speed) * deltaTime);
    }
}
