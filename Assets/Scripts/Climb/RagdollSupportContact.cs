using UnityEngine;

/// <summary>Only upward-facing solid contacts count as support, not wall scrapes.</summary>
public sealed class RagdollSupportContact : MonoBehaviour
{
    [System.NonSerialized] public ClimbRagdoll owner;
    void OnCollisionEnter(Collision collision) => Report(collision);
    void OnCollisionStay(Collision collision) => Report(collision);
    void Report(Collision collision)
    {
        if (owner == null || !owner.Active) return;
        var other=collision.collider.GetComponent<RagdollSupportContact>();
        if(other!=null && other.owner==owner) return;
        for (int i = 0; i < collision.contactCount; i++)
            if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) > .45f)
            {
                owner.NotifySupport();
                return;
            }
    }
}
