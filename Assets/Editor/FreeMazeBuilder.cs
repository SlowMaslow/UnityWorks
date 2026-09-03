using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ СБОРКА УРОВНЯ БЕЗ РЕШЁТКИ: дерево комнат → раскладка прямоугольников → сетка символов.
///
/// Строится РЯДОМ со старым генератором, а не вместо него: старый работает и отдаёт чистые уровни,
/// и ломать его до того, как новый путь себя покажет, — плохая сделка. Переключение произойдёт, когда
/// новый путь пройдёт те же замеры (брак на выходе, сцепление, время).
///
/// Порядок обратный прежнему и в этом весь смысл:
///   1. РЕЦЕПТ — из чего уровень состоит (баллы сложности → состав);
///   2. ДЕРЕВО — основной путь нужной длины плюс ветки под элементы рецепта;
///   3. РОЛИ — каждый элемент садится на конкретное ребро и диктует ему НАПРАВЛЕНИЕ
///      (ворота требуют подъёма, дверь — бокового ребра);
///   4. РАСКЛАДКА — каждая комната получает свой прямоугольник нужного размера (<see cref="RoomLayout"/>);
///   5. РЕНДЕР — камень, полости комнат, проходы по связям.
/// Раньше геометрия бросалась вслепую, а замысел подбирал, что выпало.
/// </summary>
public static class FreeMazeBuilder
{
    /// <summary>Комната абстрактного дерева: кто родитель и какой элемент рецепта сидит на её ребре.</summary>
    public class Node
    {
        public int id, parent = -1;
        public PuzzleElement? element;     // элемент на ребре «родитель → эта комната»
        public string route;
        public bool onMainPath;
    }

    public class Built
    {
        public string scheme;
        public List<Node> nodes;
        public RoomLayout.Result layout;
        public string story;               // человекочитаемо: что и куда легло
    }

    /// <summary>
    /// Построить дерево под рецепт. Основной путь — цепочка комнат, на рёбрах которой сидят элементы
    /// основного маршрута; между соседними элементами вставляется пустая комната-развязка, чтобы
    /// механизмы не лезли друг другу в клетки. Ветки подвешиваются к случайным комнатам основного пути.
    /// </summary>
    public static List<Node> BuildTree(LevelRecipe recipe, System.Random rng)
    {
        var nodes = new List<Node>();
        System.Func<int, PuzzleElement?, string, bool, int> add = (parent, el, route, main) =>
        {
            var n = new Node { id = nodes.Count, parent = parent, element = el, route = route, onMainPath = main };
            nodes.Add(n); return n.id;
        };

        add(-1, null, "старт", true);                       // комната спавна
        var spine = new List<int> { 0 };
        foreach (var route in recipe.routes)
        {
            if (!route.isMain) continue;
            for (int i = 0; i < route.elements.Count; i++)
            {
                // Комната-развязка между механизмами: два подряд в соседних комнатах не помещаются.
                if (i > 0) spine.Add(add(spine[spine.Count - 1], null, route.name, true));
                spine.Add(add(spine[spine.Count - 1], route.elements[i], route.name, true));
            }
            spine.Add(add(spine[spine.Count - 1], null, route.name, true));   // комната финиша
        }

        foreach (var route in recipe.routes)
        {
            if (route.isMain) continue;
            // Ветка цепляется к случайной комнате основного пути, кроме самой первой и последней:
            // у спавна и финиша своя работа, механизмы туда не ставим.
            int attach = spine[1 + rng.Next(Mathf.Max(1, spine.Count - 2))];
            int cur = attach;
            foreach (var el in route.elements) cur = add(cur, el, route.name, false);
        }
        return nodes;
    }

    /// <summary>Требования и направления для раскладки — прямо из деклараций словаря.</summary>
    public static void Requirements(List<Node> nodes, out RoomLayout.RoomReq[] req, out RoomLayout.LinkDir[] dir)
    {
        int n = nodes.Count;
        req = new RoomLayout.RoomReq[n];
        dir = new RoomLayout.LinkDir[n];
        for (int i = 0; i < n; i++) { req[i] = new RoomLayout.RoomReq(); dir[i] = RoomLayout.LinkDir.Any; }
        foreach (var nd in nodes)
        {
            if (nd.element == null) continue;
            var need = PuzzleVocabulary.Need(nd.element.Value);
            // ⭐ Требование к месту берётся ИЗ СЛОВАРЯ и применяется к ОБЕИМ комнатам ребра:
            // механизм живёт между ними, и тесной не должна быть ни та, ни другая.
            foreach (int id in new[] { nd.id, nd.parent })
            {
                if (id < 0) continue;
                req[id].minW = Mathf.Max(req[id].minW, need.minWidth);
                req[id].minH = Mathf.Max(req[id].minH, Mathf.Min(need.minHeight, RoomLayout.MaxRoomHeight));
            }
            if (need.verticalEdge) dir[nd.id] = RoomLayout.LinkDir.Up;
            else if (need.horizontalEdge) dir[nd.id] = RoomLayout.LinkDir.Horizontal;
        }
    }

    /// <summary>
    /// Рендер раскладки в сетку символов: сплошной камень, в нём вырезаются полости комнат и проходы
    /// по связям. Дверной проём режется от пола вверх (низкий, как и на решётке), люк — во всю ширину
    /// общего отрезка.
    /// ⚠️ Сетка символов растёт СВЕРХУ ВНИЗ, а раскладка считает Y вверх — перевод здесь, в одном месте.
    /// </summary>
    public static char[,] Render(RoomLayout.Result lay, System.Random rng, out int rows, out int cols)
    {
        int maxX = 0, maxY = 0;
        foreach (var r in lay.rooms) { maxX = Mathf.Max(maxX, r.X1); maxY = Mathf.Max(maxY, r.Y1); }
        cols = maxX + 4; rows = maxY + 4;
        var g = new char[rows, cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) g[r, c] = '#';
        int rowsLocal = rows;
        System.Func<int, int> toRow = y => rowsLocal - 1 - y;      // мир (Y вверх) → строка сетки

        foreach (var rm in lay.rooms)
            for (int y = rm.y0; y <= rm.Y1; y++)
            for (int x = rm.x0; x <= rm.X1; x++)
                g[toRow(y), x] = '.';

        foreach (var l in lay.links)
        {
            if (l.vertical)
            {
                // Люк во всю ширину общего отрезка — по нему и лезут наверх.
                for (int x = l.from; x <= l.to; x++) g[toRow(l.wall), x] = '.';
            }
            else
            {
                // Дверной проём: от пола вверх, высотой 2..вся общая часть — как было на решётке.
                int h = Mathf.Min(l.to - l.from + 1, 2 + rng.Next(2));
                for (int k = 0; k < h; k++) g[toRow(l.from + k), l.wall] = '.';
            }
        }
        return g;
    }

    /// <summary>Полный проход: рецепт → дерево → роли → раскладка → сетка. null, если не уложилось.</summary>
    public static Built Build(LevelRecipe recipe, System.Random rng)
    {
        var nodes = BuildTree(recipe, rng);
        RoomLayout.RoomReq[] req; RoomLayout.LinkDir[] dir;
        Requirements(nodes, out req, out dir);
        var parent = new int[nodes.Count];
        for (int i = 0; i < nodes.Count; i++) parent[i] = nodes[i].parent;

        var lay = RoomLayout.Build(nodes.Count, parent, req, dir, rng);
        if (lay == null) return null;

        int rows, cols;
        var g = Render(lay, rng, out rows, out cols);
        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < rows; r++) { for (int c = 0; c < cols; c++) sb.Append(g[r, c]); sb.Append('\n'); }

        var story = new System.Text.StringBuilder();
        story.Append("комнат ").Append(nodes.Count).Append(", габарит ").Append(cols).Append('×').Append(rows).Append(": ");
        foreach (var nd in nodes)
            if (nd.element != null)
                story.Append(PuzzleVocabulary.Name(nd.element.Value)).Append(" на ребре ")
                     .Append(nd.parent).Append("→").Append(nd.id).Append("; ");
        return new Built { scheme = sb.ToString(), nodes = nodes, layout = lay, story = story.ToString() };
    }
}
