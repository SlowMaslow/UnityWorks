using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// ⭐ ЗАМЕР ПРОХОЖДЕНИЯ: пишет в файл, когда игрок перехватывается, жмёт кнопки и когда открываются
/// и закрываются окна платформ.
///
/// Зачем. В модели проходимости ровно одно число стоит ПРИКИДКОЙ, а не замером — сколько перехватов
/// игрок успевает, пока платформа держится (<c>LevelModel.MoveBudget</c>). Дотяжку в своё время
/// откалибровали замером на 28 станциях, и она с тех пор не врёт; окно так и не мерили. Из этого
/// лога число считается напрямую: сколько «grip» попало между «window_open» и «window_close».
///
/// ⚠️ Только наблюдает. Ни одной подписки на изменение состояния, ни одного вызова в игру — если
/// логгер выключить, поведение не изменится ни на кадр.
///
/// Куда пишет: <c>Logs/ClimbTelemetry.csv</c> в корне проекта. ⚠️ Именно Logs, а не корень: эта папка
/// уже в .gitignore, поэтому замеры не полезут в коммиты и не будут мозолить глаза в git status.
/// Файл ДОПИСЫВАЕТСЯ, каждый запуск отделён строкой «# run». Формат: время;событие;деталь;x;y.
/// </summary>
public class ClimbTelemetry : MonoBehaviour
{
    /// <summary>Выключатель на случай, если лог начнёт мешать: false — не создаём объект вовсе.</summary>
    public static bool Enabled = true;

    private static ClimbTelemetry _instance;
    private StringBuilder _buf = new StringBuilder();
    private string _path;
    private float _t0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Enabled || _instance != null) return;
        var go = new GameObject("~ClimbTelemetry") { hideFlags = HideFlags.HideAndDontSave };
        _instance = go.AddComponent<ClimbTelemetry>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        string dir = System.IO.Path.GetFullPath(Application.dataPath + "/../Logs");
        try { if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir); }
        catch (System.Exception e) { Debug.LogWarning("[Телеметрия] нет папки Logs: " + e.Message); }
        _path = System.IO.Path.Combine(dir, "ClimbTelemetry.csv");
        _t0 = Time.time;
        Line("# run", Application.isEditor ? "editor" : "build", 0f, 0f);
    }

    private void OnEnable()
    {
        ClimbController.PadGripped += OnGrip;
        TriggerTile.Pressed += OnPress;
        DisappearingPlatform.GroupStateChanged += OnWindow;
    }

    private void OnDisable()
    {
        ClimbController.PadGripped -= OnGrip;
        TriggerTile.Pressed -= OnPress;
        DisappearingPlatform.GroupStateChanged -= OnWindow;
        Flush();
    }

    private void OnApplicationQuit() { Flush(); }

    private void OnGrip(int pad, Vector3 p)   { Line("grip", "pad" + pad, p.x, p.y); }
    private void OnPress(string id, Vector3 p) { Line("press", id, p.x, p.y); }

    /// <summary>Группы, окно которых реально открывали. ⚠️ Нужны, чтобы отсеять ЛОЖНЫЕ закрытия:
    /// при загрузке уровня каждая платформа рапортует своё состояние покоя, и лог захлёбывался
    /// пачками «window_close» ещё до первого хода игрока — а по ним считается длина окна.</summary>
    private readonly System.Collections.Generic.HashSet<string> _opened =
        new System.Collections.Generic.HashSet<string>();

    private void OnWindow(string id, bool active)
    {
        if (active) { _opened.Add(id); Line("window_open", id, 0f, 0f); }
        else if (_opened.Remove(id)) Line("window_close", id, 0f, 0f);
    }

    private void Line(string ev, string detail, float x, float y)
    {
        _buf.Append((Time.time - _t0).ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
            .Append(ev).Append(';').Append(detail).Append(';')
            .Append(x.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
            .Append(y.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
        // ⚠️ Пишем на диск не каждую строку, а пачками: перехваты идут густо, и файловый ввод-вывод
        // в кадре — верный способ испортить ровно тот тайминг, который мы замеряем.
        if (_buf.Length > 4000) Flush();
    }

    private void Flush()
    {
        if (_buf.Length == 0 || string.IsNullOrEmpty(_path)) return;
        try { System.IO.File.AppendAllText(_path, _buf.ToString()); }
        catch (System.Exception e) { Debug.LogWarning("[Телеметрия] не записалось: " + e.Message); }
        _buf.Length = 0;
    }
}
