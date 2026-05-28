using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateMenu()
    {
        if (SceneManager.GetActiveScene().name != "Menu")
        {
            return;
        }

        if (FindAnyObjectByType<MainMenuController>() != null)
        {
            return;
        }

        GameObject host = new GameObject("Main Menu Controller");
        host.AddComponent<MainMenuController>();
    }

    private Canvas canvas;
    private GameObject settingsPanel;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BuildMenu();
        EnsureEventSystem();
        StartCoroutine(LoadBackground());
    }

    private void BuildMenu()
    {
        GameObject canvasObject = new GameObject("Main Menu UI");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        RawImage background = CreateRawImage("Cocuh Background", canvas.transform);
        background.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        Image shade = CreatePanel("Dark Menu Shade", new Color(0f, 0f, 0f, 0.48f), canvas.transform);
        shade.raycastTarget = false;

        Text titleShadow = CreateText("Title Shadow", canvas.transform, "Couch Case", 88, TextAnchor.MiddleCenter);
        titleShadow.color = new Color(0.18f, 0f, 0f, 0.95f);
        titleShadow.rectTransform.anchoredPosition = new Vector2(4f, -64f);
        SetAnchors(titleShadow.rectTransform, 0.1f, 0.58f, 0.9f, 0.8f);

        Text title = CreateText("Title", canvas.transform, "Couch Case", 88, TextAnchor.MiddleCenter);
        title.color = new Color(0.82f, 0.75f, 0.62f);
        SetAnchors(title.rectTransform, 0.1f, 0.58f, 0.9f, 0.8f);

        Text subtitle = CreateText("Subtitle", canvas.transform, "based on a true story", 28, TextAnchor.MiddleCenter);
        subtitle.color = new Color(0.7f, 0.07f, 0.055f);
        SetAnchors(subtitle.rectTransform, 0.2f, 0.52f, 0.8f, 0.59f);

        Button play = CreateButton("Play Button", "PLAY", new Vector2(0f, -60f));
        play.onClick.AddListener(() => StartCoroutine(LoadGame()));

        Button settings = CreateButton("Settings Button", "SETTINGS", new Vector2(0f, -130f));
        settings.onClick.AddListener(ToggleSettings);

        settingsPanel = CreatePanel("Settings Panel", new Color(0f, 0f, 0f, 0.72f), canvas.transform).gameObject;
        RectTransform settingsRect = settingsPanel.GetComponent<RectTransform>();
        SetAnchors(settingsRect, 0.32f, 0.24f, 0.68f, 0.49f);
        settingsPanel.SetActive(false);
    }

    private IEnumerator LoadBackground()
    {
        string path = Path.Combine(Application.dataPath, "Cocuh.jpg");
        if (!File.Exists(path))
        {
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture("file:///" + path.Replace("\\", "/")))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            RawImage background = GameObject.Find("Cocuh Background").GetComponent<RawImage>();
            background.texture = DownloadHandlerTexture.GetContent(request);
            background.color = Color.white;
        }
    }

    private void ToggleSettings()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    private IEnumerator LoadGame()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.LoadScene("SampleScene");
        yield return null;
        NightmarePrologue.EnsureStarted();
        Destroy(gameObject);
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private Button CreateButton(string name, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(canvas.transform);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.22f, 0.04f, 0.035f, 0.95f);
        colors.pressedColor = new Color(0.42f, 0.02f, 0.02f, 1f);
        button.colors = colors;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 52f);
        rect.anchoredPosition = position;

        Text text = CreateText(name + " Text", buttonObject.transform, label, 26, TextAnchor.MiddleCenter);
        text.color = new Color(0.82f, 0.75f, 0.62f);
        SetAnchors(text.rectTransform, 0f, 0f, 1f, 1f);
        return button;
    }

    private RawImage CreateRawImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent);
        RawImage image = imageObject.AddComponent<RawImage>();
        SetAnchors(image.rectTransform, 0f, 0f, 1f, 1f);
        return image;
    }

    private Image CreatePanel(string name, Color color, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        SetAnchors(image.rectTransform, 0f, 0f, 1f, 1f);
        return image;
    }

    private Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
