using UnityEngine;

/// <summary>
/// VFX менеджер. Все эффекты создаются кодом.
/// Генерирует круглую текстуру для частиц — никаких квадратов.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    // Кэшируем материалы чтобы не создавать на каждый эффект
    private Material _matAdditive;    // для искр (coin, star) — даёт свечение
    private Material _matAlpha;       // для конфетти — обычная прозрачность

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        var circleTex = CreateCircleTexture(64);
        _matAdditive  = CreateMaterial("Legacy Shaders/Particles/Additive", circleTex);
        _matAlpha     = CreateMaterial("Sprites/Default",                    circleTex);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ──────────────────────────────────────────────────────────
    public void PlayJointBreakVFX(Vector3 pos)
    {
        // Резкий белый взрыв — "snap!" момент разрыва суставов
        var ps = MakeBurst("JointBreakVFX", pos,
            count: 22, speedMin: 4f, speedMax: 10f,
            sizeMin: 0.10f, sizeMax: 0.25f,
            lifetime: 0.5f, gravity: 0.2f, radius: 0.15f,
            material: _matAdditive);

        SetColor(ps, new Color(1f, 0.9f, 0.7f), new Color(1f, 0.4f, 0.1f)); // белый → оранжевый
        ps.Play();
        Destroy(ps.gameObject, 1.5f);
    }

    public void PlayCoinVFX(Vector3 pos)
    {
        var ps = MakeBurst("CoinVFX", pos,
            count: 20, speedMin: 2f, speedMax: 5f,
            sizeMin: 0.08f, sizeMax: 0.18f,
            lifetime: 0.7f, gravity: 1.5f, radius: 0.2f,
            material: _matAdditive);

        SetColor(ps, new Color(1f, 0.8f, 0f), new Color(1f, 0.4f, 0f));
        ps.Play();
        Destroy(ps.gameObject, 2f);
    }

    public void PlayStarVFX(Vector3 pos)
    {
        var ps = MakeBurst("StarVFX", pos,
            count: 30, speedMin: 3f, speedMax: 9f,
            sizeMin: 0.12f, sizeMax: 0.30f,
            lifetime: 0.8f, gravity: 0.3f, radius: 0.35f,
            material: _matAdditive);

        SetColor(ps, new Color(1f, 1f, 0.5f), new Color(1f, 0.8f, 0f));

        // Частицы немного вращаются
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        rot.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        ps.Play();
        Destroy(ps.gameObject, 2.5f);
    }

    public void PlayWinVFX()
    {
        // Canvas поверх ВСЕГО UI — UI Images всегда рендерятся правильно
        var canvasGO           = new GameObject("WinVFXCanvas");
        var canvas             = canvasGO.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode      = UnityEngine.RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder    = 999;

        var scaler             = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode     = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;

        StartCoroutine(SpawnConfettiUI(canvasGO.transform));
        Destroy(canvasGO, 7f);
    }

    // ─── Builders ────────────────────────────────────────────────────────────
    private ParticleSystem MakeBurst(string name, Vector3 pos,
        int count, float speedMin, float speedMax,
        float sizeMin, float sizeMax, float lifetime,
        float gravity, float radius, Material material)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main             = ps.main;
        main.duration        = 0.2f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh      = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = radius;

        // Плавное затухание
        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var g       = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r      = ps.GetComponent<ParticleSystemRenderer>();
        r.material = material;

        return ps;
    }

    // ─── UI Confetti ─────────────────────────────────────────────────────────
    private static readonly Color[] ConfettiColors =
    {
        new Color(1f, 0.25f, 0.35f),  // розовый
        new Color(0.3f, 0.75f, 1f),   // голубой
        new Color(1f, 0.85f, 0.1f),   // жёлтый
        new Color(0.45f, 1f, 0.35f),  // зелёный
        new Color(1f, 0.55f, 0.1f),   // оранжевый
        new Color(0.85f, 0.3f, 1f),   // фиолетовый
        new Color(1f, 1f, 1f),        // белый
    };

    private System.Collections.IEnumerator SpawnConfettiUI(Transform canvasRoot)
    {
        int total = 90; // количество конфетти
        for (int i = 0; i < total; i++)
        {
            SpawnOneConfetti(canvasRoot);
            yield return new WaitForSecondsRealtime(0.04f); // стаггер 40мс
        }
    }

    private void SpawnOneConfetti(Transform parent)
    {
        var go = new GameObject("C", typeof(UnityEngine.RectTransform));
        go.transform.SetParent(parent, false);

        var rt         = go.GetComponent<UnityEngine.RectTransform>();
        float w        = Random.Range(8f, 18f);
        float h        = Random.Range(6f, 14f);
        rt.sizeDelta   = new Vector2(w, h);
        rt.anchorMin   = rt.anchorMax = Vector2.zero;
        // Стартуют выше экрана в случайной X позиции
        rt.anchoredPosition = new Vector2(
            Random.Range(0f, Screen.width),
            Screen.height + Random.Range(10f, 120f));

        var img        = go.AddComponent<UnityEngine.UI.Image>();
        img.color      = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
        img.raycastTarget = false;

        float speed    = Random.Range(350f, 700f);
        float rotSpeed = Random.Range(-200f, 200f);
        float drift    = Random.Range(-60f, 60f);

        StartCoroutine(AnimateConfetti(rt, speed, rotSpeed, drift));
    }

    private static System.Collections.IEnumerator AnimateConfetti(
        UnityEngine.RectTransform rt, float speed, float rotSpeed, float drift)
    {
        while (rt != null)
        {
            float dt = Time.unscaledDeltaTime;
            rt.anchoredPosition += new Vector2(drift * dt, -speed * dt);
            rt.Rotate(0f, 0f, rotSpeed * dt);
            speed  += 80f * dt; // ускорение от "гравитации"
            drift  *= (1f - dt * 0.8f); // дрейф затухает

            if (rt.anchoredPosition.y < -80f) break;
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private static void SetColor(ParticleSystem ps, Color from, Color to)
    {
        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var g       = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 0.5f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    // ─── Texture ─────────────────────────────────────────────────────────────
    /// <summary>Круглая мягкая текстура — вместо квадратных дефолтных частиц.</summary>
    private static Texture2D CreateCircleTexture(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                                               new Vector2(half, half));
                float t     = Mathf.Clamp01(dist / half);
                // Плавный спад от центра к краям
                float alpha = Mathf.Pow(1f - t, 1.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static Material CreateMaterial(string shaderName, Texture2D tex)
    {
        var shader = Shader.Find(shaderName);
        // Фолбэк если шейдер не найден
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        var mat        = new Material(shader);
        mat.mainTexture = tex;
        return mat;
    }
}
