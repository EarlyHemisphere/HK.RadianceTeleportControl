using System.Collections;
using Modding;
using UnityEngine;
using UnityEngine.UI;

public class RngIndicatorUI : MonoBehaviour {
    private const float DisplaySeconds = 2.5f;

    private CanvasGroup canvasGroup;
    private Text textComponent;
    private Coroutine activeRoutine;

    private void Awake() {
        DontDestroyOnLoad(gameObject);

        GameObject canvas = CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));
        canvas.transform.SetParent(transform);

        canvasGroup = canvas.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject textPanel = CanvasUtil.CreateTextPanel(
            canvas,
            "",
            48,
            TextAnchor.MiddleCenter,
            new CanvasUtil.RectData(new Vector2(1600, 90), new Vector2(0, -380), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f))
        );
        textComponent = textPanel.GetComponent<Text>();
        textComponent.color = Color.red;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
    }

    public void Show(string message) {
        textComponent.text = message;
        if (activeRoutine != null) {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(ShowRoutine());
    }

    public void ShowPlatsPhaseIfAltered() {
        if (!RadianceTeleportControl.instance.PlatsTeleportsAreDefault()) {
            Show("Platform Phase: Modified Teleports");
        }
    }

    public void ShowFinalPhaseIfAltered() {
        if (!RadianceTeleportControl.instance.FinalPhaseTeleportsAreDefault()) {
            Show("Final Phase: Modified Teleports");
        }
    }

    private IEnumerator ShowRoutine() {
        yield return StartCoroutine(CanvasUtil.FadeInCanvasGroup(canvasGroup));
        yield return new WaitForSeconds(DisplaySeconds);
        yield return StartCoroutine(CanvasUtil.FadeOutCanvasGroup(canvasGroup));
        activeRoutine = null;
    }
}
