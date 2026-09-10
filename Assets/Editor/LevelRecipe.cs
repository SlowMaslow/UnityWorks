using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ СЛОВАРЬ ЭЛЕМЕНТОВ ГОЛОВОЛОМКИ. Из чего вообще может состоять маршрут уровня.
/// Список заведомо будет расти — на том и держится расчёт: добавление элемента должно сводиться
/// к одной строке здесь и одной декларации в <see cref="PuzzleVocabulary"/>.
/// </summary>
public enum PuzzleElement
{
    /// <summary>Вертикальные ворота: ступенька из появляющихся платформ.</summary>
    Gate,
    /// <summary>Дверь-инверсия: проём заложен камнем, кнопка убирает его на окно.</summary>
    Door,
    /// <summary>Мост на таймере: у сквозного коридора нет пола, кнопка возвращает его на время.</summary>
    Bridge,
    /// <summary>Ворота, кнопка которых стоит НА платформе предыдущих (призрачная, пока те не вызваны).</summary>
    NestedGate,
    /// <summary>Вылазка: односторонний вход провалом на ловчую площадку, выход через стену изнутри.</summary>
    Excursion,
    /// <summary>Камера: потолок исчезает по кнопке снаружи, внутри полка с кнопкой, за стенкой —
    /// ключ. Разобрана из ручного Level_11 игрока (см. <see cref="VaultModule"/>).</summary>
    Vault
}

/// <summary>
/// ⭐ ТРЕБОВАНИЯ ЭЛЕМЕНТА К МЕСТУ — ЕДИНСТВЕННОЕ МЕСТО, ГДЕ ОНИ ЗАПИСАНЫ.
///
/// Раньше требования были размазаны по <c>Fits</c> каждого модуля и проверялись ПОСЛЕ того, как
/// размеры комнат уже брошены. Из-за этого каждый новый паттерн превращался в лотерею: мост требует
/// комнату шириной 8 — такие редки; вылазка требует камеру глубиной 5 — 1 схема из 40. Я трижды чинил
/// это подкручиванием вероятностей, то есть лечил симптом.
///
/// Смысл декларации: генератор сперва СПРАШИВАЕТ, что элементу нужно, и строит геометрию под заказ,
/// а не ищет, где повезло. Требования почти всегда вида «не меньше», поэтому на строку и колонку
/// берётся максимум запрошенного — упаковка не нужна.
/// </summary>
public struct SpaceNeed
{
    /// <summary>Сколько комнат маршрута занимает элемент (ворота — пара, мост — одна).</summary>
    public int rooms;
    public int minWidth;              // минимальная ширина комнаты
    public int minHeight;             // минимальная высота комнаты

    /// <summary>
    /// ⭐ ПОТОЛОК РАЗМЕРА КОМНАТЫ. 0 — не ограничиваем.
    ///
    /// 🐞 Трафарету мало сказать «мне нужно не меньше 8×4»: раскладка вправе дать комнату вдвое
    /// больше, и картинка займёт её угол. Стены, которые трафарет рисует сам, оказываются
    /// декорацией внутри чужого простора — игрок проваливается не в камеру, а в обычный зал, и
    /// паттерн не читается вовсе (поймано игроком на первом же импорте камеры).
    /// Комната под трафарет должна быть РОВНО по нему.
    /// </summary>
    public int maxWidth;
    public int maxHeight;

    /// <summary>
    /// ⭐⭐ ПАТТЕРН САМ ОТВЕЧАЕТ ЗА ПОДЪЁМ в своей комнате, поэтому потолок высоты
    /// (<see cref="RoomLayout.MaxRoomHeight"/>) на неё не распространяется.
    ///
    /// Зачем. Потолок в пять рядов стоит по одной причине: выше игроку не за что зацепиться, и
    /// обычную комнату он пройти не сможет. Но у трафарета все уступы нарисованы ВНУТРИ картинки —
    /// ограничение к нему просто не относится.
    /// 🐞 Пока я этого не различал, камера наследовала чужой потолок и от девятирядного оригинала
    /// игрока оставалась плоская коробка в четыре ряда — игрок сказал «даже близко не похоже».
    /// Комната перестаёт быть клеткой для паттерна: её размер диктует СОДЕРЖИМОЕ.
    /// </summary>
    public bool selfClimbing;
    public bool verticalEdge;         // нужен ПОДЪЁМ (ребро вверх)
    public bool horizontalEdge;       // нужно горизонтальное ребро
    public bool deadEnd;              // нужен тупик
    public bool noFloorBelow;         // под комнатой не должно быть этажа (падение смертельно)
    /// <summary>Нужен предыдущий механизм С ПЛАТФОРМОЙ, на которую сядет кнопка.</summary>
    public bool needsHostPlatform;
    /// <summary>Требует ключа в своей комнате (иначе запирать нечего).</summary>
    public bool holdsKey;

    /// <summary>
    /// ⭐ ЭЛЕМЕНТ СОЗДАЁТ ОБЛАСТЬ, ИЗ КОТОРОЙ НЕ ВЫЙТИ ТЕМ ЖЕ ПУТЁМ, и потому требует ВЫХОДА.
    /// Камера — ровно такая: провалился сквозь потолок, а обратно наверх уже не забраться.
    ///
    /// ⚠️ Чем закрыть выход, элемент НЕ РЕШАЕТ. Иначе получается то, что я и сделал в первой версии
    /// камеры: вшил инверсную стену внутрь модуля — а выходом с тем же успехом может быть мост на
    /// таймере обратно к маршруту или любой будущий паттерн. Поэтому элемент лишь ЗАЯВЛЯЕТ нужду,
    /// а подбирает исполнителя словарь (см. <see cref="CanBeExit"/>).
    /// </summary>
    public bool needsExit;

    /// <summary>
    /// ⭐ ЭЛЕМЕНТУ НУЖНА КОМНАТА, ПРОХОДНАЯ НАСКВОЗЬ ВБОК — ровно два боковых прохода и ни одного
    /// вертикального. Так живёт мост: иначе его обходят по вертикальной связи, и он декорация.
    ///
    /// ⚠️ Заявлено ЗДЕСЬ, а не проверено на месте, потому что от этого зависит ВЫБОР исполнителя.
    /// 🐞 Замер: выход из камеры выбирается случайно (дверь или мост), и когда выпадал мост, он не
    /// вставал НИ РАЗУ — у камеры по построению есть вертикальная связь (люк, сквозь который в неё и
    /// проваливаются). Камера оставалась вообще без выхода: ключ берётся, финиш достижим, а вместе
    /// никогда — приёмка говорила «проходим False» и молчала о причине.
    /// </summary>
    public bool throughCorridor;

    /// <summary>
    /// ⭐⭐ ВЫСОТА ПОДВАЛА, который надо зарезервировать РОВНО ПОД комнатой элемента. 0 — не нужен.
    ///
    /// Паттерн вправе занимать несколько этажей: у камеры наверху коридор трассы, а под ним — сама
    /// камера с ключом и колодец возврата. Раньше трафарет пытался выкопать это сам, требуя под
    /// собой сплошной камень, и замер объяснил, почему так нельзя: под коридором сплошного массива
    /// не бывает — 104 отказа из 110 посадок. Место под паттерн РЕЗЕРВИРУЕТСЯ планом.
    /// </summary>
    public int cellarHeight;
}

/// <summary>Что каждый элемент требует от места. Добавляешь элемент — дописываешь строку СЮДА.</summary>
public static class PuzzleVocabulary
{
    public static SpaceNeed Need(PuzzleElement e)
    {
        switch (e)
        {
            // Ступенька ставится на 3 ряда над полом, поэтому при H=3 её просто перелезут.
            case PuzzleElement.Gate:
                return new SpaceNeed { rooms = 2, minWidth = 3, minHeight = 4, verticalEdge = true };

            // Проём должен быть выше одной клетки, иначе это не дверь.
            case PuzzleElement.Door:
                return new SpaceNeed { rooms = 2, minWidth = 3, minHeight = 3, horizontalEdge = true };

            // ⚠️ Ширина 8 не с потолка: опоры стоят по краям, разрыв = W−1, а боковая дотяжка 6.
            // Разрыв уже дотяжки игрок перетягивает руками, и моста фактически нет.
            case PuzzleElement.Bridge:
                return new SpaceNeed { rooms = 1, minWidth = MazeCanvas.ReachSide + 2, minHeight = 3,
                                       throughCorridor = true };

            // Как ворота, но кнопка садится на платформу предыдущего механизма — нужна пристройка,
            // отсюда лишняя клетка ширины у комнаты-хозяина.
            case PuzzleElement.NestedGate:
                return new SpaceNeed { rooms = 2, minWidth = 4, minHeight = 4,
                                       verticalEdge = true, needsHostPlatform = true };

            // Камере нужна комната-коробка под собой и комната сверху, откуда открывают потолок.
            // Ширина — под карман с ключом, стенку и полку; высота — пол, полка на дотяжке, кнопка.
            // ⭐⭐ КАМЕРА — ПРОЛЁТ ТРАССЫ, а не комната сбоку. Разбор ручного Level_11 показал: весь
            // паттерн вместе с коридором сверху и колодцем возврата — один прямоугольник, и дорога
            // назад нарисована ВНУТРИ него. Значит раскладке нужно отдать только сам коридор, три
            // ряда: остальное трафарет выроет в толще под ним (Pattern.rockBelowRow).
            // ⚠️ Комната обязана быть СКВОЗНОЙ вбок: трасса входит слева и выходит справа, а провал
            // в полу шириной 7 при дотяжке 6 её и гейтит. Вертикальная связь всё бы обесценила —
            // камеру обошли бы поверху, и потолок стал бы украшением (это уже ловила приёмка).
            case PuzzleElement.Vault:
                return new SpaceNeed { rooms = 2,
                                       // Комната резервируется РОВНО под картинку: и минимум, и
                                       // максимум, иначе паттерн займёт угол просторного зала.
                                       minWidth  = StencilLibrary.Vault().Width - 2,
                                       maxWidth  = StencilLibrary.Vault().Width - 2,
                                       minHeight = 3,   // коридор: два ряда воздуха над полом
                                       maxHeight = 3,
                                       horizontalEdge = true, throughCorridor = true,
                                       // Подвал: всё, что у картинки ниже пола коридора, кроме
                                       // последнего ряда — он и есть пол подвала.
                                       cellarHeight = StencilLibrary.Vault().Height - 6,
                                       holdsKey = true,
                                       // Возврат нарисован внутри паттерна — внешний выход не нужен.
                                       needsExit = StencilLibrary.Vault().needsExit };

            // ⚠️ Глубина 5 обязательна: ключ лежит в 1-2 рядах над полом камеры, то есть в H−1 рядах
            // под полом верхней комнаты. При H ≤ 4 игрок достаёт его СВЕРХУ ЧЕРЕЗ ЛЮК, не спускаясь,
            // и вся вылазка становится украшением (поймано приёмкой на сиде 16).
            case PuzzleElement.Excursion:
                return new SpaceNeed { rooms = 2, minWidth = 6, minHeight = MazeCanvas.Climb + 2,
                                       horizontalEdge = true, deadEnd = true,
                                       noFloorBelow = true, holdsKey = true };
        }
        return new SpaceNeed { rooms = 1, minWidth = 3, minHeight = 3 };
    }

    /// <summary>
    /// ⭐ БАЛЛЫ СЛОЖНОСТИ элемента. Ими задаётся уровень: генератор сам подбирает рецепт под нужную
    /// сумму (предложение игрока 2026-09-02). Раньше сложность выражалась числом механизмов, а это
    /// врало: трое ворот подряд и одна вылазка — совсем не одно и то же.
    ///
    /// Чем меряем: сколько игроку надо ПОНЯТЬ и сколько удержать в голове одновременно.
    ///   • ворота — базовый кирпич, понимается с одного экрана;
    ///   • дверь — самозакрывающаяся, добавляет тайминг и «закрылось за спиной»;
    ///   • мост — бег на время над пропастью, цена ошибки — падение;
    ///   • вложенная кнопка — надо догадаться, что призрачная кнопка оживает от ЧУЖОЙ платформы,
    ///     плюс жёсткий порядок действий;
    ///   • вылазка — намеренное смертельное падение, путь в один конец, вложенная кнопка и выход
    ///     через стену: четыре идеи сразу.
    /// ⚠️ Числа — не замер, а первая калибровка «на глаз». Уточнять по плейтестам.
    /// </summary>
    /// <summary>
    /// ⭐ КТО УМЕЕТ БЫТЬ ВЫХОДОМ из замкнутой области — то есть вернуть игрока к маршруту оттуда,
    /// откуда он пришёл в одну сторону. Требование объявляет элемент (<see cref="SpaceNeed.needsExit"/>),
    /// а исполнителя выбирают ЗДЕСЬ — это единственное место, куда дописывается новый умелец.
    ///
    /// Сегодня их двое:
    ///   • ДВЕРЬ-ИНВЕРСИЯ — стена, твёрдая в покое, кнопка изнутри её убирает (идиома D и E у игрока);
    ///   • МОСТ НА ТАЙМЕРЕ — возвращает через пропасть, тоже «нажал и успей».
    /// ⚠️ Ворота выходом не считаются: они дают ПОДЪЁМ, а из ямы наверх подъём и так закрыт — иначе
    /// область не была бы замкнутой.
    /// </summary>
    public static bool CanBeExit(PuzzleElement e)
        => e == PuzzleElement.Door || e == PuzzleElement.Bridge;

    public static int Difficulty(PuzzleElement e)
    {
        switch (e)
        {
            case PuzzleElement.Gate:       return 2;
            case PuzzleElement.Door:       return 3;
            case PuzzleElement.Bridge:     return 4;
            case PuzzleElement.NestedGate: return 5;
            case PuzzleElement.Excursion:  return 9;
            // Три группы и вложенная кнопка внутри: дороже вложенных ворот, дешевле вылазки.
            case PuzzleElement.Vault:      return 7;
        }
        return 2;
    }

    public static string Name(PuzzleElement e)
    {
        switch (e)
        {
            case PuzzleElement.Gate:       return "ворота";
            case PuzzleElement.Door:       return "дверь";
            case PuzzleElement.Bridge:     return "мост";
            case PuzzleElement.NestedGate: return "ворота с вложенной кнопкой";
            case PuzzleElement.Excursion:  return "вылазка";
            case PuzzleElement.Vault:      return "камера с ключом";
        }
        return e.ToString();
    }
}

/// <summary>Один маршрут рецепта: основной путь к финишу либо ветка к ключу.</summary>
public class RouteSpec
{
    public string name;
    public bool isMain;
    public List<PuzzleElement> elements = new List<PuzzleElement>();

    /// <summary>Сколько комнат маршруту нужно: сами элементы плюс по комнате-развязке между ними,
    /// чтобы механизмы не лезли друг другу в клетки.</summary>
    public int RoomsNeeded
    {
        get
        {
            int n = isMain ? 2 : 1;                       // старт и финиш (у ветки — только стык)
            foreach (var e in elements) n += PuzzleVocabulary.Need(e).rooms;
            n += Mathf.Max(0, elements.Count - 1);        // развязки
            return n;
        }
    }
}

/// <summary>
/// ⭐ РЕЦЕПТ УРОВНЯ — «из чего он состоит», решённое ДО всякой геометрии.
///
/// Предложение игрока (2026-09-02), и оно точнее того, что я делал: я чинил ЧАСТОТУ ВЫПАДЕНИЯ
/// паттернов, а управлять надо СОСТАВОМ. Рецепт задаёт состав по маршрутам:
///   основной маршрут:   ворота ×3, мост ×1
///   ветка к ключу 1:    вылазка
///   ветка к ключу 2:    дверь, ворота с вложенной кнопкой
/// и уже под него строятся дерево комнат и их размеры. Разнообразие берётся из РАЗНЫХ РЕЦЕПТОВ,
/// а не из разных бросков одной и той же геометрии.
///
/// ⚠️ Размер сетки — СЛЕДСТВИЕ рецепта, а не настройка: «не помещается — берём больше»
/// (решение игрока). Молча выдавать «что вышло» нельзя.
/// </summary>
public class LevelRecipe
{
    public List<RouteSpec> routes = new List<RouteSpec>();

    public int RoomsNeeded
    {
        get { int n = 0; foreach (var r in routes) n += r.RoomsNeeded; return n; }
    }

    /// <summary>Сколько всего механизмов закажет рецепт (вылазка считается за три группы).</summary>
    public int GroupsNeeded
    {
        get
        {
            int n = 0;
            foreach (var r in routes)
            foreach (var e in r.elements)
                n += e == PuzzleElement.Excursion ? 3 : 1;
            return n;
        }
    }

    /// <summary>Длина обязательной цепочки: столько механизмов основного маршрута идут по порядку.
    /// Мост в счёт не идёт — он ограничивает временем, а не доступом.</summary>
    public int MainChain
    {
        get
        {
            foreach (var r in routes)
            {
                if (!r.isMain) continue;
                int n = 0;
                foreach (var e in r.elements) if (e != PuzzleElement.Bridge) n++;
                return n;
            }
            return 0;
        }
    }

    /// <summary>
    /// ⭐ СЛОЖНОСТЬ РЕЦЕПТА В БАЛЛАХ = сумма элементов + надбавка за ЦЕПОЧКУ.
    /// Надбавка обязательна: три механизма вразнобой и три подряд — разная задача, во втором случае
    /// игрок держит в голове весь порядок. Без неё «сложность» опять свелась бы к количеству.
    /// </summary>
    public int Points
    {
        get
        {
            int p = 0;
            foreach (var r in routes) foreach (var e in r.elements) p += PuzzleVocabulary.Difficulty(e);
            p += 2 * Mathf.Max(0, MainChain - 1);
            return p;
        }
    }

    /// <summary>Сколько элементов в рецепте (вылазка — ОДИН элемент, хоть и три группы).</summary>
    public int ElementCount
    { get { int n = 0; foreach (var r in routes) n += r.elements.Count; return n; } }

    /// <summary>Максимум из требований элементов — нижняя граница для размеров комнат.</summary>
    public int MinRoomWidth
    {
        get { int w = 3; foreach (var r in routes) foreach (var e in r.elements)
              w = Mathf.Max(w, PuzzleVocabulary.Need(e).minWidth); return w; }
    }

    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < routes.Count; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(routes[i].name).Append(": ");
            if (routes[i].elements.Count == 0) sb.Append("пусто");
            for (int j = 0; j < routes[i].elements.Count; j++)
            {
                if (j > 0) sb.Append(", ");
                sb.Append(PuzzleVocabulary.Name(routes[i].elements[j]));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Случайный рецепт заданной насыщенности. <paramref name="mechanisms"/> — сколько механизмов
    /// хочется на уровень, <paramref name="chain"/> — какой длины обязательная цепочка.
    ///
    /// ⚠️ Вложенная кнопка не бывает первой в маршруте: ей нужен предыдущий механизм с платформой.
    /// ⚠️ Вылазка занимает ветку целиком — она и есть ветка (вход провалом, выход стеной).
    /// </summary>
    /// <summary>
    /// ⭐ РЕЦЕПТ ПОД ЗАДАННУЮ СЛОЖНОСТЬ (предложение игрока): состав случайный, но сумма баллов
    /// попадает в цель. Так один и тот же слот пака каждый раз выглядит по-новому, оставаясь ровно
    /// той же сложности — то, чего нельзя было добиться, задавая «число механизмов».
    ///
    /// <paramref name="maxGroups"/> — потолок по числу ГРУПП, и он не про дизайн, а про цену
    /// приёмки: поиск идёт по 2^групп состояний, и на каждую группу гоняется ещё раз. Замер: 4 группы
    /// — 283 мс на схему, 6 — 752 мс, 8 — больше двадцати секунд. Поэтому сложность может ЗАПРОСИТЬ
    /// больше, чем мы способны проверить, и тогда честнее недобрать баллов, чем выдать непроверенное.
    /// </summary>
    /// <param name="minElements">
    /// ⭐⭐ НИЖНЯЯ ГРАНИЦА ПО ЧИСЛУ МЕХАНИЗМОВ. Баллы и счёт — РАЗНЫЕ вещи, и их путали: одна вылазка
    /// стоит 9 баллов, то есть почти половину бюджета в двадцать, и уровень выходит богатый по
    /// сложности, но с одним видимым механизмом.
    ///
    /// 🐞 Замер (сложность 20, потолок 8, 25 принятых уровней): механизмов в среднем 3.76, но
    /// распределение 1→1, 2→3, 3→5, 4→10, 5→4, 6→2 — то есть каждый четвёртый уровень выходил с
    /// одним-двумя. Игрок это и увидел: «механизмов до сих пор практически никогда особо не
    /// ставится». Средним такое не лечится — нужна именно граница снизу.
    ///
    /// Добор идёт САМЫМИ ДЕШЁВЫМИ элементами (ворота), потому что задача добора — счёт, а не баллы;
    /// перелёт по сложности здесь осознан и лучше пустого уровня. Ограничение сверху прежнее —
    /// потолок групп, то есть цена проверки.
    /// </param>
    public static LevelRecipe RollForDifficulty(System.Random rng, int targetPoints,
                                                int maxGroups, bool allowExcursion, int minElements = 0)
    {
        var rec = new LevelRecipe();
        var main = new RouteSpec { name = "основной маршрут", isMain = true };
        rec.routes.Add(main);

        // Первое звено всегда ворота: это база механики, и следующему может понадобиться хозяин
        // с платформой (вложенная кнопка на дверь не садится).
        main.elements.Add(PuzzleElement.Gate);
        int groups = 1;

        // ── Набираем баллы, пока не дотянем до цели и пока хватает бюджета групп ──
        // Порядок предпочтений случайный: именно отсюда берётся непохожесть уровней одной сложности.
        int guard = 0;
        while (rec.Points < targetPoints && groups < maxGroups && guard++ < 40)
        {
            int left = targetPoints - rec.Points;
            // ⚠️ Куда пойдёт элемент, решаем ДО выбора: на основном маршруте он стоит дороже на
            // надбавку за цепочку. Без этого подбор систематически перелетал цель на пару баллов.
            bool toBranchNow = main.elements.Count >= 2 && rng.Next(100) < 35;
            int chainSurcharge = toBranchNow ? 0 : 2;
            System.Func<PuzzleElement, int> cost = el =>
                PuzzleVocabulary.Difficulty(el) + (el == PuzzleElement.Bridge || el == PuzzleElement.Excursion ? 0 : chainSurcharge);

            var pick = new List<PuzzleElement>();
            // Элемент годится, если не перелетает цель больше чем на 2 балла.
            if (allowExcursion && groups + 3 <= maxGroups && cost(PuzzleElement.Excursion) <= left + 2)
                pick.Add(PuzzleElement.Excursion);
            // ⚠️ Камера съедает ПЯТЬ групп разом (пол трассы, полка, две стенки шахты, стенка колодца).
            if (groups + 5 <= maxGroups && cost(PuzzleElement.Vault) <= left + 2)
                pick.Add(PuzzleElement.Vault);
            if (cost(PuzzleElement.NestedGate) <= left + 2) pick.Add(PuzzleElement.NestedGate);
            if (cost(PuzzleElement.Door)       <= left + 2) pick.Add(PuzzleElement.Door);
            if (cost(PuzzleElement.Bridge)     <= left + 2) pick.Add(PuzzleElement.Bridge);
            if (cost(PuzzleElement.Gate)       <= left + 2) pick.Add(PuzzleElement.Gate);
            if (pick.Count == 0) break;                       // ближе к цели уже не подойти
            var e = pick[rng.Next(pick.Count)];

            // ⭐⭐ КАМЕРА ИДЁТ НА ОСНОВНОЙ МАРШРУТ, а не в ветку. Она ПРОЛЁТ ТРАССЫ: игрок обязан
            // пройти по её коридору, а пол коридора в середине держится на исчезающей платформе.
            // 🐞 Пока камера была веткой, её потолок ничего не гейтил — ветка вела в тупик, и
            // приёмка справедливо звала группу холостой. В ручном Level_11 всё наоборот: мимо
            // камеры проходит сам маршрут, а спуск за лутом — добровольная точка невозврата.
            if (e == PuzzleElement.Vault)
            {
                main.elements.Add(PuzzleElement.Vault);
                groups += 5;
                continue;
            }
            if (e == PuzzleElement.Excursion)
            {
                // Вылазка — это ВЕТКА ЦЕЛИКОМ: вход провалом, выход стеной, ключ внутри.
                rec.routes.Add(new RouteSpec { name = "ветка к ключу " + rec.routes.Count,
                                               elements = { PuzzleElement.Excursion } });
                groups += 3;
                continue;
            }
            // Мост не даёт яруса, поэтому его место — основной маршрут, где он фактура, а не замок.
            // Остальное распределяем: цепочку растим на основном, ветки набираем отдельно.
            bool toBranch = toBranchNow && e != PuzzleElement.Bridge;
            if (toBranch)
            {
                RouteSpec br = null;
                foreach (var r in rec.routes)
                    if (!r.isMain && r.elements.Count < 2 && r.elements[0] != PuzzleElement.Excursion) { br = r; break; }
                if (br == null)
                {
                    br = new RouteSpec { name = "ветка к ключу " + rec.routes.Count };
                    rec.routes.Add(br);
                }
                // ⚠️ Вложенной кнопке нужен предыдущий механизм С ПЛАТФОРМОЙ в том же маршруте.
                if (e == PuzzleElement.NestedGate && br.elements.Count == 0) e = PuzzleElement.Gate;
                br.elements.Add(e);
            }
            else main.elements.Add(e);
            groups++;
        }

        // ── ДОБОР ДО МИНИМУМА ПО СЧЁТУ (см. minElements) ──────────────────────────────────────
        // Баллы уже набраны, но механизмов могло выйти мало — добираем самыми дешёвыми воротами,
        // пока хватает потолка групп. Сложность при этом перелетает заказ, и это осознанно: пустой
        // уровень хуже слегка переусложнённого.
        {
            int guard2 = 0;
            while (guard2++ < 40 && groups < maxGroups)
            {
                int have = 0;
                foreach (var r in rec.routes) have += r.elements.Count;
                if (have >= minElements) break;
                // Кладём по очереди в маршрут и в ветку, чтобы добор не вытягивался в одну кишку.
                RouteSpec target = main;
                if (have % 2 == 1)
                {
                    foreach (var r in rec.routes)
                        if (!r.isMain && r.elements.Count < 2
                            && r.elements.Count > 0 && r.elements[0] != PuzzleElement.Excursion)
                        { target = r; break; }
                    if (target == main)
                    {
                        target = new RouteSpec { name = "ветка к ключу " + rec.routes.Count };
                        rec.routes.Add(target);
                    }
                }
                target.elements.Add(PuzzleElement.Gate);
                groups++;
            }
        }
        return rec;
    }

    /// <summary>Элемент рецепта, ПРИВЯЗАННЫЙ к месту: конкретное ребро или комната дерева.</summary>
    public class RoleSlot
    {
        public PuzzleElement element;
        public Vector2Int roomA;          // для ребра — ближняя к старту комната; для вылазки — камера
        public Vector2Int roomB;          // дальняя комната (для вылазки — комната сверху)
        public string route;              // из какого маршрута рецепта
        public override string ToString()
            => PuzzleVocabulary.Name(element) + " " + roomA + "→" + roomB;
    }

    /// <summary>
    /// ⭐ НАЗНАЧЕНИЕ РОЛЕЙ: рецепт превращается из заказа в НАРЯД — каждый элемент получает конкретное
    /// ребро дерева. Делается СРАЗУ ПОСЛЕ постройки дерева и ДО выбора размеров комнат, чтобы размеры
    /// можно было выдать под роли, а не надеяться на удачу.
    ///
    /// Работает на голом дереве: ни одной клетки, ни одного тайла. Возвращает то, что удалось
    /// разместить — недобор честнее молчаливой подмены.
    /// </summary>
    public static List<RoleSlot> AssignRoles(LevelRecipe recipe, int CW, int CH,
                                             System.Func<int, int, bool> alive,
                                             System.Func<int, int, bool> hPass,
                                             System.Func<int, int, bool> vPass,
                                             Vector2Int start, Vector2Int finish)
    {
        System.Func<Vector2Int, List<Vector2Int>> nbrs = p =>
        {
            var l = new List<Vector2Int>();
            if (p.x + 1 < CW && hPass(p.x, p.y))     l.Add(new Vector2Int(p.x + 1, p.y));
            if (p.x - 1 >= 0 && hPass(p.x - 1, p.y)) l.Add(new Vector2Int(p.x - 1, p.y));
            if (p.y + 1 < CH && vPass(p.x, p.y))     l.Add(new Vector2Int(p.x, p.y + 1));
            if (p.y - 1 >= 0 && vPass(p.x, p.y - 1)) l.Add(new Vector2Int(p.x, p.y - 1));
            return l;
        };
        // Дерево от старта
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var order = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int> { start };
        var q = new Queue<Vector2Int>(); q.Enqueue(start);
        while (q.Count > 0)
        {
            var cur = q.Dequeue(); order.Add(cur);
            foreach (var nb in nbrs(cur)) if (seen.Add(nb)) { parent[nb] = cur; q.Enqueue(nb); }
        }
        var mainPath = new List<Vector2Int>();
        { var cur = finish; mainPath.Add(cur);
          while (parent.ContainsKey(cur)) { cur = parent[cur]; mainPath.Add(cur); }
          mainPath.Reverse(); }

        var used = new HashSet<Vector2Int> { start, finish };
        var slots = new List<RoleSlot>();
        System.Func<Vector2Int, Vector2Int, bool> free = (a, b) => !used.Contains(a) && !used.Contains(b);

        // ⭐⭐ САМОЕ ТРЕБОВАТЕЛЬНОЕ РАЗМЕЩАЕТСЯ ПЕРВЫМ. 🐞 Пока роли назначались в порядке рецепта,
        // основной маршрут разбирал комнаты жадно, и вылазке — у которой условий больше всех (тупик,
        // пропасть под полом, живая комната сверху, горизонтальный выход) — места не оставалось:
        // из 12 заказов не размещалось 9. Классическая жадность наоборот.
        var routesByDemand = new List<RouteSpec>(recipe.routes);
        routesByDemand.Sort((a, b) =>
        {
            System.Func<RouteSpec, int> demand = r =>
            {
                int d = 0;
                foreach (var e in r.elements)
                {
                    var nd = PuzzleVocabulary.Need(e);
                    if (nd.deadEnd) d += 8;
                    if (nd.noFloorBelow) d += 4;
                    d += nd.minWidth + nd.minHeight;
                }
                return d;
            };
            return demand(b).CompareTo(demand(a));
        });
        foreach (var route in routesByDemand)
        {
            if (route.isMain)
            {
                int at = 0;
                foreach (var e in route.elements)
                {
                    var need = PuzzleVocabulary.Need(e);
                    bool placed = false;
                    for (int i = at; i + 1 < mainPath.Count && !placed; i++)
                    {
                        var a = mainPath[i]; var b = mainPath[i + 1];
                        if (!free(a, b)) continue;
                        bool vertical = a.x == b.x;
                        if (need.verticalEdge && !(vertical && b.y > a.y)) continue;
                        if (need.horizontalEdge && vertical) continue;
                        // Мост занимает одну комнату — сквозной коридор, а не ребро.
                        slots.Add(new RoleSlot { element = e, roomA = a, roomB = b, route = route.name });
                        used.Add(a); used.Add(b);
                        at = i + 2; placed = true;
                    }
                    if (!placed) break;                       // дальше по этому маршруту тоже не влезет
                }
            }
            else
            {
                // Ветка: ищем тупик подальше от старта, ещё не занятый.
                foreach (var e in route.elements)
                {
                    var need = PuzzleVocabulary.Need(e);
                    Vector2Int best = new Vector2Int(-1, -1), bestUp = new Vector2Int(-1, -1);
                    foreach (var r in order)
                    {
                        if (used.Contains(r)) continue;
                        if (need.deadEnd && nbrs(r).Count != 1) continue;
                        if (need.noFloorBelow && r.y > 0 && alive(r.x, r.y - 1)) continue;
                        Vector2Int p;
                        if (!parent.TryGetValue(r, out p) || used.Contains(p)) continue;
                        if (need.horizontalEdge && p.x == r.x) continue;
                        if (need.verticalEdge && !(p.x == r.x && r.y > p.y)) continue;
                        // Вылазке нужна ещё и живая комната СВЕРХУ — оттуда игрок падает.
                        if (e == PuzzleElement.Excursion)
                        {
                            if (r.y + 1 >= CH || !alive(r.x, r.y + 1) || used.Contains(new Vector2Int(r.x, r.y + 1))) continue;
                            bestUp = new Vector2Int(r.x, r.y + 1);
                        }
                        best = r; break;
                    }
                    if (best.x < 0) break;
                    var other = e == PuzzleElement.Excursion ? bestUp : parent[best];
                    slots.Add(new RoleSlot { element = e, roomA = best, roomB = other, route = route.name });
                    used.Add(best); used.Add(other);
                }
            }
        }
        return slots;
    }

    public static LevelRecipe Roll(System.Random rng, int mechanisms, int chain, bool allowExcursion)
    {
        var rec = new LevelRecipe();
        int budget = Mathf.Max(1, mechanisms);

        // ── Основной маршрут: на нём и строится цепочка ──
        var main = new RouteSpec { name = "основной маршрут", isMain = true };
        int chainLen = Mathf.Clamp(chain, 1, budget);
        for (int i = 0; i < chainLen; i++)
        {
            // Первое звено — всегда с платформой: следующему может понадобиться хозяин.
            if (i == 0) main.elements.Add(PuzzleElement.Gate);
            else if (rng.Next(100) < 40) main.elements.Add(PuzzleElement.NestedGate);
            else if (rng.Next(100) < 35) main.elements.Add(PuzzleElement.Door);
            else main.elements.Add(PuzzleElement.Gate);
        }
        budget -= main.elements.Count;
        if (budget > 0 && rng.Next(100) < 40) { main.elements.Add(PuzzleElement.Bridge); budget--; }
        rec.routes.Add(main);

        // ── Ветки к ключам ──
        int branchNo = 1;
        while (budget > 0 && branchNo <= 2)
        {
            var br = new RouteSpec { name = "ветка к ключу " + branchNo };
            if (allowExcursion && budget >= 3 && rng.Next(100) < 45)
            { br.elements.Add(PuzzleElement.Excursion); budget -= 3; }
            else
            {
                br.elements.Add(rng.Next(100) < 50 ? PuzzleElement.Door : PuzzleElement.Gate);
                budget--;
                if (budget > 0 && rng.Next(100) < 35) { br.elements.Add(PuzzleElement.NestedGate); budget--; }
            }
            rec.routes.Add(br);
            branchNo++;
        }
        return rec;
    }
}
