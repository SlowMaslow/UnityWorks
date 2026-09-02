using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ КОМПОНОВЩИК ГОЛОВОЛОМКИ — «СНАЧАЛА ЗАМЫСЕЛ, ПОТОМ ГЕОМЕТРИЯ» (решение игрока 2026-09-02).
///
/// Как было до него: генератор строил лабиринт, а потом ТРИ независимых прохода искали, куда бы
/// воткнуть механизм — «широкий пол? ставлю мост», «шахта режет дерево? ставлю ворота». Механизмы
/// получались не связаны друг с другом, а цепочка «нажал A → добрался до B» выходила случайно, если
/// вообще выходила. Отсюда и вердикт игрока про «уровни среднего качества»: отсутствие брака — это
/// не замысел. Приёмка умела ОТБРАКОВЫВАТЬ, но не умела ЗАДУМЫВАТЬ.
///
/// Как стало: сперва на голом дереве комнат строится ПЛАН — какие рёбра заперты, чем, и в каком
/// порядке их обязан открывать игрок. План ничего не знает про клетки, тайлы и дотяжку: это чистый
/// граф зависимостей. Только потом геометрия: каждый узел плана отдаётся модулю, который вписывает
/// его в конкретную комнату.
///
/// ⭐ ДВА СВОЙСТВА ПО ПОСТРОЕНИЮ (а не по проверке постфактум):
///   • КАЖДЫЙ МЕХАНИЗМ НЕСУЩИЙ. Ребро запирается, только если за ним ОСТАЁТСЯ ЦЕЛЬ (финиш, ключ или
///     кнопка следующего механизма). Обойти нельзя: лабиринт — остовное дерево, второго пути нет.
///   • ЦЕПОЧКА НАСТОЯЩАЯ. Кнопка «открыть» лежит в комнате-родителе, то есть ПЕРЕД воротами. Если
///     следующие ворота стоят глубже по тому же корневому пути, их кнопка физически недостижима,
///     пока не открыты предыдущие. Порядок не «так вышло», а «иначе не бывает».
///
/// ⚠️ МОСТ НА ТАЙМЕРЕ ГЛУБИНЫ НЕ ДАЁТ, и это честно: его кнопка доступна всегда, он ограничивает
/// ВРЕМЕНЕМ, а не доступом. Поэтому мосты в плане идут отдельным видом — «фактура внутри яруса», —
/// и в длину цепочки не засчитываются. Складывать их с воротами значило бы врать себе о сложности.
/// </summary>
public static class PuzzleComposer
{
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Ребро дерева комнат, ориентированное ОТ СТАРТА: <see cref="parent"/> ближе к спавну.</summary>
    public struct PuzzleEdge
    {
        public Vector2Int parent, child;
        public bool Vertical => parent.x == child.x;
        /// <summary>Подъём (ребёнок выше). Запирать имеет смысл только подъём: вниз игрок и так упадёт.</summary>
        public bool Upward => parent.x == child.x && child.y > parent.y;
        public override string ToString() => $"({parent.x},{parent.y})→({child.x},{child.y})";
    }

    /// <summary>Чем механизм держит игрока.</summary>
    public enum Role
    {
        /// <summary>Вертикальные ворота: ступенька из появляющихся платформ. Даёт ЯРУС.</summary>
        Gate,
        /// <summary>Дверь-инверсия: проём заложен камнем, кнопка убирает его на окно. Даёт ЯРУС.</summary>
        Door,
        /// <summary>Мост на таймере: у сквозного коридора нет пола. Яруса НЕ даёт — ограничивает время.</summary>
        Bridge
    }

    /// <summary>Один узел замысла: что заперто, чем и после чего открывается.</summary>
    public class PlannedGate
    {
        public PuzzleEdge edge;
        public Role role;
        /// <summary>Ярус: сколько ДРУГИХ механизмов надо открыть, чтобы добраться до кнопки этого.
        /// 0 — доступен от спавна.</summary>
        public int tier;
        /// <summary>Индексы узлов плана, которые обязаны быть открыты раньше (по построению — те,
        /// что стоят на корневом пути к этому ребру).</summary>
        public List<int> requires = new List<int>();
        /// <summary>Ради чего заперто — для лога и отладки замысла.</summary>
        public string holds = "";
        /// <summary>Комната, из которой жмут «открыть» (всегда родитель ребра).</summary>
        public Vector2Int buttonRoom;
        /// <summary>Комната кнопки возврата (всегда ребёнок): без неё игрок заперся бы за собой.</summary>
        public Vector2Int backRoom;
        /// <summary>Какой группой механизм в итоге построен (заполняет реализация).</summary>
        public char groupId;
        /// <summary>
        /// ⭐ ВЛОЖЕННОСТЬ: механизм, НА ПЛАТФОРМЕ которого стоит кнопка «открыть» этого. null — кнопка
        /// лежит на полу и доступна всегда.
        ///
        /// Зачем это нужно отдельно от ярусов по дереву: обычная цепочка требует, чтобы следующее
        /// запертое ребро лежало ГЛУБЖЕ предыдущего по корневому пути, а такой формы лабиринт даёт не
        /// всегда — отсюда уровни со сцеплением 1 при запрошенных 3. Вложенная кнопка создаёт
        /// зависимость там, где дерево её не даёт: кнопку видно, но она призрачная, пока не вызван
        /// хозяин. Это и есть «уникальная вложенность триггеров», а не ещё одни ворота подряд.
        ///
        /// ⚠️ КНОПКУ СТАВИМ ТОЛЬКО НА ТАЙЛЫ ХОЗЯИНА, никогда в стороне. Нажать её можно лишь пока идёт
        /// окно хозяина, а модель времени не знает — значит бежать до неё нельзя ни одной клетки.
        /// Стоя на платформе хозяина, игрок уже у кнопки, и таймингового риска нет по построению.
        /// </summary>
        public PlannedGate nestOn;
    }

    /// <summary>Готовый замысел уровня.</summary>
    public class PuzzlePlan
    {
        public List<PlannedGate> gates = new List<PlannedGate>();
        /// <summary>Длина обязательной цепочки в механизмах (мосты не в счёт — см. заголовок класса).</summary>
        public int Depth
        {
            get
            {
                int d = 0;
                foreach (var g in gates) if (g.role != Role.Bridge && g.tier + 1 > d) d = g.tier + 1;
                return d;
            }
        }

        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("цепочка ").Append(Depth).Append(", механизмов ").Append(gates.Count).Append(": ");
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                if (i > 0) sb.Append("; ");
                sb.Append(g.role == Role.Gate ? "ворота" : g.role == Role.Door ? "дверь" : "мост")
                  .Append(' ').Append(g.groupId == '\0' ? '?' : g.groupId)
                  .Append(' ').Append(g.edge).Append(" ярус ").Append(g.tier)
                  .Append(" [").Append(g.holds).Append(']');
                if (g.nestOn != null) sb.Append(" ⟨кнопка на ").Append(g.nestOn.groupId).Append('⟩');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Мир головоломки: дерево комнат и цели. Ни одной клетки, ни одного тайла — только связи.
    /// Всё это генератор уже посчитал для себя (обход, финиш, ключи), компоновщик лишь получает.
    /// </summary>
    public class PuzzleWorld
    {
        public Vector2Int start;
        public Vector2Int finish;
        public List<Vector2Int> keys = new List<Vector2Int>();
        public Dictionary<Vector2Int, List<Vector2Int>> adj = new Dictionary<Vector2Int, List<Vector2Int>>();
        /// <summary>Комнаты, занятые под другую роль (спавн, финиш, флаг): механизм туда не лезет.</summary>
        public HashSet<Vector2Int> busy = new HashSet<Vector2Int>();
        /// <summary>Сквозные горизонтальные коридоры — единственные места, где мост осмыслен.</summary>
        public HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
        /// <summary>Комнаты, где вертикальные ворота физически не встанут (низкий потолок и т.п.).
        /// Заполняет генератор: это его знание о геометрии, а не плана.</summary>
        public HashSet<Vector2Int> noGate = new HashSet<Vector2Int>();

        public List<Vector2Int> Neighbours(Vector2Int r)
        { List<Vector2Int> l; return adj.TryGetValue(r, out l) ? l : new List<Vector2Int>(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// ⭐ ПОСТРОИТЬ ЗАМЫСЕЛ. Сперва основная линия (цепочка ярусов на пути спавн→финиш), потом боковые
    /// механизмы к ключам, потом мосты для фактуры.
    /// </summary>
    /// <param name="wantChain">Желаемая длина цепочки в механизмах: 1 — «нажал и прошёл», 3 — «нажал,
    /// поднялся, нашёл вторую, открыл третью». Это и есть ручка сложности пака.</param>
    /// <param name="maxGates">Потолок по числу механизмов (маска групп в модели — 12 бит, и каждый
    /// лишний механизм умножает время приёмки вдвое).</param>
    /// <param name="maxDoors">Потолок по дверям-инверсиям. ⚠️ Держим 1: механика новая, в игре ещё не
    /// обкатана, и сыпать по несколько самозакрывающихся стен на уровень рано (решение игрока
    /// 2026-09-02). Планировщик из-за этого охотнее берёт вертикальные ворота — это и нужно.</param>
    /// <param name="banned">Рёбра, на которых реализация уже провалилась: план обязан их обойти.</param>
    public static PuzzlePlan Plan(PuzzleWorld w, int wantChain, int maxGates,
                                  System.Random rng, int maxDoors = 1, HashSet<string> banned = null)
    {
        var plan = new PuzzlePlan();
        if (banned == null) banned = new HashSet<string>();

        // ── Дерево от старта: родители и порядок обхода ──
        List<Vector2Int> order;
        var parent = BuildParents(w, out order);

        // ── Что лежит ЗА каждым ребром (в поддереве ребёнка): считаем обходом снизу вверх ──
        var subFinish = new HashSet<Vector2Int>();
        var subKeys = new Dictionary<Vector2Int, int>();
        foreach (var r in order) subKeys[r] = 0;
        for (int i = order.Count - 1; i >= 0; i--)
        {
            var r = order[i];
            if (r == w.finish) subFinish.Add(r);
            foreach (var k in w.keys) if (k == r) subKeys[r]++;
            Vector2Int p;
            if (!parent.TryGetValue(r, out p)) continue;
            if (subFinish.Contains(r)) subFinish.Add(p);
            subKeys[p] += subKeys[r];
        }

        // Путь спавн→финиш комнатами: это «основная линия», по ней и строится цепочка.
        var mainPath = new List<Vector2Int>();
        {
            var cur = w.finish; mainPath.Add(cur);
            while (parent.ContainsKey(cur)) { cur = parent[cur]; mainPath.Add(cur); }
            mainPath.Reverse();
        }

        // Ярус ребра = сколько УЖЕ ЗАПЛАНИРОВАННЫХ механизмов-ворот стоит на пути от старта до него.
        // Именно это делает цепочку обязательной: кнопка ребра лежит в комнате-родителе, а до неё
        // не дойти, пока не открыты все ворота выше по корневому пути.
        System.Func<PuzzleEdge, List<int>> requiredBefore = e => RequiredBefore(parent, plan.gates, e);

        System.Func<PuzzleEdge, string> key = e => e.parent.x + "," + e.parent.y + ">" + e.child.x + "," + e.child.y;

        // Можно ли запереть это ребро — единственное место, где проверяются ВСЕ запреты сразу.
        // <paramref name="shareWith"/> — механизм, с которым РАЗРЕШЕНО делить комнату: у вложенной
        // кнопки это её хозяин. 🐞 Без этого исключения правило «одна комната — один механизм» резало
        // ровно те рёбра, ради которых вложенность и заводилась (кнопка стоит на платформе хозяина,
        // то есть в его комнате), и вложенность обнулялась в ноль случаев из шестнадцати.
        // Геометрического конфликта нет: платформы вложенного механизма лежат в СОСЕДНЕЙ комнате,
        // общей у них остаётся только кнопка.
        System.Func<PuzzleEdge, Role, PlannedGate, bool> usableWith = (e, role, shareWith) =>
        {
            if (banned.Contains(key(e))) return false;
            if (w.busy.Contains(e.parent) || w.busy.Contains(e.child)) return false;
            foreach (var g in plan.gates)                              // одна комната — один механизм
            {
                if (g == shareWith) continue;
                if (g.edge.parent == e.parent && g.edge.child == e.child) return false;
                if (g.edge.parent == e.parent || g.edge.child == e.parent
                 || g.edge.parent == e.child || g.edge.child == e.child) return false;
            }
            if (shareWith != null && shareWith.edge.parent == e.parent && shareWith.edge.child == e.child)
                return false;                                          // то же самое ребро дважды
            // ⭐ НЕСУЩЕЕ ПО ПОСТРОЕНИЮ: за ребром обязана остаться цель. Кнопки следующих механизмов
            // тоже цель — но они появятся позже, поэтому здесь достаточно финиша или ключа: цепочка
            // строится сверху вниз по пути, и у более раннего ребра поддерево всегда шире.
            if (!subFinish.Contains(e.child) && subKeys[e.child] == 0) return false;
            if (role == Role.Gate) return e.Upward && !w.noGate.Contains(e.parent);
            if (role == Role.Door)
            {
                if (e.Vertical) return false;
                int doors = 0;
                foreach (var g in plan.gates) if (g.role == Role.Door) doors++;
                return doors < maxDoors;
            }
            return false;
        };
        // Обычный случай: делить комнату не с кем.
        System.Func<PuzzleEdge, Role, bool> usable = (e, role) => usableWith(e, role, null);

        // Роль по геометрии ребра: вверх — ворота, вбок — дверь-инверсия. Спуск запирать бессмысленно
        // (игрок просто спрыгнет), такие рёбра план не берёт вовсе.
        System.Func<PuzzleEdge, Role?> roleFor = e =>
        {
            if (usable(e, Role.Gate)) return Role.Gate;
            if (usable(e, Role.Door)) return Role.Door;
            return null;
        };

        // ── 1. ОСНОВНАЯ ЛИНИЯ: цепочка ярусов на пути к финишу ────────────────────────────────
        // Рёбра берём ПО ПОРЯДКУ от старта: каждое следующее лежит глубже предыдущего, значит его
        // кнопка заперта предыдущим. Это и есть механизм получения глубины.
        // ⭐ ЗВЕНО МОЖЕТ БЫТЬ ВЛОЖЕННЫМ. Правило «одна комната — один механизм» заставляет пропускать
        // комнату между звеньями, и цепочка получается разреженной. Но если предыдущее звено — ВОРОТА,
        // следующее можно посадить на СОСЕДНЕЕ ребро и повесить его кнопку прямо на ступеньку
        // предыдущих: игрок нажал, залез, а там уже следующая кнопка. Так строится «цепочка вложенных
        // триггеров подряд» — и звенья идут вплотную, а не через комнату.
        int chainWant = Mathf.Clamp(wantChain, 0, maxGates);
        PlannedGate lastGate = null;
        for (int i = 0; i + 1 < mainPath.Count && plan.Depth < chainWant && plan.gates.Count < maxGates; i++)
        {
            var e = new PuzzleEdge { parent = mainPath[i], child = mainPath[i + 1] };
            // Делим комнату с предыдущим звеном? Тогда это возможно только вложенностью, а хозяином
            // может быть только механизм С ПЛАТФОРМОЙ (ворота): на дверь кнопку не поставишь.
            bool touchesLast = lastGate != null
                && (lastGate.edge.parent == e.parent || lastGate.edge.child == e.parent
                 || lastGate.edge.parent == e.child  || lastGate.edge.child == e.child);
            PlannedGate share = touchesLast ? lastGate : null;
            if (share != null && share.role != Role.Gate) continue;
            Role? role = usableWith(e, Role.Gate, share) ? Role.Gate
                       : usableWith(e, Role.Door, share) ? Role.Door : (Role?)null;
            if (role == null) continue;
            // ⚠️ ДВЕРЬ — ПОСЛЕДНИЙ ВЫБОР, А НЕ ПЕРВЫЙ. Механика новая и в игре не обкатана, а «первое
            // подходящее ребро» ставило её ВО ВСЕ уровни подряд (замер: 24 из 24), потому что первое
            // ребро маршрута часто горизонтальное. Смотрим вперёд: если дальше по маршруту есть
            // вертикальный подъём, берём ворота, а дверь оставляем на случай, когда его нет.
            if (role == Role.Door)
            {
                bool gateAhead = false;
                for (int j = i + 1; j + 1 < mainPath.Count && !gateAhead; j++)
                {
                    var e2 = new PuzzleEdge { parent = mainPath[j], child = mainPath[j + 1] };
                    if (usable(e2, Role.Gate)) gateAhead = true;
                }
                if (gateAhead) continue;
            }
            var g = new PlannedGate
            {
                edge = e, role = role.Value, buttonRoom = e.parent, backRoom = e.child,
                nestOn = share,
                holds = subFinish.Contains(e.child) ? "финиш" : "ключ"
            };
            g.requires = requiredBefore(e);
            g.tier = 0; foreach (int ri in g.requires) g.tier = Mathf.Max(g.tier, plan.gates[ri].tier + 1);
            if (share != null) g.tier = Mathf.Max(g.tier, share.tier + 1);
            plan.gates.Add(g);
            if (g.role == Role.Gate) lastGate = g;                 // хозяином может быть только платформа
            else lastGate = null;
        }

        // ── 2. ВЛОЖЕННАЯ КНОПКА — РАВНОПРАВНЫЙ ПАТТЕРН, А НЕ ЗАПЛАТКА ────────────────────────
        // ⭐ Кнопка стоит НА платформе другого механизма: пока хозяин не вызван, она призрачная и не
        // нажимается. Зависимость берётся не из формы лабиринта, а из СОСТОЯНИЯ — поэтому запереть
        // можно любое свободное ребро, даже то, что лежит в стороне от корневого пути.
        //
        // ⚠️ Сначала я поставил это только «когда дерево не дало нужной глубины» — и вложенность
        // выпадала на 1 уровень из 16. Такого решения не принимали: цель — разнообразие и как можно
        // больше разных паттернов, а не аварийный добор. Теперь ставим всегда, пока есть бюджет.
        // Несколько вложенных на РАЗНЫХ хозяевах дают ветвящуюся зависимость («на платформе A две
        // кнопки»), на одном и том же — цепочку вглубь.
        {
            // Хозяин обязан иметь ПЛАТФОРМУ, на которую можно встать: у ворот это ступенька.
            // ⛔ Дверь хозяином быть не может: у инверсной группы «тайлы твёрдые» = «окно НЕ идёт»,
            // то есть вложенная в неё кнопка была бы доступна по умолчанию и запиралась при нажатии —
            // ровно наоборот. Мост тоже не берём: его пол исчезает под ногами.
            while (plan.gates.Count < maxGates)
            {
                // Хозяин: самые глубокие ворота, ещё не занятые вложенной кнопкой, — так каждая новая
                // вложенность удлиняет цепочку, а не громоздится на одну и ту же платформу.
                PlannedGate host = null;
                foreach (var g0 in plan.gates)
                {
                    if (g0.role != Role.Gate) continue;
                    bool taken = false;
                    foreach (var g1 in plan.gates) if (g1.nestOn == g0) { taken = true; break; }
                    if (taken) continue;
                    if (host == null || g0.tier > host.tier) host = g0;
                }
                if (host == null) break;
                // ⚠️⚠️ КНОПКА И ЕЁ ПЛАТФОРМЫ ДОЛЖНЫ БЫТЬ РЯДОМ. Нажатие запускает ОКНО, а модель времени
                // не знает и одобрит любое расстояние. Сперва я разрешил вложить кнопку на любое ребро
                // лабиринта — получалось «нажал здесь, беги через полкарты, пока окно идёт», и проверить
                // это было нечем. Поэтому запирать вложенной кнопкой можно ТОЛЬКО выход из комнаты
                // хозяина или из комнаты над ней: игрок стоит на ступеньке хозяина и всё нужное — рядом.
                var cands = new List<PuzzleEdge>();
                foreach (var r in order)
                {
                    Vector2Int p;
                    if (!parent.TryGetValue(r, out p)) continue;
                    if (p == host.edge.child || p == host.edge.parent)
                        cands.Add(new PuzzleEdge { parent = p, child = r });
                }
                bool done = false;
                for (int pass = 0; pass < 2 && !done; pass++)
                {
                    var want = pass == 0 ? Role.Gate : Role.Door;
                    foreach (var e in cands)
                    {
                        if (!usableWith(e, want, host)) continue;
                        plan.gates.Add(new PlannedGate
                        {
                            edge = e, role = want, buttonRoom = e.parent, backRoom = e.child,
                            nestOn = host, tier = host.tier + 1,
                            holds = subFinish.Contains(e.child) ? "финиш" : "ключ"
                        });
                        done = true; break;
                    }
                }
                if (!done) break;                                  // свободных рёбер больше нет
            }
        }

        // ── 3. БОКОВЫЕ МЕХАНИЗМЫ: ключи в ответвлениях ───────────────────────────────────────
        // Запираем ребро, которым ветка ОТХОДИТ от основной линии: тогда за механизмом оказывается
        // вся ветка целиком, а не последняя её комната. Ярус наследуется от места ответвления —
        // ветка, отходящая за вторыми воротами, автоматически становится ярусом 2.
        var onMain = new HashSet<Vector2Int>(mainPath);
        var keysShuffled = new List<Vector2Int>(w.keys);
        for (int i = keysShuffled.Count - 1; i > 0; i--)
        { int j = rng.Next(i + 1); var tmp = keysShuffled[i]; keysShuffled[i] = keysShuffled[j]; keysShuffled[j] = tmp; }
        foreach (var kroom in keysShuffled)
        {
            if (plan.gates.Count >= maxGates) break;
            if (onMain.Contains(kroom)) continue;                      // ключ на маршруте — ответвления нет
            var branch = new List<Vector2Int>();                       // путь от ключа вверх до основной линии
            var cur = kroom;
            while (!onMain.Contains(cur) && parent.ContainsKey(cur)) { branch.Add(cur); cur = parent[cur]; }
            if (branch.Count == 0) continue;
            branch.Reverse();                                          // теперь от точки ответвления к ключу
            var attach = cur;
            // ⚠️ Здесь тот же приоритет, что и на основной линии: СНАЧАЛА ищем по всей ветке место под
            // ворота и только потом соглашаемся на дверь. Без этого прохода в два круга дверь попадала
            // в 21 уровень из 24 — лимит «одна на уровень» соблюдался, а разнообразия не было.
            bool branchDone = false;
            for (int pass = 0; pass < 2 && !branchDone; pass++)
            {
                var want = pass == 0 ? Role.Gate : Role.Door;
                for (int bi = 0; bi < branch.Count; bi++)
                {
                    var e = new PuzzleEdge { parent = bi == 0 ? attach : branch[bi - 1], child = branch[bi] };
                    if (!usable(e, want)) continue;
                    var g = new PlannedGate
                    {
                        edge = e, role = want, buttonRoom = e.parent, backRoom = e.child, holds = "ключ"
                    };
                    g.requires = requiredBefore(e);
                    g.tier = 0; foreach (int ri in g.requires) g.tier = Mathf.Max(g.tier, plan.gates[ri].tier + 1);
                    plan.gates.Add(g);
                    branchDone = true;                                 // одна ветка — один механизм
                    break;
                }
            }
        }

        // ── 4. МОСТ: фактура внутри яруса ────────────────────────────────────────────────────
        // Ставим на сквозной коридор основной линии. Глубины не даёт (см. заголовок), поэтому идёт
        // последним и только если остался запас по числу механизмов.
        if (plan.gates.Count < maxGates)
        for (int i = 1; i + 1 < mainPath.Count; i++)
        {
            var room = mainPath[i];
            if (!w.corridors.Contains(room) || w.busy.Contains(room)) continue;
            var e = new PuzzleEdge { parent = room, child = mainPath[i + 1] };
            if (banned.Contains(key(e))) continue;
            bool taken = false;
            foreach (var g0 in plan.gates)
                if (g0.edge.parent == room || g0.edge.child == room) { taken = true; break; }
            if (taken) continue;
            var g2 = new PlannedGate
            { edge = e, role = Role.Bridge, buttonRoom = room, backRoom = room, holds = "пол коридора на маршруте" };
            g2.requires = requiredBefore(e);
            g2.tier = 0; foreach (int ri in g2.requires) g2.tier = Mathf.Max(g2.tier, plan.gates[ri].tier + 1);
            plan.gates.Add(g2);
            break;                                                     // не больше одного моста на уровень
        }

        // Порядок реализации — по ярусам: геометрия ставится в том же порядке, в каком её проходит игрок.
        plan.gates.Sort((a, b) => a.tier.CompareTo(b.tier));
        return plan;
    }

    /// <summary>Ключ ребра для списка запрещённых (реализация провалилась — плану сюда больше не ходить).</summary>
    public static string EdgeKey(PuzzleEdge e)
        => e.parent.x + "," + e.parent.y + ">" + e.child.x + "," + e.child.y;

    /// <summary>Дерево комнат от спавна: кто чей родитель + порядок обхода (родитель раньше детей).</summary>
    private static Dictionary<Vector2Int, Vector2Int> BuildParents(PuzzleWorld w, out List<Vector2Int> order)
    {
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        order = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int> { w.start };
        var q = new Queue<Vector2Int>(); q.Enqueue(w.start);
        while (q.Count > 0)
        {
            var cur = q.Dequeue(); order.Add(cur);
            foreach (var nb in w.Neighbours(cur))
                if (seen.Add(nb)) { parent[nb] = cur; q.Enqueue(nb); }
        }
        return parent;
    }

    /// <summary>Механизмы из <paramref name="gates"/>, стоящие на корневом пути к ребру <paramref name="e"/>.</summary>
    private static List<int> RequiredBefore(Dictionary<Vector2Int, Vector2Int> parent,
                                            List<PlannedGate> gates, PuzzleEdge e)
    {
        var need = new List<int>();
        var cur = e.parent;
        while (true)
        {
            Vector2Int p;
            if (!parent.TryGetValue(cur, out p)) break;
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                if (g.role == Role.Bridge) continue;              // мост не запирает доступ
                if (g.edge.parent == p && g.edge.child == cur) need.Add(i);
            }
            cur = p;
        }
        return need;
    }

    /// <summary>
    /// ⭐ ПЕРЕСЧЁТ ЯРУСОВ ПО ФАКТУ. Планировать приходится в несколько заходов: если геометрия не
    /// приняла механизм, ребро попадает в запрет и план строится заново — а у нового плана нумерация
    /// ярусов своя, про уже вбитые механизмы он не знает. Поэтому итог ВСЕГДА пересчитываем по
    /// реально построенному набору, иначе отчёт о сложности врал бы в меньшую сторону.
    /// </summary>
    public static void Retier(PuzzleWorld w, List<PlannedGate> gates)
    {
        List<Vector2Int> order;
        var parent = BuildParents(w, out order);
        // Сортировка по глубине корневого пути гарантирует, что предшественники уже пронумерованы.
        var depthOf = new Dictionary<Vector2Int, int>();
        foreach (var r in order)
        {
            Vector2Int p;
            depthOf[r] = parent.TryGetValue(r, out p) ? depthOf[p] + 1 : 0;
        }
        gates.Sort((a, b) =>
        {
            int da = depthOf.ContainsKey(a.edge.child) ? depthOf[a.edge.child] : 0;
            int db = depthOf.ContainsKey(b.edge.child) ? depthOf[b.edge.child] : 0;
            return da.CompareTo(db);
        });
        // ⚠️ СЧИТАЕМ ДО НЕПОДВИЖНОЙ ТОЧКИ, а не одним проходом слева направо. Порядок по глубине в
        // дереве больше не гарантирует, что предшественник стоит раньше: у ВЛОЖЕННОЙ кнопки хозяин
        // может оказаться где угодно, хоть в другой ветке. Одним проходом её ярус занизился бы.
        foreach (var g in gates) g.tier = 0;
        for (int pass = 0; pass <= gates.Count; pass++)
        {
            bool changed = false;
            for (int i = 0; i < gates.Count; i++)
            {
                gates[i].requires = RequiredBefore(parent, gates, gates[i].edge);
                int tier = 0;
                foreach (int ri in gates[i].requires) tier = Mathf.Max(tier, gates[ri].tier + 1);
                if (gates[i].nestOn != null) tier = Mathf.Max(tier, gates[i].nestOn.tier + 1);
                if (tier != gates[i].tier) { gates[i].tier = tier; changed = true; }
            }
            if (!changed) break;
        }
    }
}
