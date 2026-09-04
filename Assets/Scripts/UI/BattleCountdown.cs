using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class BattleCountdown : MonoBehaviour
{
    private static readonly string[] PhaseLabels =
    {
        "3",
        "2",
        "1",
        "GO!"
    };

    private static readonly Color OverlayColor =
        new(0f, 0f, 0f, 0.76f);

    [Header("References")]
    [SerializeField] private MatchController matchController;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float phaseDuration = 0.5f;

    [Header("Phase Sprites")]
    [SerializeField] private Sprite threeSprite;
    [SerializeField] private Sprite twoSprite;
    [SerializeField] private Sprite oneSprite;
    [SerializeField] private Sprite goSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 imageSize = new(420f, 260f);

    private readonly List<Behaviour> suspendedInputSources = new();

    private GameObject overlayRoot;
    private Image overlayBackground;
    private Image countdownImage;
    private Text fallbackText;
    private Coroutine countdownCoroutine;
    private Action completionCallback;
    private float timeScaleBeforeCountdown = 1f;

    private static Font runtimeFont;

    public MatchController MatchController => matchController;
    public float PhaseDuration => phaseDuration;
    public Sprite ThreeSprite => threeSprite;
    public Sprite TwoSprite => twoSprite;
    public Sprite OneSprite => oneSprite;
    public Sprite GoSprite => goSprite;
    public Image OverlayBackground => overlayBackground;
    public Image CountdownImage => countdownImage;
    public Text FallbackText => fallbackText;
    public string CurrentPhaseLabel { get; private set; } = string.Empty;
    public bool IsCountingDown { get; private set; }
    public bool IsVisible => overlayRoot != null && overlayRoot.activeSelf;
    public bool AreParticipantInputsSuspended =>
        suspendedInputSources.Count > 0;

    private void Awake()
    {
        ResolveMatchController();
        EnsureBuilt();
        HideOverlay();
    }

    private void OnDisable()
    {
        CancelCountdown();
    }

    public void Bind(MatchController controller)
    {
        if (matchController == controller)
        {
            return;
        }

        CancelCountdown();
        matchController = controller;
    }

    public bool StartCountdown(Action onCompleted)
    {
        ResolveMatchController();
        EnsureBuilt();
        if (!isActiveAndEnabled
            || IsCountingDown
            || matchController == null
            || overlayRoot == null)
        {
            return false;
        }

        completionCallback = onCompleted;
        timeScaleBeforeCountdown = Time.timeScale > 0f
            ? Time.timeScale
            : 1f;
        IsCountingDown = true;
        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        SuspendParticipantInputs();
        Time.timeScale = 0f;
        SetPhase(0);
        countdownCoroutine = StartCoroutine(RunCountdown());
        return true;
    }

    internal void CompleteImmediately()
    {
        if (!IsCountingDown)
        {
            return;
        }

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        CompleteCountdown();
    }

    private IEnumerator RunCountdown()
    {
        for (int phaseIndex = 0;
             phaseIndex < PhaseLabels.Length;
             phaseIndex++)
        {
            SetPhase(phaseIndex);
            yield return new WaitForSecondsRealtime(phaseDuration);
        }

        countdownCoroutine = null;
        CompleteCountdown();
    }

    private void CompleteCountdown()
    {
        if (!IsCountingDown)
        {
            return;
        }

        Action callback = completionCallback;
        completionCallback = null;
        EndCountdownHold();
        callback?.Invoke();
    }

    private void CancelCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        completionCallback = null;
        EndCountdownHold();
    }

    private void EndCountdownHold()
    {
        bool restoreTimeScale = IsCountingDown;
        IsCountingDown = false;
        HideOverlay();
        RestoreParticipantInputs();

        if (restoreTimeScale)
        {
            Time.timeScale = timeScaleBeforeCountdown;
        }
    }

    private void SetPhase(int phaseIndex)
    {
        CurrentPhaseLabel = PhaseLabels[phaseIndex];
        Sprite phaseSprite = GetPhaseSprite(phaseIndex);
        countdownImage.sprite = phaseSprite;
        countdownImage.enabled = phaseSprite != null;
        fallbackText.text = CurrentPhaseLabel;
        fallbackText.enabled = phaseSprite == null;
    }

    private Sprite GetPhaseSprite(int phaseIndex)
    {
        return phaseIndex switch
        {
            0 => threeSprite,
            1 => twoSprite,
            2 => oneSprite,
            3 => goSprite,
            _ => null
        };
    }

    private void SuspendParticipantInputs()
    {
        if (matchController == null)
        {
            return;
        }

        foreach (PlayerLife participant in matchController.Participants)
        {
            if (participant == null)
            {
                continue;
            }

            if (participant.TryGetComponent(
                    out PlayerInputCommandSource humanInput))
            {
                SuspendInputSource(humanInput);
            }

            if (participant.TryGetComponent(
                    out AIPlayerCommandSource aiInput))
            {
                SuspendInputSource(aiInput);
            }
        }
    }

    private void SuspendInputSource(Behaviour inputSource)
    {
        if (inputSource == null
            || !inputSource.enabled
            || suspendedInputSources.Contains(inputSource))
        {
            return;
        }

        inputSource.enabled = false;
        suspendedInputSources.Add(inputSource);
    }

    private void RestoreParticipantInputs()
    {
        foreach (Behaviour inputSource in suspendedInputSources)
        {
            if (inputSource != null)
            {
                inputSource.enabled = true;
            }
        }

        suspendedInputSources.Clear();
    }

    private void ResolveMatchController()
    {
        if (matchController != null
            && matchController.gameObject.scene == gameObject.scene)
        {
            return;
        }

        matchController = null;
        MatchController[] controllers = FindObjectsByType<MatchController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (MatchController controller in controllers)
        {
            if (controller.gameObject.scene == gameObject.scene)
            {
                matchController = controller;
                return;
            }
        }
    }

    private void EnsureBuilt()
    {
        if (overlayRoot != null)
        {
            return;
        }

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        overlayRoot = CreateUiObject("CountdownOverlay", canvasRect);
        RectTransform overlayRect = (RectTransform)overlayRoot.transform;
        Stretch(overlayRect);

        overlayBackground = overlayRoot.AddComponent<Image>();
        overlayBackground.color = OverlayColor;
        overlayBackground.raycastTarget = true;

        GameObject imageObject = CreateUiObject(
            "CountdownImage",
            overlayRect);
        RectTransform imageRect = (RectTransform)imageObject.transform;
        Center(imageRect, imageSize);
        countdownImage = imageObject.AddComponent<Image>();
        countdownImage.color = Color.white;
        countdownImage.preserveAspect = true;
        countdownImage.raycastTarget = false;

        GameObject fallbackObject = CreateUiObject(
            "CountdownFallbackText",
            overlayRect);
        RectTransform fallbackRect =
            (RectTransform)fallbackObject.transform;
        Center(fallbackRect, imageSize);
        fallbackText = fallbackObject.AddComponent<Text>();
        fallbackText.font = RuntimeFont;
        fallbackText.fontSize = 144;
        fallbackText.fontStyle = FontStyle.Bold;
        fallbackText.alignment = TextAnchor.MiddleCenter;
        fallbackText.color = Color.white;
        fallbackText.raycastTarget = false;
        fallbackText.supportRichText = false;
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        uiObject.layer = uiLayer >= 0 ? uiLayer : 5;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static Font RuntimeFont
    {
        get
        {
            if (runtimeFont == null)
            {
                runtimeFont = Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
            }

            return runtimeFont;
        }
    }
}
