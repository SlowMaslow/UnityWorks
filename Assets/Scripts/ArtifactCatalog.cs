using UnityEngine;

/// <summary>
/// ⭐ СКОЛЬКО АРТЕФАКТОВ В ПАКЕ И СКОЛЬКО ИЗ НИХ УЖЕ ДОБЫТО — одно место на всю игру.
///
/// Зачем отдельный класс. Знаменатель «X из N» нужен ТРЁМ разным экранам: финалке уровня, экрану
/// коллекции и тизеру в меню. Считать его на месте каждый раз нельзя: число артефактов уровня живёт
/// в самом префабе (<see cref="Artifact"/> на сцене уровня), и чтобы его узнать, надо грузить префаб
/// из Resources. Делать это на каждый кадр перерисовки мозаики — чистая растрата.
///
/// ⚠️ ЗНАМЕНАТЕЛЬ СЧИТАЕТСЯ ПО ПРЕФАБАМ, А НЕ ЗАДАЁТСЯ ЧИСЛОМ. Дизайн прямо говорит: артефактов
/// «3/уровень (data-driven, число варьируется по мирам)». Константа тут рассинхронизировалась бы с
/// контентом при первой же правке уровня, и мозаика показывала бы «37 из 36».
/// На сегодня в паке 12 уровней по 3 артефакта = 36, что ровно ложится в сетку 6×6.
/// </summary>
public static class ArtifactCatalog
{
    /// <summary>Первый уровень пака (1-based, как везде в проекте).</summary>
    public const int FirstLevel = 1;

    private static int[] _perLevel;      // сколько артефактов лежит на уровне (индекс = уровень − 1)
    private static int   _total = -1;

    /// <summary>Сколько артефактов НА УРОВНЕ (по префабу). 0, если уровня нет.</summary>
    public static int TotalOn(int levelIndex)
    {
        EnsureScanned();
        int i = levelIndex - FirstLevel;
        return i >= 0 && i < _perLevel.Length ? _perLevel[i] : 0;
    }

    /// <summary>Сколько артефактов в паке ВСЕГО — знаменатель картинки-мира.</summary>
    public static int PackTotal { get { EnsureScanned(); return _total; } }

    /// <summary>Последний уровень пака.</summary>
    public static int LastLevel { get { EnsureScanned(); return FirstLevel + _perLevel.Length - 1; } }

    /// <summary>Сколько артефактов игрок уже занёс в коллекцию (банк = только за прохождение).</summary>
    public static int PackCollected => SaveSystem.GetPackArtifacts(FirstLevel, LastLevel);

    /// <summary>Собрана ли картинка целиком.</summary>
    public static bool PackComplete => PackTotal > 0 && PackCollected >= PackTotal;

    /// <summary>
    /// ⚠️ СБРОСИТЬ КЭШ. Нужен редактору: уровни правятся прямо в проекте, и после добавления
    /// артефакта знаменатель обязан перечитаться без перезапуска.
    /// </summary>
    public static void Invalidate() { _perLevel = null; _total = -1; }

    private static void EnsureScanned()
    {
        if (_perLevel != null) return;

        int levels = LevelLoader.TotalLevels;
        _perLevel = new int[Mathf.Max(0, levels)];
        _total = 0;
        for (int i = 0; i < _perLevel.Length; i++)
        {
            var prefab = Resources.Load<GameObject>(
                $"{LevelLoader.ResourcesPath}/{LevelLoader.PrefabName(FirstLevel + i)}");
            // true — считаем и выключенные: артефакт гасится при подборе, но в ПРЕФАБЕ он активен;
            // флаг стоит на случай, если уровень собран с заранее выключенными объектами.
            _perLevel[i] = prefab != null ? prefab.GetComponentsInChildren<Artifact>(true).Length : 0;
            _total += _perLevel[i];
        }
    }
}
