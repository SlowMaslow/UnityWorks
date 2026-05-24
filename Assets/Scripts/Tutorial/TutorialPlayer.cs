using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Универсальный исполнитель TutorialSequence.
    /// Не знает ни о пэдах, ни о чекпоинтах, ни о троллинге — всё в данных.
    /// UI собирается кодом (можно вынести в префаб позже, но в Phase B это не цель).
    /// </summary>
    public class TutorialPlayer : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField] private TutorialSequence sequence;

        [Tooltip("Если задано — туториал стартует с шага с этим id (полезно для прыжков через jumpToStepId).")]
        [SerializeField] private string startAtStepId;

        [Tooltip("Сразу пропустить туториал если completeFlag уже выставлен")]
        [SerializeField] private bool respectCompleteFlag = true;

        [Header("UI Style")]
        [SerializeField] private string  mainCanvasName  = "Canvas";
        [SerializeField] private Vector2 panelSize        = new Vector2(360f, 85f);
        [SerializeField] private Vector2 panelAnchoredPos = new Vector2(-20f, 20f);
        [SerializeField] private Color   panelBgColor     = new Color(0.06f, 0.03f, 0.18f, 0.88f);
        [SerializeField] private Color   hintTextColor    = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private int     hintFontSize     = 22;
        [SerializeField] private int     dotsFontSize     = 11;
        [SerializeField] private float   checkpointRingSize = 110f;
        [SerializeField] private int     checkpointFontSize = 86;

        [Header("Animation")]
        [SerializeField] private float pulseScaleUpDuration   = 0.6f;
        [SerializeField] private float pulseScaleDownDuration = 0.4f;
        [SerializeField] private float pulseMaxScale          = 1.35f;
        [SerializeField] private float dismissFadeDuration    = 0.4f;

        // ─── Runtime state ───────────────────────────────────────────────────
        private int       _currentIndex = -1;
        private TutorialStep _currentStep;
        private float     _autoAdvanceAt = -1f;
        private bool      _running;

        // Последовательное выполнение actions
        private int                                              _actionIndex   = 0;
        private System.Collections.Generic.List<TutorialAction>  _enteredActions
            = new System.Collections.Generic.List<TutorialAction>();

        // UI
        private GameObject    _panel;
        private Text          _hintText, _subText, _stepDots;
        private Canvas        _canvas;
        private Canvas        _ringCanvas;
        private Camera        _cam;
        // Активные подсветки (UI-элементы + цель из TutorialTarget)
        private System.Collections.Generic.List<ActiveHighlight> _activeHighlights
            = new System.Collections.Generic.List<ActiveHighlight>();

        private class ActiveHighlight
        {
            public StepHighlight cfg;
            public RectTransform rect;
            public Coroutine     pulse;
        }

        // ─── Unity ───────────────────────────────────────────────────────────
        private void Start()
        {
            // На всякий случай сбрасываем гейт ввода (статика могла остаться с прошлой сессии)
            TutorialInputGate.ResetAll();

            if (sequence == null) { enabled = false; return; }
            if (respectCompleteFlag && IsAlreadyComplete(sequence))
            {
                enabled = false;
                return;
            }

            _cam = Camera.main;
            var canvasGO = GameObject.Find(mainCanvasName);
            _canvas = canvasGO?.GetComponent<Canvas>() ?? FindFirstObjectByType<Canvas>();

            StartCoroutine(StartDeferred());
        }

        private IEnumerator StartDeferred()
        {
            // Один кадр — чтобы LevelManager инстанциировал уровень и TutorialTarget'ы зарегистрировались
            yield return null;

            _ringCanvas = CreateRingCanvas();
            BuildPanelUI();

            int startIdx = 0;
            if (!string.IsNullOrEmpty(startAtStepId))
            {
                int idx = sequence.FindIndexById(startAtStepId);
                if (idx >= 0) startIdx = idx;
            }

            _running = true;
            EnterStep(startIdx);
        }

        private void Update()
        {
            if (!_running || _currentStep == null) return;

            float dt = Time.unscaledDeltaTime;
            _currentStep.condition?.Tick(dt);

            // Тикаем активный action и продвигаем очередь
            if (_currentStep.actions != null && _enteredActions.Count > 0)
            {
                var current = _enteredActions[_enteredActions.Count - 1];
                if (current != null && !current.IsDone)
                    current.Tick(dt);
                else
                {
                    _actionIndex++;
                    TryEnterNextAction();
                }
            }

            if (_autoAdvanceAt > 0 && Time.unscaledTime >= _autoAdvanceAt)
                Advance();

            // Подсветки следуют за целями (с учётом offset на TutorialTarget)
            foreach (var h in _activeHighlights)
            {
                var tt = TutorialTarget.FindComponent(h.cfg.targetId);
                if (tt != null) MoveRingToWorld(h.rect, tt.WorldPosition);
            }
        }

        private void OnDestroy()
        {
            ExitCurrentStep();
            TutorialInputGate.ResetAll();
            if (CameraController.Instance != null)
                CameraController.Instance.ClearTemporaryTarget(0f);
            if (_panel)      Destroy(_panel);
            if (_ringCanvas) Destroy(_ringCanvas.gameObject);
        }

        // ─── Step lifecycle ──────────────────────────────────────────────────
        private void EnterStep(int index)
        {
            ExitCurrentStep();

            // Пропускаем шаги с поднятым skip-флагом
            while (index >= 0 && index < sequence.steps.Length)
            {
                var s = sequence.steps[index];
                if (s != null && !string.IsNullOrEmpty(s.skipIfFlagSet) && PlayerPrefs.HasKey(s.skipIfFlagSet))
                {
                    index++;
                    continue;
                }
                break;
            }

            if (index < 0 || index >= sequence.steps.Length)
            {
                Finish();
                return;
            }

            _currentIndex = index;
            _currentStep  = sequence.steps[index];

            if (!string.IsNullOrEmpty(_currentStep.setFlagOnEnter))
            {
                PlayerPrefs.SetInt(_currentStep.setFlagOnEnter, 1);
                PlayerPrefs.Save();
            }

            if (_hintText) _hintText.text = _currentStep.hintText ?? "";
            if (_subText)  _subText.text  = _currentStep.subText  ?? "";
            UpdateDots();

            // Подписываемся на condition
            if (_currentStep.condition != null)
            {
                _currentStep.condition.Reset();
                _currentStep.condition.OnCompleted += OnConditionCompleted;
                _currentStep.condition.Enter();
            }

            _autoAdvanceAt = _currentStep.autoAdvanceDelay > 0
                ? Time.unscaledTime + _currentStep.autoAdvanceDelay
                : -1f;

            ShowHighlights(_currentStep.highlights);

            // Сбрасываем очередь actions для нового шага — TryEnterNextAction
            // запустит первый action; следующий стартует в Update когда IsDone.
            _enteredActions.Clear();
            _actionIndex = 0;
            TryEnterNextAction();
        }

        /// <summary>Запускает следующий action в очереди (с пропуском null и мгновенно-завершённых).</summary>
        private void TryEnterNextAction()
        {
            if (_currentStep == null || _currentStep.actions == null) return;
            var arr = _currentStep.actions;
            while (_actionIndex < arr.Length)
            {
                var a = arr[_actionIndex];
                if (a == null) { _actionIndex++; continue; }
                a.Enter();
                _enteredActions.Add(a);
                if (!a.IsDone) return;  // ждём пока завершится через Tick
                _actionIndex++;          // мгновенный — сразу к следующему
            }
        }

        private void ExitCurrentStep()
        {
            if (_currentStep != null)
            {
                if (_currentStep.condition != null)
                {
                    _currentStep.condition.OnCompleted -= OnConditionCompleted;
                    _currentStep.condition.Exit();
                }
                // Откатываем все запущенные actions (только те у которых Enter был вызван)
                foreach (var a in _enteredActions)
                    a?.Exit();
                _enteredActions.Clear();
                _actionIndex = 0;
            }
            HideHighlights();
            _autoAdvanceAt = -1f;
        }

        private void OnConditionCompleted()
        {
            // Опционально выставляем флаг при выполнении condition (для троллинга и подобных веток)
            if (_currentStep != null && !string.IsNullOrEmpty(_currentStep.setFlagOnComplete))
            {
                PlayerPrefs.SetInt(_currentStep.setFlagOnComplete, 1);
                PlayerPrefs.Save();
            }

            // Если шаг помечен stopAfterComplete — НЕ продвигаемся.
            // Туториал просто останавливается в этой сессии; продолжение произойдёт после
            // game-restart, когда новый TutorialPlayer стартует и через skip-логику попадёт
            // на следующий валидный шаг.
            if (_currentStep != null && _currentStep.stopAfterComplete)
            {
                ExitCurrentStep();
                if (_panel) _panel.SetActive(false);
                _running     = false;
                _currentStep = null;
                return;
            }

            Advance();
        }

        private static bool IsAlreadyComplete(TutorialSequence seq)
        {
            if (!string.IsNullOrEmpty(seq.completeFlag) && PlayerPrefs.HasKey(seq.completeFlag))
                return true;
            if (seq.additionalCompleteFlags != null)
                foreach (var f in seq.additionalCompleteFlags)
                    if (!string.IsNullOrEmpty(f) && PlayerPrefs.HasKey(f)) return true;
            return false;
        }

        public void Advance() => EnterStep(_currentIndex + 1);

        /// <summary>Внешний API — для шагов с ManualAdvanceCondition (например по кнопке).</summary>
        public void AdvanceManually() => Advance();

        /// <summary>Debug: сбрасывает completeFlag + дополнительные флаги и перезапускает туториал.</summary>
        public void DebugResetTutorial()
        {
            if (sequence != null)
            {
                if (!string.IsNullOrEmpty(sequence.completeFlag))
                    PlayerPrefs.DeleteKey(sequence.completeFlag);
                if (sequence.additionalCompleteFlags != null)
                    foreach (var f in sequence.additionalCompleteFlags)
                        if (!string.IsNullOrEmpty(f)) PlayerPrefs.DeleteKey(f);
                // Также чистим все флаги, упоминающиеся в шагах (skipIfFlagSet / setFlag*)
                if (sequence.steps != null)
                    foreach (var s in sequence.steps)
                    {
                        if (s == null) continue;
                        if (!string.IsNullOrEmpty(s.skipIfFlagSet))      PlayerPrefs.DeleteKey(s.skipIfFlagSet);
                        if (!string.IsNullOrEmpty(s.setFlagOnEnter))     PlayerPrefs.DeleteKey(s.setFlagOnEnter);
                        if (!string.IsNullOrEmpty(s.setFlagOnComplete))  PlayerPrefs.DeleteKey(s.setFlagOnComplete);
                    }
            }
            PlayerPrefs.Save();

            // Перезагружаем активную сцену чтобы туториал стартовал заново
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }

        private void Finish()
        {
            _running     = false;
            _currentStep = null;
            if (sequence != null && !string.IsNullOrEmpty(sequence.completeFlag))
            {
                PlayerPrefs.SetInt(sequence.completeFlag, 1);
                PlayerPrefs.Save();
            }
            // Полностью снимаем блокировки на финале — иначе UI/ввод останутся выключенными
            TutorialInputGate.ResetAll();
            // Возвращаем камеру к игроку (на случай если последний шаг был CameraFocusAction)
            if (CameraController.Instance != null)
                CameraController.Instance.ClearTemporaryTarget(4f);
            StartCoroutine(DismissPanel());
        }

        private IEnumerator DismissPanel()
        {
            if (_panel == null) { enabled = false; yield break; }

            // Unity-null check + явное создание CanvasGroup.
            // ?? не дружит с Unity-fake-null, поэтому делаем if вручную.
            CanvasGroup group = _panel.GetComponent<CanvasGroup>();
            if (group == null) group = _panel.AddComponent<CanvasGroup>();

            if (group == null || dismissFadeDuration <= 0f)
            {
                if (_panel) _panel.SetActive(false);
                enabled = false;
                yield break;
            }

            float t = 0f;
            while (t < dismissFadeDuration)
            {
                if (group == null || _panel == null) break;
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(1f, 0f, t / dismissFadeDuration);
                yield return null;
            }

            if (_panel) _panel.SetActive(false);
            enabled = false;
        }

        // ─── Highlights ──────────────────────────────────────────────────────
        private void ShowHighlights(StepHighlight[] highlights)
        {
            if (highlights == null) return;
            foreach (var h in highlights)
            {
                if (h == null) continue;
                RectTransform rt = BuildCheckpointIndicator(h.color);
                var active = new ActiveHighlight { cfg = h, rect = rt };
                active.pulse = StartCoroutine(PulseRing(rt));
                _activeHighlights.Add(active);

                // Мгновенно поставим на цель, если она уже есть
                var target = TutorialTarget.FindComponent(h.targetId);
                if (target != null) MoveRingToWorld(rt, target.WorldPosition);
            }
        }

        private void HideHighlights()
        {
            foreach (var h in _activeHighlights)
            {
                if (h.pulse != null) StopCoroutine(h.pulse);
                if (h.rect != null) Destroy(h.rect.gameObject);
            }
            _activeHighlights.Clear();
        }

        // ─── UI build ────────────────────────────────────────────────────────
        private static Canvas CreateRingCanvas()
        {
            var go = new GameObject("TutorialRingCanvas");
            var c  = go.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 50;
            return c;
        }

        private void BuildPanelUI()
        {
            if (_canvas == null) return;

            var font    = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var fredoka = Resources.Load<Font>("../Fonts/FredokaOne-Regular") ?? font;

            _panel = new GameObject("TutorialPanel", typeof(RectTransform));
            _panel.transform.SetParent(_canvas.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchoredPosition = panelAnchoredPos;
            rt.sizeDelta = panelSize;
            var bg = _panel.AddComponent<Image>();
            bg.color = panelBgColor;

            var hGO = MkTxt("Hint", _panel.transform, fredoka);
            var hRt = hGO.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0.2f); hRt.anchorMax = new Vector2(1f, 1f);
            hRt.offsetMin = new Vector2(12f, 0f);  hRt.offsetMax = new Vector2(-12f, 0f);
            _hintText = hGO.GetComponent<Text>();
            _hintText.fontSize  = hintFontSize;
            _hintText.fontStyle = FontStyle.Bold;
            _hintText.color     = hintTextColor;
            _hintText.alignment = TextAnchor.MiddleCenter;

            var dGO = MkTxt("Dots", _panel.transform, font);
            var dRt = dGO.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0f, 0f); dRt.anchorMax = new Vector2(1f, 0.25f);
            dRt.offsetMin = dRt.offsetMax = Vector2.zero;
            _stepDots = dGO.GetComponent<Text>();
            _stepDots.fontSize  = dotsFontSize;
            _stepDots.color     = new Color(1f, 1f, 1f, 0.4f);
            _stepDots.alignment = TextAnchor.MiddleCenter;
        }

        private RectTransform BuildCheckpointIndicator(Color fillColor)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("CP", typeof(RectTransform));
            root.transform.SetParent(_ringCanvas.transform, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(checkpointRingSize, checkpointRingSize);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(root.transform, false);
            var fRt = fillGO.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.offsetMin = fRt.offsetMax = Vector2.zero;
            var fill = fillGO.AddComponent<Image>();
            fill.sprite = CreateCircleSprite(64);
            fill.color  = fillColor;

            var ringGO = new GameObject("Ring", typeof(RectTransform));
            ringGO.transform.SetParent(root.transform, false);
            var rRt = ringGO.GetComponent<RectTransform>();
            rRt.anchorMin = Vector2.zero; rRt.anchorMax = Vector2.one;
            rRt.offsetMin = rRt.offsetMax = Vector2.zero;
            var ringT = ringGO.AddComponent<Text>();
            ringT.text = "○"; ringT.font = font; ringT.fontSize = checkpointFontSize;
            ringT.fontStyle = FontStyle.Bold; ringT.alignment = TextAnchor.MiddleCenter;
            ringT.color = new Color(fillColor.r, fillColor.g, fillColor.b, 1f);

            return rt;
        }

        private void MoveRingToWorld(RectTransform ring, Vector3 worldPos)
        {
            if (ring == null || _cam == null) return;
            Vector3 sp = _cam.WorldToScreenPoint(worldPos);
            if (sp.z < 0f) { ring.gameObject.SetActive(false); return; }
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            ring.position = new Vector3(sp.x, sp.y, 0f);
        }

        private IEnumerator PulseRing(RectTransform ring)
        {
            while (ring != null && ring.gameObject.activeSelf)
            {
                float t = 0f;
                while (t < pulseScaleUpDuration)
                {
                    t += Time.unscaledDeltaTime;
                    if (ring) ring.localScale = Vector3.one * Mathf.Lerp(1f, pulseMaxScale, t / pulseScaleUpDuration);
                    yield return null;
                }
                t = 0f;
                while (t < pulseScaleDownDuration)
                {
                    t += Time.unscaledDeltaTime;
                    if (ring) ring.localScale = Vector3.one * Mathf.Lerp(pulseMaxScale, 1f, t / pulseScaleDownDuration);
                    yield return null;
                }
            }
        }

        private void UpdateDots()
        {
            if (_stepDots == null || sequence == null) return;
            int total = sequence.steps.Length;
            if (total == 0) { _stepDots.text = ""; return; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < total; i++)
            {
                sb.Append(i <= _currentIndex ? "● " : "○ ");
            }
            _stepDots.text = sb.ToString().TrimEnd();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(h, h));
                    float a = Mathf.Clamp01(1f - (d / h));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static GameObject MkTxt(string name, Transform parent, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = font; t.supportRichText = false;
            return go;
        }
    }
}
