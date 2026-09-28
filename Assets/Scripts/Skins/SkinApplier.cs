using UnityEngine;

/// <summary>Restores the shared body, including saves made with retired skins.</summary>
public class SkinApplier : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer[] defaultRenderers;
    private void Start() => ApplySkin(null);
    public void ApplySkin(SkinDefinition skin)
    {
        if (defaultRenderers != null)
            foreach (var renderer in defaultRenderers)
                if (renderer != null) renderer.enabled = true;
        var legacyMesh = transform.Find("_SkinMesh");
        if (legacyMesh != null)
        {
            legacyMesh.gameObject.SetActive(false);
            Destroy(legacyMesh.gameObject);
        }
        var shared = SkinDatabase.Instance != null ? SkinDatabase.Instance.GetAt(0) : null;
        if (shared != null && SaveSystem.SelectedSkinId != shared.skinId)
            SaveSystem.SelectedSkinId = shared.skinId;
    }
}
