using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ ОПИСАНИЕ УРОВНЯ — ЕДИНСТВЕННЫЙ ИСТОЧНИК ИСТИНЫ для модели проходимости.
/// Сюда приводится и загруженный уровень (из иерархии GameObject), и то, что породил генератор.
///
/// Заводится взамен ASCII-схемы (решение игрока 2026-09-01): генератор перестаёт быть черновиком и
/// становится продуктовой фичей (дейли-уровни), а текстовая сетка это не тянет — алфавит кончился
/// (`a-z` тайлы групп, `A-Z` кнопки), свойства группы (inverted/окно) в клетку не помещаются,
/// связь «кнопка внутри группы» это иерархия, а не сетка, и подклеточные позиции теряются.
/// ASCII остаётся только отрисовкой для глаз — заведомо неполной.
/// </summary>
public class LevelSpec
{
    public float cell = 0.5f;
    public HashSet<Vector2Int> rock = new HashSet<Vector2Int>();
    public List<LevelGroup> groups = new List<LevelGroup>();
    public Vector2Int spawn;
    /// <summary>Спавн задан. Отдельный флаг, потому что клетка (0,0) — законная позиция.</summary>
    public bool hasSpawn;
    public LevelTarget finish = new LevelTarget();
    public List<LevelTarget> artifacts = new List<LevelTarget>();
    public List<LevelTarget> checkpoints = new List<LevelTarget>();
    /// <summary>Монеты: модели проходимости они не нужны, но уровень из описания собирается целиком,
    /// и терять их на полпути незачем.</summary>
    public List<Vector2Int> coins = new List<Vector2Int>();

    public int IndexOfGroup(string id)
    {
        for (int i = 0; i < groups.Count; i++) if (groups[i].id == id) return i;
        return -1;
    }

    public LevelGroup GetOrAddGroup(string id, bool inverted)
    {
        int i = IndexOfGroup(id);
        if (i >= 0) return groups[i];
        var g = new LevelGroup { id = id, inverted = inverted };
        groups.Add(g); return g;
    }

    /// <summary>
    /// ⭐ Копия уровня, где механизм группы <paramref name="gi"/> НЕ РАБОТАЕТ. Нужна для критерия
    /// «каждый механизм НЕСУЩИЙ»: если после этого все цели по-прежнему достижимы, механизм
    /// декоративный — его можно обойти, и головоломки он не создаёт.
    ///
    /// Что значит «не работает», зависит от вида группы:
    ///   • ОБЫЧНАЯ (платформы появляются по кнопке) — платформ не будет никогда: клетки просто пусты;
    ///   • ИНВЕРСНАЯ (стена, которая открывается) — стена не откроется никогда: клетки становятся
    ///     ВЕЧНЫМ КАМНЕМ.
    /// </summary>
    public LevelSpec WithoutGroup(int gi)
    {
        var copy = new LevelSpec { cell = cell, spawn = spawn, finish = finish };
        copy.artifacts.AddRange(artifacts);
        copy.checkpoints.AddRange(checkpoints);
        copy.coins.AddRange(coins);
        foreach (var k in rock) copy.rock.Add(k);
        if (groups[gi].inverted) foreach (var k in groups[gi].tiles) copy.rock.Add(k);

        // ⚠️ Индексы групп после удаления СДВИГАЮТСЯ — ссылки кнопок на хозяина надо перенумеровать,
        // иначе вложенная кнопка начнёт зависеть не от той группы.
        var map = new int[groups.Count];
        int next = 0;
        for (int i = 0; i < groups.Count; i++) map[i] = (i == gi) ? -1 : next++;
        for (int i = 0; i < groups.Count; i++)
        {
            if (i == gi) continue;
            var src = groups[i];
            var g = new LevelGroup { id = src.id, inverted = src.inverted, window = src.window };
            g.tiles.AddRange(src.tiles);
            foreach (var b in src.buttons)
                g.buttons.Add(new LevelButton
                {
                    cell = b.cell, center = b.center, half = b.half, mount = b.mount,
                    // Кнопка внутри УДАЛЁННОЙ группы недоступна навсегда — не «без хозяина»!
                    host = b.host < 0 ? b.host : (map[b.host] < 0 ? LevelButton.HostGone : map[b.host])
                });
            copy.groups.Add(g);
        }
        return copy;
    }
}

/// <summary>Группа исчезающих платформ: тайлы, свойства и её кнопки.</summary>
public class LevelGroup
{
    public string id = "A";
    /// <summary>Тайлы твёрдые ПОКА кнопку не нажали (стена, которая открывается).</summary>
    public bool inverted;
    /// <summary>Сколько секунд группа активна после нажатия (DisappearingPlatform.activeWindow).
    /// В сетке символов этого нет вовсе — ещё одна причина, по которой схема не источник истины.</summary>
    public float window = 5f;

    /// <summary>
    /// ⭐⭐ КЛАПАН ВОЗВРАТА: дверь, которой игрок ВЫХОДИТ из ветки обратно на маршрут. Кнопка у неё
    /// только изнутри — снаружи этим путём не войти.
    ///
    /// Зачем отдельный признак. Критерий «механизм несущий» спрашивает: убери группу — пропала ли
    /// хоть одна цель? У клапана не пропадает НИЧЕГО: убери его, и проход просто всегда открыт,
    /// цели достижимы тем более. По этому критерию он вечно «холостой» — а работа у него другая:
    /// не пускать игрока в ветку с обратной стороны и не давать ему застрять внутри.
    /// ⚠️ Что мы этим НЕ проверяем: что клапан не открывает лишнего. Это гарантируется построением —
    /// кнопка ставится только со стороны ветки (InvertedDoorModule, exitOnly).
    /// </summary>
    public bool returnValve;
    public List<Vector2Int> tiles = new List<Vector2Int>();
    public List<LevelButton> buttons = new List<LevelButton>();
}

/// <summary>
/// ⭐ К ЧЕМУ КНОПКА ПРИКРЕПЛЕНА. Свойство самостоятельное, и путать его с вложенностью нельзя:
/// «на чём держится» и «внутри какой группы лежит» — две разные оси (поправлено игроком).
/// 🐞 До этого крепления не существовало вовсе: импорт ставил ЛЮБУЮ кнопку через SurfaceBelow —
/// «съезжай вниз до первой твёрдой клетки и встань сверху». Кнопка, нарисованная под потолком
/// (так висит группа B в ручном Level_11), уезжала на три ряда вниз и садилась на исчезающую
/// платформу — то есть повисала в воздухе, стоило той пропасть. Модель при этом считала геометрию
/// по схеме и расхождения не видела.
/// </summary>
public enum MountSide { Floor, Ceiling, WallLeft, WallRight }

/// <summary>Кнопка группы. <see cref="host"/> — индекс группы, ВНУТРИ которой она лежит (или -1):
/// пока хозяин в превью, кнопка полупрозрачна и без коллайдера, нажать её нельзя.</summary>
public class LevelButton
{
    /// <summary>Хозяина нет — кнопка доступна всегда.</summary>
    public const int NoHost = -1;
    /// <summary>Хозяин УДАЛЁН (проверка «механизм несущий») — кнопка недоступна НИКОГДА.
    /// ⚠️ Отдельное значение нужно именно потому, что «нет хозяина» и «хозяин исчез» — противоположности,
    /// и спутать их значит сделать заведомо запертую кнопку вечно доступной.</summary>
    public const int HostGone = -2;

    public Vector2Int cell;
    public Vector2 center, half;   // габарит коллайдера в ДРОБНЫХ клетках
    public int host = NoHost;
    /// <summary>К какой поверхности прижата. Пол — как было; потолок и стены появились потому, что
    /// в ручных уровнях кнопки висят и так тоже.</summary>
    public MountSide mount = MountSide.Floor;
}

/// <summary>Цель касания: ключ, флаг, чекпоинт. Габарит берётся из коллайдера — подбор это
/// перекрытие с триггером, а не попадание в точку пивота.</summary>
public class LevelTarget
{
    public bool exists;
    public Vector2Int cell;
    public Vector2 center, half;
    public Vector3 world;
}

/// <summary>
/// ⭐ МОДЕЛЬ ПРОХОДИМОСТИ — ЕДИНСТВЕННОЕ МЕСТО, ГДЕ ЖИВУТ ПРАВИЛА. Раньше поиск по состояниям был
/// написан ДВАЖДЫ (маршрут по уровню и проверка ASCII-схем) — для черновиков это стоило времени,
/// для автономного генератора стоило бы непроходимого уровня у игрока на экране.
///
/// Правила (все ИЗМЕРЕНЫ на калибровочных уровнях, детали — в комментариях к методам):
///   • дотяжка — восьмиугольник вбок ≤6, вверх ≤3, сумма ≤7, симметрично во все стороны;
///   • путь до цели укладывается в бюджет: крюк съедает дотяжку;
///   • состояние = (позиция, маска групп); нажатие и ИСТЕЧЕНИЕ ОКНА — такие же ходы, как шаг;
///   • падение бывает только из-под исчезнувшей платформы.
/// </summary>
public class LevelModel
{
    public const int ReachSumCells = 7;

    public readonly LevelSpec Spec;
    public readonly int RS, RU;

    public List<Vector2Int> Cells;      // индексация состояний: индекс → клетка
    public int N, G, MASKS, TOTAL;

    /// <summary>
    /// ⭐⭐ СОБРАННЫЕ КЛЮЧИ — ЧАСТЬ СОСТОЯНИЯ, а не отдельный вопрос.
    ///
    /// 🐞 Раньше приёмка спрашивала про каждый ключ отдельно: «достижим ли он хоть из какого-нибудь
    /// посещённого состояния». На уровне с ОДНОСТОРОННИМ потоком это враньё: ключ 1 берётся на одном
    /// маршруте, ключ 2 на другом, а вместе — никогда. С добавлением времени стало ещё хуже: «достать
    /// можно» и «успеть вернуться» это разные вопросы, а спрашивали только первый (разбор Level_11
    /// с игроком: ключ забрать успеешь, а вот обратно — вряд ли).
    ///
    /// Теперь ключи едут битами В ТОЙ ЖЕ оси, что и маска групп: combined = (ключи &lt;&lt; G) | группы.
    /// Состояний становится в 2^K раз больше (K = число ключей, у нас 3 → ×8) — дёшево, а вопрос
    /// становится честным: «есть ли состояние, где собрано ВСЁ и достаётся финиш».
    /// ⚠️ Ключ подбирается АВТОМАТИЧЕСКИ, как только до него можно дотянуться: пропустить его игроку
    /// незачем, а состояний это экономит вдвое на каждом ключе.
    /// </summary>
    public int K, MASKS_ALL;

    /// Маска ГРУПП из индекса состояния (без битов ключей).
    public int GroupMask(int st) => (st / N) & (MASKS - 1);
    /// Маска собранных КЛЮЧЕЙ из индекса состояния.
    public int KeyMask(int st) => (st / N) >> G;
    public bool[] Seen;
    public int[] Prev; public byte[] PrevKind; public int[] Depth;   // 0 = шаг, 1 = нажатие, 2 = окно истекло
    public Vector2Int SpawnCell;        // опора под спавном (уже разрешённая)
    public bool TooManyGroups;

    private readonly Dictionary<int, HashSet<Vector2Int>> _solidCache = new Dictionary<int, HashSet<Vector2Int>>();
    private readonly Dictionary<Vector2Int, int> _cellIdx = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<long, bool> _stepCache = new Dictionary<long, bool>();
    private int _minY;
    /// <summary>
    /// ⭐⭐ СОСЕДИ ПО ДОТЯЖКЕ, посчитанные ОДИН РАЗ. Для каждой клетки — индексы тех, что вообще
    /// лежат в восьмиугольнике (вбок ≤RS, вверх ≤RU, сумма ≤7). От маски это не зависит: маска
    /// решает, ТВЁРДАЯ ли клетка, а не далеко ли она.
    ///
    /// 🐞 Зачем. Поиск и разбор тупиков перебирали ВСЕ клетки уровня на каждое состояние — при 700
    /// клетках и сотнях тысяч состояний это сотни миллионов проверок. Пока тупики считались редко,
    /// цена была незаметна; когда я убрал пропуск и они стали считаться всегда — да ещё внутри
    /// проверки «механизм несущий», то есть на каждую группу, — разбор одной схемы перестал
    /// укладываться в минуту, а Unity подвисал. Окно вместо всего уровня — это ~80 кандидатов
    /// вместо 700.
    /// </summary>
    private int[][] _reachNear;
    private Queue<int> _q;
    private int[] _timeLeft;            // сколько перехватов осталось до конца окна (−1 = не был)

    /// <summary>
    /// ⭐⭐ МОДЕЛЬ ВРЕМЕНИ. Сколько ПЕРЕХВАТОВ игрок успевает сделать, пока открыто окно платформы.
    /// 0 — время выключено, окно живёт вечно (как было).
    ///
    /// Зачем понадобилась. «Окно закончилось» было НЕОБЯЗАТЕЛЬНЫМ ходом: поиск мог им не
    /// пользоваться, поэтому нажатая кнопка держала платформу бесконечно. Из-за этого модель не
    /// видела смысла у половины ручных механик игрока: в Level_11 потолок камеры возвращается через
    /// три секунды, поэтому обратно не вылезти и нужна стена-выход — а модель считала, что однажды
    /// открытый потолок открыт навсегда, и объявляла выход декоративным.
    ///
    /// ⚠️ ЗДЕСЬ ЗАГРУБЛЕНИЕ, И ОНО НАМЕРЕННОЕ: когда истекает окно, гасятся ВСЕ открытые группы
    /// разом, а не каждая по своему таймеру. Точный учёт стоил бы (бюджет+1)^групп состояний вместо
    /// (бюджет+1). Загрубление СТРОЖЕ правды: модель может забраковать проходимый уровень, но не
    /// пропустит непроходимый — для генератора это безопасная сторона.
    /// </summary>
    public int MoveBudget;

    /// Размер оси времени в индексе состояния (1, когда время выключено).
    private int TSlots => MoveBudget > 0 ? MoveBudget + 1 : 1;

    public LevelModel(LevelSpec spec, int reachSide, int reachUp)
    { Spec = spec; RS = reachSide; RU = reachUp; }

    // ─── Правила ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Форма дотяжки — ВОСЬМИУГОЛЬНИК: пределы по осям + срезанные углы. Замерено на калибровочных
    /// уровнях (28 станций, 2026-08-18): вбок ≤6, вверх ≤3, сумма ≤7; сошлось 26 из 26.
    /// Симметрична во все стороны (вверх=вниз, влево=вправо) — уточнение игрока.
    /// ⛔ Прежняя коробка ↑4 ↔4 давала невозможные диагонали вроде (4,4); эллипс резал законные (4,3).
    /// </summary>
    public static bool InReach(int dx, int dy, int rs, int ru)
    {
        int ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
        return ax <= rs && ay <= ru && ax + ay <= ReachSumCells;
    }

    /// <summary>
    /// Проход между двумя СВОБОДНЫМИ клетками. Ограничение одно — БЮДЖЕТ ПУТИ: длина обхода
    /// ≤ ReachSumCells, то есть КРЮК СЪЕДАЕТ ДОТЯЖКУ. Обойти угол на пару клеток можно, протащить
    /// руку вокруг полки — нет: на это уже не хватает вылета.
    /// ⛔ Прежний «коридор» (отклонение от прямой ≤2) удалён: он давал ОДИНАКОВЫЙ допуск на обход и
    /// при вылете в 2 клетки, и при вылете в 6, из-за чего пропускал нырок под стену на Level_06 и
    /// при этом резал законный обход угла на Level_03 с запасом в 0.12 клетки. Оба случая сверены
    /// со скринами игрока.
    /// </summary>
    // ⚠️ Обход укладывается в бюджет ReachSumCells, значит и вся его область — квадрат 15×15 вокруг
    // старта. Держим три готовых массива на неё и метим поколением вместо очистки: раньше на КАЖДЫЙ
    // вызов заводились Dictionary и Queue, а вызывается это по сотне раз на состояние.
    private const int PathSpan = ReachSumCells * 2 + 1;
    private static readonly int[] _ppStamp = new int[PathSpan * PathSpan];
    private static readonly int[] _ppDist  = new int[PathSpan * PathSpan];
    private static readonly int[] _ppQueue = new int[PathSpan * PathSpan];
    private static int _ppGen;

    public static bool PathPossible(System.Func<Vector2Int, bool> solid, Vector2Int from, Vector2Int to)
    {
        if (solid(from) || solid(to)) return false;
        if (from == to) return true;
        // Дальше бюджета пути не дойти при любой форме обхода — считать нечего.
        int mdx = to.x - from.x, mdy = to.y - from.y;
        if (Mathf.Abs(mdx) + Mathf.Abs(mdy) > ReachSumCells) return false;

        int gen = ++_ppGen;
        int head = 0, tail = 0;
        int start = ReachSumCells * PathSpan + ReachSumCells;   // (0,0) в локальных координатах
        _ppStamp[start] = gen; _ppDist[start] = 0; _ppQueue[tail++] = start;
        while (head < tail)
        {
            int cur = _ppQueue[head++];
            int d0 = _ppDist[cur];
            if (d0 >= ReachSumCells) continue;
            int cx = cur % PathSpan - ReachSumCells, cy = cur / PathSpan - ReachSumCells;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 2 ? -1 : k == 3 ? 1 : 0);
                int ny = cy + (k == 0 ? -1 : k == 1 ? 1 : 0);
                if (nx < -ReachSumCells || nx > ReachSumCells || ny < -ReachSumCells || ny > ReachSumCells) continue;
                int ni = (ny + ReachSumCells) * PathSpan + (nx + ReachSumCells);
                if (_ppStamp[ni] == gen) continue;
                _ppStamp[ni] = gen; _ppDist[ni] = d0 + 1;
                var nb = new Vector2Int(from.x + nx, from.y + ny);
                if (solid(nb)) continue;                        // камень: помечен, но не разворачивается
                if (nb == to) return true;
                _ppQueue[tail++] = ni;
            }
        }
        return false;
    }

    /// <summary>Переход ХОЛД→ХОЛД. Позиция стояния = клетка холда + 1 (стоим НАД тайлом).</summary>
    public static bool StepPossible(System.Func<Vector2Int, bool> solid, Vector2Int a, Vector2Int b, int rs, int ru)
    {
        if (!InReach(b.x - a.x, b.y - a.y, rs, ru)) return false;
        return PathPossible(solid, new Vector2Int(a.x, a.y + 1), new Vector2Int(b.x, b.y + 1));
    }

    /// <summary>
    /// ДОТЯНУТЬСЯ ДО ТОЧКИ (ключ, флаг, чекпоинт, КНОПКА) — это НЕ переход на холд: вставать на неё
    /// не надо, достаточно КОСНУТЬСЯ пэдом (Artifact/TriggerTile ловят OnTriggerEnter).
    /// Дотяжка меряется до ближайшей точки ГАБАРИТА цели и в ДРОБНЫХ клетках: позиции ставятся руками
    /// и почти все стоят ровно на границе округления (23 из 30 на наших уровнях).
    /// ⛔ Окрестность 3×3 вокруг цели — ЗАПРЕЩЁННЫЙ ПРИЁМ (обжигались дважды): «любая из 9» это выбор
    /// самого удобного варианта, и он то удлинял дотяжку на клетку, то давал касание СКВОЗЬ ПОЛ.
    /// Цель, попавшую в камень, приводим к ближайшей свободной клетке ОДИН раз (вверх дешевле вбок,
    /// вбок дешевле вниз — объекты стоят НА тайле) и дальше идём обычной проверкой.
    /// </summary>
    public static bool TouchPossible(System.Func<Vector2Int, bool> solid, Vector2Int a,
                                     Vector2Int target, Vector2 centerCells, Vector2 halfCells, int rs, int ru)
    {
        var from = new Vector2Int(a.x, a.y + 1);
        float dfx = Mathf.Max(0f, Mathf.Abs(centerCells.x - from.x) - halfCells.x);
        float dfy = Mathf.Max(0f, Mathf.Abs(centerCells.y - from.y) - halfCells.y);
        const float eps = 1e-3f;
        if (dfx > rs + eps || dfy > ru + eps || dfx + dfy > ReachSumCells + eps) return false;
        var t = target;
        if (solid(t))
        {
            int bestCost = int.MaxValue; bool found = false; Vector2Int best = t;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var c = new Vector2Int(t.x + dx, t.y + dy);
                if (solid(c)) continue;
                int cost = Mathf.Abs(dx) * 3 + (dy == 1 ? 0 : dy == 0 ? 2 : 4);
                if (cost < bestCost) { bestCost = cost; best = c; found = true; }
            }
            if (!found) return false;
            t = best;
        }
        return PathPossible(solid, from, t);
    }

    // ─── Состояния ────────────────────────────────────────────────────────────
    /// <summary>⭐ Бит маски = «ОКНО ГРУППЫ ИДЁТ» (кнопку нажали), а НЕ «тайлы твёрдые»: у обычной
    /// группы это одно и то же, у ИНВЕРСНОЙ — противоположное.</summary>
    public bool TilesSolid(int mask, int g)
        => Spec.groups[g].inverted ? (mask & (1 << g)) == 0 : (mask & (1 << g)) != 0;

    public HashSet<Vector2Int> SolidFor(int mask)
    {
        HashSet<Vector2Int> got;
        if (_solidCache.TryGetValue(mask, out got)) return got;
        var s = new HashSet<Vector2Int>(Spec.rock);
        for (int g = 0; g < G; g++) if (TilesSolid(mask, g)) foreach (var k in Spec.groups[g].tiles) s.Add(k);
        _solidCache[mask] = s; return s;
    }

    // ─── ⭐⭐ БИТОВАЯ СЕТКА: «ТВЁРДАЯ ЛИ КЛЕТКА» ЧЕРЕЗ ИНДЕКС, А НЕ ЧЕРЕЗ ХЕШ ────────────────────
    // Самая горячая операция всей модели — спросить у клетки, камень ли она. Она стояла на
    // HashSet<Vector2Int>, то есть на хешировании структуры, и вызывалась сотни раз на каждое
    // состояние. Замер на 2 млн проверок: HashSet 76 мс против 9 мс у обычного массива — в 8.4 раза.
    // Уровень это сетка ~90×45, клетку можно адресовать как y*W+x, и хеш не нужен вовсе.
    //
    // ⚠️ ПРАВИЛА НЕ МЕНЯЮТСЯ. Меняется ТОЛЬКО способ хранить «камень/не камень»: за границей сетки
    // ответ «не камень», ровно как у HashSet.Contains. SolidFor остаётся как был — им пользуются
    // снаружи (окно редактора), и после этой правки он с горячего пути ушёл.
    private int _bx0, _by0, _bw, _bh;
    private readonly Dictionary<int, bool[]> _solidBits = new Dictionary<int, bool[]>();
    private readonly Dictionary<int, System.Func<Vector2Int, bool>> _solidFn
        = new Dictionary<int, System.Func<Vector2Int, bool>>();
    private bool[] _buttonBits;

    /// <summary>
    /// ⭐⭐ ГРУППЫ, КОТОРЫХ В ЭТОМ ПРОГОНЕ НЕТ (битовая маска). Ровно то же, что
    /// <see cref="LevelSpec.WithoutGroup"/>, но БЕЗ пересборки модели: кнопку такой группы нажать
    /// нельзя, её плиты ведут себя как при ненажатой кнопке (обычная — пусто, инверсная — вечный
    /// камень), её кнопки не мешают пэду, а вложенные в неё кнопки заперты навсегда.
    ///
    /// Зачем: приёмка проверяет «каждый механизм несущий», убирая группы по одной, и раньше на
    /// каждую строила НОВУЮ модель. А тяжёлое в модели — не сам обход, а подготовка: список клеток,
    /// соседи в дотяжке на каждую клетку, битовые сетки на маску, кэш шагов. При девяти группах это
    /// десять полных пересборок подряд. С этой маской подготовка делается ОДИН раз и переиспользуется
    /// всеми прогонами: меняется только то, какие кнопки разрешено нажимать.
    ///
    /// ⚠️ Нумерация групп при этом НЕ сдвигается — в отличие от WithoutGroup, где индексы съезжали и
    /// ссылки вложенных кнопок приходилось перенумеровывать. Здесь ссылаться не на кого: маска
    /// говорит про те же самые индексы.
    /// </summary>
    public int IgnoreGroups;

    private int BitIndex(int x, int y)
    {
        int px = x - _bx0, py = y - _by0;
        return (px < 0 || py < 0 || px >= _bw || py >= _bh) ? -1 : py * _bw + px;
    }
    private static bool At(bool[] bits, int i) => i >= 0 && bits[i];

    // Одноместный кэш последней маски: во внутреннем цикле маска меняется редко, а поиск в словаре
    // на каждого соседа — это хеш там, где хватает сравнения целых.
    private int _lastMask = -1; private bool[] _lastBits;
    private int _lastFnMask = -1; private System.Func<Vector2Int, bool> _lastFnValue;

    private bool[] SolidBits(int mask)
    {
        if (mask == _lastMask) return _lastBits;
        bool[] got;
        if (_solidBits.TryGetValue(mask, out got)) { _lastMask = mask; _lastBits = got; return got; }
        var bits = new bool[_bw * _bh];
        foreach (var k in Spec.rock) { int i = BitIndex(k.x, k.y); if (i >= 0) bits[i] = true; }
        for (int g = 0; g < G; g++)
            if (TilesSolid(mask, g))
                foreach (var k in Spec.groups[g].tiles) { int i = BitIndex(k.x, k.y); if (i >= 0) bits[i] = true; }
        _solidBits[mask] = bits; _lastMask = mask; _lastBits = bits; return bits;
    }

    /// <summary>Предикат «камень» для правил перехода. Замыкание строится ОДИН РАЗ на маску:
    /// раньше `k => solid.Contains(k)` рождалось на каждый вызов CanStep.</summary>
    private System.Func<Vector2Int, bool> SolidFn(int mask)
    {
        if (mask == _lastFnMask) return _lastFnValue;
        System.Func<Vector2Int, bool> f;
        if (_solidFn.TryGetValue(mask, out f)) { _lastFnMask = mask; _lastFnValue = f; return f; }
        var bits = SolidBits(mask);
        int bx = _bx0, by = _by0, bw = _bw, bh = _bh;
        f = k =>
        {
            int px = k.x - bx, py = k.y - by;
            return px >= 0 && py >= 0 && px < bw && py < bh && bits[py * bw + px];
        };
        _solidFn[mask] = f; _lastFnMask = mask; _lastFnValue = f; return f;
    }

    /// <summary>Границы битовой сетки: всё, о чём модель может спросить, плюс запас под бюджет пути
    /// (правила щупают клетки вокруг цели, а не только саму цель).</summary>
    private void BuildBitGrid()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        System.Action<Vector2Int> grow = k =>
        {
            if (k.x < minX) minX = k.x; if (k.x > maxX) maxX = k.x;
            if (k.y < minY) minY = k.y; if (k.y > maxY) maxY = k.y;
        };
        foreach (var k in Spec.rock) grow(k);
        foreach (var g in Spec.groups)
        {
            foreach (var k in g.tiles) grow(k);
            foreach (var b in g.buttons) grow(b.cell);
        }
        foreach (var a in Spec.artifacts) grow(a.cell);
        foreach (var c in Spec.checkpoints) grow(c.cell);
        if (Spec.finish.exists) grow(Spec.finish.cell);
        grow(Spec.spawn);
        if (minX > maxX) { _bx0 = _by0 = 0; _bw = _bh = 1; }
        else
        {
            int pad = ReachSumCells + 2;
            _bx0 = minX - pad; _by0 = minY - pad;
            _bw = maxX - minX + 1 + 2 * pad; _bh = maxY - minY + 1 + 2 * pad;
        }
        _solidBits.Clear(); _solidFn.Clear();
        _lastMask = _lastFnMask = -1; _lastBits = null; _lastFnValue = null;
        RebuildButtonBits();
    }

    /// <summary>Кнопки, занимающие клетку под пэд. ⚠️ Кнопки убранных групп сюда НЕ попадают: вместе
    /// с механизмом исчезает и его кнопка, а значит и помеха. Пересобирается на каждый прогон, потому
    /// что зависит от <see cref="IgnoreGroups"/>; всё остальное (клетки, соседи, сетки, кэш шагов)
    /// от него не зависит и переживает смену маски.</summary>
    private void RebuildButtonBits()
    {
        if (_buttonBits == null || _buttonBits.Length != _bw * _bh) _buttonBits = new bool[_bw * _bh];
        else System.Array.Clear(_buttonBits, 0, _buttonBits.Length);
        for (int g = 0; g < Spec.groups.Count; g++)
        {
            if ((IgnoreGroups & (1 << g)) != 0) continue;
            foreach (var b in Spec.groups[g].buttons)
            { int i = BitIndex(b.cell.x, b.cell.y); if (i >= 0) _buttonBits[i] = true; }
        }
    }

    /// <summary>
    /// ⭐ КНОПКА ЗАНИМАЕТ КЛЕТКУ, В КОТОРУЮ ИГРОК СТАВИТ ПЭД. Холд — это камень, над которым воздух;
    /// но если в этом воздухе сидит кнопка, встать на холд НЕЛЬЗЯ — руке некуда лечь.
    ///
    /// 🐞 Модель этого не знала и считала кнопку только целью, а не препятствием. Генератор спокойно
    /// ставил кнопку на последнюю свободную клетку узкой полки, приёмка говорила «всё хорошо», и
    /// уровень выходил непроходимым (поймано игроком на Level_12: платформа группы A в четыре клетки,
    /// над двумя нависает камень, третью занимает вложенная кнопка B — пролезть негде).
    /// </summary>
    private readonly HashSet<Vector2Int> _buttonCells = new HashSet<Vector2Int>();
    /// Выключатель правила — только для замеров: сравнить поведение модели с ним и без него.
    public static bool ButtonsBlockPad = true;

    public bool IsHold(int mask, Vector2Int k)
    {
        var bits = SolidBits(mask);
        int i = BitIndex(k.x, k.y);
        if (!At(bits, i)) return false;
        int iu = BitIndex(k.x, k.y + 1);
        if (At(bits, iu)) return false;
        return !ButtonsBlockPad || !At(_buttonBits, iu);
    }

    public bool CanStep(int mask, Vector2Int a, Vector2Int b)
    {
        if (!InReach(b.x - a.x, b.y - a.y, RS, RU)) return false;
        long key = ((long)mask << 48) ^ ((long)(a.x + 512) << 36) ^ ((long)(a.y + 512) << 24)
                 ^ ((long)(b.x + 512) << 12) ^ (long)(b.y + 512);
        bool cached;
        if (_stepCache.TryGetValue(key, out cached)) return cached;
        bool ok = StepPossible(SolidFn(mask), a, b, RS, RU);
        _stepCache[key] = ok; return ok;
    }

    public bool CanTouch(int mask, Vector2Int a, Vector2Int target, Vector2 center, Vector2 half)
        => TouchPossible(SolidFn(mask), a, target, center, half, RS, RU);

    public bool CanTouch(int mask, Vector2Int a, LevelTarget t)
        => t.exists && CanTouch(mask, a, t.cell, t.center, t.half);

    public int CellIndex(Vector2Int c) { int i; return _cellIdx.TryGetValue(c, out i) ? i : -1; }

    /// <summary>
    /// Индекс состояния с УЖЕ ПОДОБРАННЫМИ ключами: всё, до чего из этой клетки дотягиваешься,
    /// считается взятым. ⚠️ Подбирать всегда выгодно, отказываться незачем — поэтому не плодим
    /// вариант «прошёл мимо ключа», он бы удвоил состояния на каждом артефакте без всякой пользы.
    /// </summary>
    private int WithKeys(int keys, int groupMask, int ci)
    {
        for (int a = 0; a < K; a++)
        {
            if ((keys & (1 << a)) != 0) continue;
            if (CanTouch(groupMask, Cells[ci], Spec.artifacts[a])) keys |= 1 << a;
        }
        return ((keys << G) | groupMask) * N + ci;
    }

    private int WithKeys(int groupMask, int ci) => WithKeys(0, groupMask, ci);

    /// <summary>⭐ ГЛАВНЫЙ ВОПРОС ПРИЁМКИ: существует ли прохождение, где собраны ВСЕ артефакты и
    /// достигнут финиш. Именно «и», а не «каждый по отдельности достижим».</summary>
    public bool AllKeysAndFinish()
    {
        if (Seen == null || N == 0) return false;
        int fullKeys = (1 << K) - 1;
        for (int st = 0; st < TOTAL; st++)
        {
            if (!Seen[st] || KeyMask(st) != fullKeys) continue;
            if (!Spec.finish.exists) return true;              // финиша нет — довольно ключей
            if (CanTouch(GroupMask(st), Cells[st % N], Spec.finish)) return true;
        }
        return false;
    }

    /// <summary>
    /// Поиск по состояниям (позиция + маска), идёт ДО КОНЦА — и финиш, и ключи берутся из одного
    /// дерева. Ходы: перейти на холд, нажать кнопку, дождаться конца окна (может уронить).
    /// </summary>
    /// <summary>Подготовка уже сделана — второй прогон переиспользует клетки, соседей, битовые сетки
    /// и кэш шагов, а перебирает только состояния.</summary>
    private bool _prepared;
    /// <summary>Индексы состояний, которых прогон коснулся: по ним и только по ним чистится
    /// состояние перед следующим прогоном (см. <see cref="Rerun"/>).</summary>
    private readonly List<int> _touched = new List<int>();

    /// <summary>
    /// ⭐⭐ ДОСТИГНУТЫЕ ПАРЫ (МАСКА ГРУПП, КЛЕТКА) — индексы вида mask*N+ci, без оси ключей.
    ///
    /// Зачем: вопрос «дотянусь ли отсюда до цели» зависит ТОЛЬКО от маски и клетки, а состояний с
    /// одной и той же парой ровно 2^ключей — на трёх артефактах это восемь, на шести шестьдесят
    /// четыре. Приёмка же перебирала все состояния подряд и звала CanTouch на каждое, то есть делала
    /// одну и ту же работу по восемь раз, и так на каждую цель и на каждую убранную группу.
    /// </summary>
    public List<int> ReachedPositions => _reachedPos;
    private readonly List<int> _reachedPos = new List<int>();
    private bool[] _posSeen;

    private void MarkTouched(int st)
    {
        _touched.Add(st);
        int pos = ((st / N) & (MASKS - 1)) * N + st % N;
        if (!_posSeen[pos]) { _posSeen[pos] = true; _reachedPos.Add(pos); }
    }

    /// <summary>
    /// ⭐ ПРОГНАТЬ ЗАНОВО, УБРАВ ГРУППЫ <paramref name="ignoreGroups"/> — без пересборки модели.
    /// Именно этим приёмка проверяет «каждый механизм несущий»: раньше на каждую группу строилась
    /// новая модель, то есть заново считались клетки, соседи в дотяжке, сетки и кэш шагов.
    /// От маски убранных групп зависят только кнопки (какие можно нажать и какие мешают пэду) и
    /// сам обход; вся тяжёлая подготовка от неё не зависит и переживает смену маски.
    /// </summary>
    public void Rerun(int ignoreGroups)
    {
        if (!_prepared || TooManyGroups || N == 0) { IgnoreGroups = ignoreGroups; Search(); return; }
        IgnoreGroups = ignoreGroups;
        RebuildButtonBits();
        // ⚠️ СБРАСЫВАЕМ ТОЛЬКО ТРОНУТОЕ, А НЕ ВЕСЬ МАССИВ. 🐞 Первая версия чистила все TOTAL клеток
        // пяти массивов на каждый прогон — и переиспользование модели вышло МЕДЛЕННЕЕ пересборки
        // (потолок 8: 1311 → 1443 мс на уровень). Причина простая: у общей модели ось масок полная,
        // 2^G, тогда как у отдельной модели без одной группы она вдвое короче. Зато достигается всего
        // 2-4% состояний, и список тронутых на порядки короче самого массива.
        for (int i = 0; i < _touched.Count; i++)
        {
            int x = _touched[i];
            Seen[x] = false; Prev[x] = 0; PrevKind[x] = 0; Depth[x] = 0; _timeLeft[x] = -1;
        }
        _touched.Clear();
        for (int i = 0; i < _reachedPos.Count; i++) _posSeen[_reachedPos[i]] = false;
        _reachedPos.Clear();
        _stuckFlag = null;               // тупики считаются заново под новую маску
        _q.Clear();
        Explore();
    }

    public void Search()
    {
        G = Spec.groups.Count;
        if (G > 12) { TooManyGroups = true; return; }
        MASKS = 1 << G;
        // ⚠️ Ключей в оси ровно столько, сколько артефактов, но не больше шести: 2^K множится на всё
        // остальное, а уровней с семью артефактами у нас нет (прогрессия требует трёх).
        K = Mathf.Min(Spec.artifacts.Count, 6);
        MASKS_ALL = MASKS << K;

        BuildBitGrid();                         // адресация клетки индексом вместо хеша

        // Клетки, твёрдые ХОТЬ ПРИ КАКОЙ-ТО маске. ⚠️ Одной маски MASKS-1 мало: в ней как раз
        // отсутствуют тайлы инверсных групп.
        var ever = new HashSet<Vector2Int>(Spec.rock);
        foreach (var g in Spec.groups) foreach (var k in g.tiles) ever.Add(k);
        _buttonCells.Clear();
        foreach (var g in Spec.groups) foreach (var b in g.buttons) _buttonCells.Add(b.cell);
        Cells = new List<Vector2Int>(ever);
        _cellIdx.Clear();
        for (int i = 0; i < Cells.Count; i++) _cellIdx[Cells[i]] = i;
        // Соседи по геометрии — один раз на уровень.
        _reachNear = new int[Cells.Count][];
        {
            var buf = new List<int>();
            for (int i = 0; i < Cells.Count; i++)
            {
                buf.Clear();
                var a = Cells[i];
                for (int dy = -RU; dy <= RU; dy++)
                for (int dx = -RS; dx <= RS; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!InReach(dx, dy, RS, RU)) continue;
                    int j;
                    if (_cellIdx.TryGetValue(new Vector2Int(a.x + dx, a.y + dy), out j)) buf.Add(j);
                }
                _reachNear[i] = buf.ToArray();
            }
        }
        N = Cells.Count;
        if (N == 0) return;
        _minY = int.MaxValue;
        foreach (var c in Cells) if (c.y < _minY) _minY = c.y;

        _prepared = true;
        _posSeen = new bool[MASKS * N];
        _reachedPos.Clear(); _touched.Clear();
        TOTAL = MASKS_ALL * N;
        Seen = new bool[TOTAL]; Prev = new int[TOTAL]; PrevKind = new byte[TOTAL]; Depth = new int[TOTAL];
        // ⭐ ВРЕМЯ ЕДЕТ ОТДЕЛЬНЫМ МАССИВОМ, А НЕ ТРЕТЬЕЙ ОСЬЮ ИНДЕКСА. Состояние по-прежнему
        // (маска, клетка), но у него есть ЛУЧШИЙ ЗАПАС ХОДОВ, с которым до него удалось дойти:
        // прийти с бо́льшим запасом — строго лучше, поэтому такое состояние переоткрываем.
        // Так вся остальная машинерия (маршруты, тупики, достижимость) осталась нетронутой.
        _timeLeft = new int[TOTAL];
        for (int i = 0; i < TOTAL; i++) _timeLeft[i] = -1;
        _q = new Queue<int>();

        // ⚠️ Не полагаемся на то, что маркер спавна попал ровно в клетку пола: в старых уровнях он
        // стоит вне сетки, и округление давало пустую клетку. Берём ближайшую ОПОРУ под спавном.
        SpawnCell = Spec.spawn;
        {
            var s0 = SolidFor(0);
            bool found = false;
            for (int d = 0; d <= 4 && !found; d++)
            { var cand = new Vector2Int(Spec.spawn.x, Spec.spawn.y - d); if (s0.Contains(cand)) { SpawnCell = cand; found = true; } }
            if (!found)
            {
                float bd = float.MaxValue;
                for (int i = 0; i < N; i++)
                {
                    if (!IsHold(0, Cells[i])) continue;
                    float d2 = (Cells[i] - Spec.spawn).sqrMagnitude;
                    if (d2 < bd) { bd = d2; SpawnCell = Cells[i]; }
                }
            }
        }

        Explore();
    }

    /// <summary>Сам обход по состояниям. Вынесен из <see cref="Search"/>, чтобы <see cref="Rerun"/>
    /// мог повторить его на уже подготовленной модели.</summary>
    private void Explore()
    {
        int full = MoveBudget > 0 ? MoveBudget : int.MaxValue / 4;
        for (int i = 0; i < N; i++)
            if (IsHold(0, Cells[i]) && CanStep(0, SpawnCell, Cells[i]))
            {
                int s0i = WithKeys(0, i);
                if (Seen[s0i]) continue;
                MarkTouched(s0i);
                Seen[s0i] = true; Prev[s0i] = -1; Depth[s0i] = 0; _timeLeft[s0i] = full; _q.Enqueue(s0i);
            }

        while (_q.Count > 0)
        {
            int cur = _q.Dequeue();
            int m = GroupMask(cur), ci = cur % N, keys = KeyMask(cur);
            var hc = Cells[ci];
            int t = _timeLeft[cur];

            for (int g = 0; g < G; g++)                     // нажать кнопку (касание пэдом)
            {
                if ((m & (1 << g)) != 0) continue;
                if ((IgnoreGroups & (1 << g)) != 0) continue;              // группы в этом прогоне нет
                bool can = false;
                foreach (var b in Spec.groups[g].buttons)
                {
                    if (b.host == LevelButton.HostGone) continue;          // хозяина больше нет вовсе
                    if (b.host >= 0 && (IgnoreGroups & (1 << b.host)) != 0) continue;   // хозяина убрали
                    if (b.host >= 0 && !TilesSolid(m, b.host)) continue;   // хозяин ещё в превью
                    if (!CanTouch(m, hc, b.cell, b.center, b.half)) continue;
                    can = true; break;
                }
                if (can) Enter(cur, m | (1 << g), ci, 1, full);   // нажатие ЗАВОДИТ окно заново
            }
            // Окно закончилось. Это и «игрок постоял и подождал», и принудительное закрытие: сам ход
            // остаётся возможным всегда, а вот ХОДИТЬ с открытым окном дольше бюджета уже нельзя.
            for (int g = 0; g < G; g++)
                if ((m & (1 << g)) != 0)
                {
                    int nm = m & ~(1 << g);
                    Enter(cur, nm, ci, 2, nm == 0 ? full : t);
                }
            // ⚠️ ПЕРЕХВАТ СТОИТ ВРЕМЕНИ. Пока висит хоть одна открытая группа, запас тратится, и
            // когда он кончился, шагать нельзя — сперва окно должно закрыться (ход выше). Именно это
            // и делает механику «успей добежать» проверяемой.
            if (m != 0 && t <= 0) continue;
            int nt = m != 0 ? t - 1 : full;
            foreach (int j in _reachNear[ci])               // перейти на другой холд
            {
                if (!IsHold(m, Cells[j])) continue;
                if (!CanStep(m, hc, Cells[j])) continue;
                int ns = WithKeys(keys, m, j);              // по дороге подбираем всё, до чего дотянулись
                if (Seen[ns] && _timeLeft[ns] >= nt) continue;
                if (!Seen[ns]) { MarkTouched(ns); Prev[ns] = cur; PrevKind[ns] = 0; Depth[ns] = Depth[cur] + 1; }
                Seen[ns] = true; _timeLeft[ns] = nt; _q.Enqueue(ns);
            }
        }
    }

    /// <summary>
    /// Переход со сменой маски с разрешением ПАДЕНИЯ: если опора под ногами перестала быть твёрдой,
    /// игрок летит вниз по своей колонке до первой твёрдой клетки. Лететь некуда = смерть, ход
    /// отбрасываем. Если над головой стало твёрдо (вернулась инверсная стена) — тоже отбрасываем.
    /// </summary>
    private void Enter(int from, int nm, int ci, byte kind, int newTime)
    {
        int idx = Landing(nm, ci);
        if (idx < 0) return;
        int ns = WithKeys(KeyMask(from), nm, idx);
        if (Seen[ns] && _timeLeft[ns] >= newTime) return;
        if (!Seen[ns]) { MarkTouched(ns); Prev[ns] = from; PrevKind[ns] = kind; Depth[ns] = Depth[from] + 1; }
        Seen[ns] = true; _timeLeft[ns] = newTime; _q.Enqueue(ns);
    }

    /// <summary>
    /// Где окажется игрок, стоявший на холде <paramref name="ci"/>, когда маска станет
    /// <paramref name="nm"/>: там же либо ниже по колонке (опора исчезла — падение).
    /// −1 — так нельзя: лететь некуда (смерть) или над головой стало твёрдо (раздавило).
    /// </summary>
    private int Landing(int nm, int ci)
    {
        var pos = Cells[ci];
        var bits = SolidBits(nm);
        if (!At(bits, BitIndex(pos.x, pos.y)))
        {
            int landY = int.MinValue;
            for (int y = pos.y - 1; y >= _minY; y--)
                if (At(bits, BitIndex(pos.x, y))) { landY = y; break; }
            if (landY == int.MinValue) return -1;
            return CellIndex(new Vector2Int(pos.x, landY));
        }
        if (At(bits, BitIndex(pos.x, pos.y + 1))) return -1;
        return ci;
    }

    /// <summary>Состояние, из которого цель достаётся: ближайшее к ней, при равенстве — по числу
    /// ходов. Игрок читает последний отрезок ветки как «вот так дотянуться», поэтому показываем
    /// самый близкий хват, а не первый попавшийся.</summary>
    public int FindTouchState(LevelTarget t)
    {
        if (!t.exists || Seen == null) return -1;
        int best = -1, bnear = int.MaxValue, bd = int.MaxValue;
        for (int st = 0; st < TOTAL; st++)
        {
            if (!Seen[st]) continue;
            var hc = Cells[st % N];
            int near = Mathf.Abs(hc.x - t.cell.x) + Mathf.Abs(hc.y - t.cell.y);
            if (near > bnear || (near == bnear && Depth[st] >= bd)) continue;
            if (!CanTouch(GroupMask(st), hc, t)) continue;
            bnear = near; bd = Depth[st]; best = st;
        }
        return best;
    }

    /// <summary>Цепочка состояний от спавна до st (первым идёт стартовое состояние).</summary>
    public List<int> ChainTo(int st)
    {
        var ch = new List<int>();
        for (int s = st; s >= 0; s = Prev[s]) { ch.Add(s); if (Prev[s] < 0) break; }
        ch.Reverse(); return ch;
    }

    /// <summary>Клетки, на которых модель побывала хоть в каком-то состоянии.</summary>
    public HashSet<int> ReachedCellIndices()
    {
        var set = new HashSet<int>();
        if (Seen == null) return set;
        for (int st = 0; st < TOTAL; st++) if (Seen[st]) set.Add(st % N);
        return set;
    }

    /// <summary>
    /// ⭐ ТУПИКОВЫЕ СОСТОЯНИЯ — те, из которых финиш уже НЕ ДОСТАТЬ НИКОГДА. Обычная проверка
    /// («цель достижима из спавна») их не видит: поиск обходит все состояния разом и находит финиш по
    /// любой ветке, а живой игрок в каждый момент находится в ОДНОМ состоянии.
    ///
    /// Пока у каждого механизма была кнопка с обеих сторон, вопрос не стоял. Он появился с
    /// ОДНОСТОРОННИМИ путями (разбор ручного Level_07: провалился в колодец → взял ключ → вылез в
    /// другом месте). Там дверь-выход открывается ТОЛЬКО изнутри, и по критерию «механизм несущий»
    /// она выглядит декоративной: убери её — ключ всё равно достижим, просто наружу не выйти.
    /// Без этой проверки генератор забраковал бы собственную вылазку, а заодно мог бы выдать уровень,
    /// где игрок запирается всерьёз.
    ///
    /// ⚠️ ШАГ ОБРАТИМ (дотяжка симметрична, путь между клетками тоже), поэтому внутри одной маски
    /// состояния разбиваются на КОМПОНЕНТЫ СВЯЗНОСТИ и считать можно по ним, а не по состояниям —
    /// иначе обратный обход по рёбрам не влез бы в память на десяти группах.
    /// </summary>
    public int StuckStates()
    {
        if (Seen == null || !Spec.finish.exists || N == 0) return 0;

        var comp = new int[TOTAL];
        for (int i = 0; i < TOTAL; i++) comp[i] = -1;
        int nComp = 0;
        var stack = new Stack<int>();
        for (int st = 0; st < TOTAL; st++)
        {
            if (!Seen[st] || comp[st] >= 0) continue;
            int m = GroupMask(st);
            comp[st] = nComp; stack.Push(st);
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                int ci0 = cur % N, combined = cur / N;
                var hc = Cells[ci0];
                // ⚠️⚠️ ИНДЕКС СОСЕДА СЧИТАЕТСЯ ОТ ПОЛНОГО СОСТОЯНИЯ, а не от одной маски групп.
                // 🐞 Здесь стояло `m * N + j` — то есть биты СОБРАННЫХ КЛЮЧЕЙ отбрасывались, и обход
                // из состояния «ключ в кармане» перепрыгивал в состояние «ключей нет». Компоненты
                // связности выходили перемешанными, а на них держится весь вывод про тупики.
                foreach (int j in _reachNear[ci0])
                {
                    int ns = combined * N + j;
                    if (!Seen[ns] || comp[ns] >= 0) continue;
                    if (!IsHold(m, Cells[j]) || !CanStep(m, hc, Cells[j])) continue;
                    comp[ns] = nComp; stack.Push(ns);
                }
            }
            nComp++;
        }

        var preds = new List<int>[nComp];
        for (int i = 0; i < nComp; i++) preds[i] = new List<int>();
        var safe = new bool[nComp];
        var q = new Queue<int>();
        for (int st = 0; st < TOTAL; st++)
        {
            if (!Seen[st]) continue;
            int m = GroupMask(st), ci = st % N;
            if (!safe[comp[st]] && CanTouch(m, Cells[ci], Spec.finish))
            { safe[comp[st]] = true; q.Enqueue(comp[st]); }

            for (int g = 0; g < G; g++)                       // смены маски = рёбра между компонентами
            {
                int nm;
                if ((m & (1 << g)) == 0)
                {
                    if ((IgnoreGroups & (1 << g)) != 0) continue;
                    bool can = false;
                    foreach (var b in Spec.groups[g].buttons)
                    {
                        if (b.host == LevelButton.HostGone) continue;
                        if (b.host >= 0 && (IgnoreGroups & (1 << b.host)) != 0) continue;
                        if (b.host >= 0 && !TilesSolid(m, b.host)) continue;
                        if (!CanTouch(m, Cells[ci], b.cell, b.center, b.half)) continue;
                        can = true; break;
                    }
                    if (!can) continue;
                    nm = m | (1 << g);
                }
                else nm = m & ~(1 << g);
                int idx = Landing(nm, ci);
                if (idx < 0) continue;
                int dst = nm * N + idx;
                if (!Seen[dst]) continue;
                preds[comp[dst]].Add(comp[st]);
            }
        }

        while (q.Count > 0)
        {
            int c = q.Dequeue();
            foreach (int p in preds[c]) if (!safe[p]) { safe[p] = true; q.Enqueue(p); }
        }

        int stuck = 0;
        _stuckFlag = new bool[TOTAL];
        for (int st = 0; st < TOTAL; st++)
            if (Seen[st] && !safe[comp[st]]) { stuck++; _stuckFlag[st] = true; }
        return stuck;
    }

    /// <summary>
    /// Какие именно состояния тупиковые — заполняется последним вызовом <see cref="StuckStates"/>.
    /// Нужно, чтобы спрашивать не «сколько их», а «можно ли ВЗЯТЬ КЛЮЧ И ВЫЙТИ»: игрок в каждый
    /// момент находится в одном состоянии, и вопрос всегда про конкретное.
    /// </summary>
    public bool[] StuckFlags => _stuckFlag;
    private bool[] _stuckFlag;

    /// <summary>Группы, чьи кнопки так и не удалось нажать (их платформы мертвы).</summary>
    public List<string> DeadGroups()
    {
        var dead = new List<string>();
        if (Seen == null) return dead;
        var activated = new bool[G];
        for (int st = 0; st < TOTAL; st++)
        {
            if (!Seen[st]) continue;
            int m = GroupMask(st);
            for (int g = 0; g < G; g++) if ((m & (1 << g)) != 0) activated[g] = true;
        }
        for (int g = 0; g < G; g++) if (!activated[g]) dead.Add(Spec.groups[g].id.ToUpperInvariant());
        return dead;
    }
}
