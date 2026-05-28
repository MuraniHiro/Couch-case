using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NightmarePrologue : MonoBehaviour
{
    private enum Stage
    {
        Classroom,
        Hallway,
        Alley,
        Attack,
        Bedroom,
        HomeRoutine,
        CouchSleep
    }

    private static NightmarePrologue instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartNightmare()
    {
        EnsureStarted();
    }

    public static void EnsureStarted()
    {
        if (instance != null)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name == "Menu")
        {
            return;
        }

        GameObject host = new GameObject("Nightmare Prologue");
        instance = host.AddComponent<NightmarePrologue>();
        DontDestroyOnLoad(host);
    }

    private readonly Color wallColor = new Color(0.44f, 0.48f, 0.46f);
    private readonly Color floorColor = new Color(0.16f, 0.17f, 0.16f);
    private readonly Color deskColor = new Color(0.36f, 0.24f, 0.16f);
    private readonly Color warningColor = new Color(0.62f, 0.08f, 0.05f);

    private Camera playerCamera;
    private CharacterController character;
    private Transform player;
    private Transform cameraPivot;
    private Canvas canvas;
    private Text dialogueText;
    private Text promptText;
    private Text objectiveText;
    private Image fade;
    private Image vignette;
    private Volume volume;
    private Vignette volumeVignette;
    private FilmGrain filmGrain;
    private ChromaticAberration chromaticAberration;

    private Stage stage;
    private bool locked;
    private bool sequenceRunning;
    private float yaw;
    private float pitch;
    private float interactionDistance = 1.65f;
    private Interactable focusedInteractable;
    private int currentDay = 1;
    private int routineStep;
    private string activeRoutineId = string.Empty;
    private bool couchMode;
    private int couchScene;
    private string activeCouchObjective = string.Empty;
    private bool couchInspecting;
    private CouchSpot inspectedCouchSpot;
    private bool goodEndingPillsLidFound;
    private bool pillsLidFlashing;
    private Transform mom;
    private Transform dad;
    private GameObject foodPlate;
    private GameObject tvGlow;
    private GameObject hallwayFigure;
    private GameObject pillsLid;
    private Renderer pillsLidRenderer;
    private Color pillsLidBaseColor;
    private GameObject bedroomDoor;
    private Collider bedroomDoorCollider;
    private Light livingRoomLight;
    private bool bedroomDoorOpen;
    private bool skipShortcutUsed;
    private float couchBaseYaw = 0f;
    private float couchBasePitch = -10f;
    private const float CouchYawLimit = 67.5f;
    private const float CouchPitchLimit = 24f;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        BuildWorld();
        BuildPlayer();
        BuildInterface();
        BuildPostProcessing();
        EnterClassroom();
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        HandleSkipShortcut(keyboard);
        UpdateGoodEndingClueGlow();

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (couchMode)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }
        }

        if (couchMode)
        {
            CouchLook();
            Move();
            UpdateCouchInteraction();
        }
        else
        {
            Look();
            Move();
            UpdateInteraction();
        }

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame && focusedInteractable != null && !sequenceRunning)
        {
            focusedInteractable.Use();
        }
    }

    private void UpdateGoodEndingClueGlow()
    {
        if (pillsLidRenderer == null || goodEndingPillsLidFound || !pillsLidFlashing)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.time * 5.2f) + 1f) * 0.5f;
        pillsLidRenderer.material.color = Color.Lerp(pillsLidBaseColor, new Color(0.15f, 0.9f, 0.2f), pulse);
    }

    private void HandleSkipShortcut(Keyboard keyboard)
    {
        if (keyboard == null || couchMode || skipShortcutUsed)
        {
            return;
        }

        if (keyboard.sKey.isPressed && keyboard.kKey.isPressed && keyboard.iKey.isPressed && keyboard.pKey.isPressed)
        {
            skipShortcutUsed = true;
            StopAllCoroutines();
            dialogueText.text = string.Empty;
            promptText.text = string.Empty;
            objectiveText.text = string.Empty;
            fade.color = Color.black;
            StartCouchSceneOne();
        }
    }

    private void BuildWorld()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.05f, 0.055f, 0.06f);
        RenderSettings.ambientLight = new Color(0.18f, 0.18f, 0.2f);

        GameObject oldCamera = GameObject.FindWithTag("MainCamera");
        if (oldCamera != null)
        {
            oldCamera.SetActive(false);
        }

        GameObject oldLight = GameObject.Find("Directional Light");
        if (oldLight != null)
        {
            oldLight.SetActive(false);
        }

        GameObject root = new GameObject("Procedural Nightmare Set");
        BuildClassroom(root.transform);
        BuildHallway(root.transform);
        BuildAlley(root.transform);
        BuildHomeLayout(root.transform);
    }

    private void BuildPlayer()
    {
        GameObject body = new GameObject("Player");
        player = body.transform;
        character = body.AddComponent<CharacterController>();
        character.height = 1.65f;
        character.radius = 0.28f;
        character.center = new Vector3(0f, 0.82f, 0f);

        cameraPivot = new GameObject("Camera Pivot").transform;
        cameraPivot.SetParent(player);
        cameraPivot.localPosition = new Vector3(0f, 1.52f, 0f);

        GameObject cameraObject = new GameObject("Nightmare Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(cameraPivot);
        cameraObject.transform.localPosition = Vector3.zero;
        cameraObject.transform.localRotation = Quaternion.identity;
        playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.fieldOfView = 64f;
        cameraObject.AddComponent<AudioListener>();
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Nightmare UI");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        fade = CreatePanel("Fade", Color.black, canvas.transform);
        fade.raycastTarget = false;

        vignette = CreatePanel("Fear Vignette", new Color(0f, 0f, 0f, 0.38f), canvas.transform);
        vignette.raycastTarget = false;

        dialogueText = CreateText("Dialogue", canvas.transform, 30, TextAnchor.LowerLeft);
        RectTransform dialogueRect = dialogueText.rectTransform;
        dialogueRect.anchorMin = new Vector2(0.08f, 0.06f);
        dialogueRect.anchorMax = new Vector2(0.92f, 0.24f);
        dialogueRect.offsetMin = Vector2.zero;
        dialogueRect.offsetMax = Vector2.zero;
        dialogueText.color = new Color(0.91f, 0.91f, 0.86f);
        dialogueText.text = string.Empty;

        promptText = CreateText("Prompt", canvas.transform, 22, TextAnchor.MiddleCenter);
        RectTransform promptRect = promptText.rectTransform;
        promptRect.anchorMin = new Vector2(0.25f, 0.45f);
        promptRect.anchorMax = new Vector2(0.75f, 0.55f);
        promptRect.offsetMin = Vector2.zero;
        promptRect.offsetMax = Vector2.zero;
        promptText.color = new Color(1f, 0.94f, 0.74f);
        promptText.text = string.Empty;

        objectiveText = CreateText("Objective", canvas.transform, 22, TextAnchor.UpperLeft);
        RectTransform objectiveRect = objectiveText.rectTransform;
        objectiveRect.anchorMin = new Vector2(0.06f, 0.82f);
        objectiveRect.anchorMax = new Vector2(0.62f, 0.94f);
        objectiveRect.offsetMin = Vector2.zero;
        objectiveRect.offsetMax = Vector2.zero;
        objectiveText.color = new Color(0.82f, 0.8f, 0.7f);
        objectiveText.text = string.Empty;
    }

    private void BuildPostProcessing()
    {
        GameObject volumeObject = new GameObject("Nightmare Post Processing");
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        volumeVignette = volume.profile.Add<Vignette>();
        volumeVignette.intensity.Override(0.35f);
        volumeVignette.smoothness.Override(0.7f);

        filmGrain = volume.profile.Add<FilmGrain>();
        filmGrain.intensity.Override(0.42f);
        filmGrain.response.Override(0.78f);

        chromaticAberration = volume.profile.Add<ChromaticAberration>();
        chromaticAberration.intensity.Override(0.08f);
    }

    private void BuildClassroom(Transform root)
    {
        GameObject room = new GameObject("Public School Classroom");
        room.transform.SetParent(root);

        CreateRoom(room.transform, new Vector3(0f, 1.5f, 0f), new Vector3(12f, 3f, 11f), wallColor, floorColor);
        CreateCube("Chalkboard", room.transform, new Vector3(0f, 1.7f, 5.46f), new Vector3(7.5f, 1.7f, 0.08f), new Color(0.04f, 0.12f, 0.09f));
        CreateCube("Teacher Desk", room.transform, new Vector3(0f, 0.45f, 3.6f), new Vector3(2.4f, 0.9f, 0.9f), deskColor);
        CreateCube("Classroom Door", room.transform, new Vector3(5.9f, 1f, -3.8f), new Vector3(0.08f, 2f, 1.1f), new Color(0.19f, 0.12f, 0.08f));

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                Vector3 position = new Vector3(-4.2f + column * 2.8f, 0.38f, 1.5f - row * 2f);
                CreateCube("Student Desk", room.transform, position, new Vector3(1.15f, 0.75f, 0.85f), deskColor);
                CreateCube("Chair", room.transform, position + new Vector3(0f, 0.22f, -0.72f), new Vector3(0.85f, 0.45f, 0.45f), new Color(0.18f, 0.18f, 0.2f));
            }
        }

        CreatePerson("Classmate", room.transform, new Vector3(4.15f, 0f, -2.2f), new Color(0.11f, 0.12f, 0.16f), string.Empty);
        AddInteraction("Classmate Trigger", new Vector3(4.15f, 0.8f, -2.2f), new Vector3(1.2f, 1.6f, 1.2f), "Talk", TalkToClassmate);

        Light classroomLight = CreateLight("Cold Fluorescent Light", room.transform, new Vector3(0f, 2.7f, 0f), LightType.Point, new Color(0.72f, 0.83f, 0.92f), 7f, 12f);
        classroomLight.shadows = LightShadows.Soft;
    }

    private void BuildHallway(Transform root)
    {
        GameObject hall = new GameObject("School Hallway");
        hall.transform.SetParent(root);
        CreateRoom(hall.transform, new Vector3(12f, 1.35f, -3.8f), new Vector3(12f, 2.7f, 3f), new Color(0.34f, 0.36f, 0.35f), new Color(0.12f, 0.13f, 0.13f));

        for (int i = 0; i < 8; i++)
        {
            CreateCube("Locker", hall.transform, new Vector3(7f + i * 1.35f, 0.95f, -5.26f), new Vector3(0.72f, 1.9f, 0.1f), new Color(0.22f, 0.29f, 0.34f));
        }

        CreateCube("Exit Door", hall.transform, new Vector3(17.95f, 1f, -3.8f), new Vector3(0.08f, 2f, 1.25f), new Color(0.11f, 0.1f, 0.09f));
        AddInteraction("Exit Trigger", new Vector3(17.5f, 0.8f, -3.8f), new Vector3(1.5f, 1.6f, 1.5f), "Go outside", LeaveSchool);
        CreateLight("Hallway Light", hall.transform, new Vector3(12f, 2.35f, -3.8f), LightType.Point, new Color(0.64f, 0.72f, 0.8f), 5f, 10f);
    }

    private void BuildAlley(Transform root)
    {
        GameObject alley = new GameObject("Dead End Alley");
        alley.transform.SetParent(root);
        CreateCube("Alley Ground", alley.transform, new Vector3(28f, -0.03f, -3.8f), new Vector3(12f, 0.06f, 4.2f), new Color(0.055f, 0.055f, 0.05f));
        CreateCube("Left Brick Wall", alley.transform, new Vector3(28f, 1.65f, -6.05f), new Vector3(12f, 3.3f, 0.3f), new Color(0.19f, 0.08f, 0.07f));
        CreateCube("Right Brick Wall", alley.transform, new Vector3(28f, 1.65f, -1.55f), new Vector3(12f, 3.3f, 0.3f), new Color(0.13f, 0.12f, 0.11f));
        CreateCube("Dead End Wall", alley.transform, new Vector3(34f, 1.65f, -3.8f), new Vector3(0.35f, 3.3f, 4.2f), new Color(0.1f, 0.09f, 0.09f));
        CreateCube("Dumpster", alley.transform, new Vector3(31.8f, 0.65f, -5.35f), new Vector3(1.8f, 1.3f, 0.8f), new Color(0.08f, 0.16f, 0.12f));

        CreatePerson("Classmate Shadow", alley.transform, new Vector3(32.2f, 0f, -3.8f), new Color(0.02f, 0.02f, 0.025f), string.Empty);
        CreatePerson("Friend Shadow A", alley.transform, new Vector3(30.9f, 0f, -4.85f), new Color(0.025f, 0.025f, 0.03f), string.Empty);
        CreatePerson("Friend Shadow B", alley.transform, new Vector3(31.1f, 0f, -2.75f), new Color(0.025f, 0.025f, 0.03f), string.Empty);
        CreatePerson("Friend Shadow C", alley.transform, new Vector3(33.1f, 0f, -4.55f), new Color(0.025f, 0.025f, 0.03f), string.Empty);
        AddInteraction("Alley Meeting Trigger", new Vector3(31.9f, 0.8f, -3.8f), new Vector3(2.4f, 1.8f, 2.4f), "Approach", StartAttackSequence);

        Light alleyLight = CreateLight("Flickering Alley Light", alley.transform, new Vector3(30f, 3.05f, -3.8f), LightType.Point, warningColor, 4.4f, 8f);
        alleyLight.shadows = LightShadows.Soft;
        alleyLight.gameObject.AddComponent<FlickerLight>();
    }

    private void BuildHomeLayout(Transform root)
    {
        GameObject home = new GameObject("Fletcher House Layout Blockout");
        home.transform.SetParent(root);

        Color carpet = new Color(0.18f, 0.145f, 0.12f);
        Color tile = new Color(0.24f, 0.25f, 0.23f);
        Color kitchenFloor = new Color(0.2f, 0.21f, 0.18f);
        Color houseWall = new Color(0.3f, 0.27f, 0.23f);

        CreateCube("Continuous House Subfloor", home.transform, new Vector3(0f, -0.09f, -19.1f), new Vector3(14.5f, 0.08f, 13.8f), carpet * 0.72f);
        CreateCube("Bedroom Floor", home.transform, new Vector3(-5f, -0.04f, -14.5f), new Vector3(4f, 0.08f, 3f), carpet);
        CreateCube("Bathroom Floor", home.transform, new Vector3(-1f, -0.04f, -14.5f), new Vector3(4f, 0.08f, 3f), tile);
        CreateCube("Kitchen Floor", home.transform, new Vector3(3.5f, -0.04f, -14.5f), new Vector3(5f, 0.08f, 3f), kitchenFloor);
        CreateCube("Hallway Floor", home.transform, new Vector3(-3f, -0.04f, -18f), new Vector3(6f, 0.08f, 3.2f), carpet * 0.8f);
        CreateCube("Living Room Square Floor", home.transform, new Vector3(0f, -0.04f, -21.25f), new Vector3(6f, 0.08f, 4.7f), carpet);
        CreateHalfCircleFloor("Living Room Rounded Floor", home.transform, new Vector3(0f, 0.015f, -23.2f), 3f, 18, carpet);
        CreateCube("Entrance Floor", home.transform, new Vector3(5f, -0.04f, -20f), new Vector3(4f, 0.08f, 7.2f), tile * 0.8f);
        CreateCube("House Ceiling", home.transform, new Vector3(0f, 2.72f, -19f), new Vector3(14.4f, 0.12f, 13.2f), houseWall * 0.65f);

        CreateCube("Back Exterior Wall", home.transform, new Vector3(-0.5f, 1.35f, -12.95f), new Vector3(13.2f, 2.7f, 0.12f), houseWall);
        CreateCube("Bedroom Left Wall", home.transform, new Vector3(-7.05f, 1.35f, -16.5f), new Vector3(0.12f, 2.7f, 7f), houseWall * 0.85f);
        CreateCube("Kitchen Right Wall", home.transform, new Vector3(6.05f, 1.35f, -16.5f), new Vector3(0.12f, 2.7f, 7f), houseWall * 0.9f);
        CreateCube("Entrance Right Wall", home.transform, new Vector3(7.05f, 1.35f, -21.8f), new Vector3(0.12f, 2.7f, 6.4f), houseWall * 0.85f);
        CreateCube("Living Room Left Wall", home.transform, new Vector3(-3.05f, 1.35f, -21.65f), new Vector3(0.12f, 2.7f, 5.1f), houseWall * 0.75f);
        CreateCube("Living Room Right Wall", home.transform, new Vector3(3.05f, 1.35f, -21.55f), new Vector3(0.12f, 2.7f, 5.3f), houseWall * 0.75f);
        CreateCube("Living Room Back Wall Left", home.transform, new Vector3(-1.85f, 1.35f, -18.95f), new Vector3(2.3f, 2.7f, 0.12f), houseWall * 0.78f);
        CreateCube("Living Room Back Wall Right", home.transform, new Vector3(2.05f, 1.35f, -18.95f), new Vector3(1.9f, 2.7f, 0.12f), houseWall * 0.78f);
        CreateArcWalls(home.transform, new Vector3(0f, 1.35f, -23.2f), 3.05f, 11, houseWall * 0.7f);
        CreateCube("Bedroom Bathroom Wall", home.transform, new Vector3(-3f, 1.35f, -14.5f), new Vector3(0.12f, 2.7f, 3f), houseWall);
        CreateCube("Bathroom Kitchen Wall", home.transform, new Vector3(1.05f, 1.35f, -14.3f), new Vector3(0.12f, 2.7f, 2.6f), houseWall);
        CreateCube("Bedroom Front Wall Left", home.transform, new Vector3(-6.18f, 1.35f, -16.05f), new Vector3(1.65f, 2.7f, 0.12f), houseWall * 0.8f);
        CreateCube("Bedroom Front Wall Right", home.transform, new Vector3(-3.18f, 1.35f, -16.05f), new Vector3(0.35f, 2.7f, 0.12f), houseWall * 0.8f);
        CreateCube("Bedroom Door Header", home.transform, new Vector3(-4.55f, 2.43f, -16.13f), new Vector3(1.55f, 0.58f, 0.22f), houseWall * 0.78f);
        CreateCube("Bedroom Door Left Jamb", home.transform, new Vector3(-5.28f, 1.35f, -16.13f), new Vector3(0.28f, 2.7f, 0.22f), houseWall * 0.78f);
        CreateCube("Bedroom Door Right Jamb", home.transform, new Vector3(-3.82f, 1.35f, -16.13f), new Vector3(0.28f, 2.7f, 0.22f), houseWall * 0.78f);
        CreateCube("Bedroom Door Threshold", home.transform, new Vector3(-4.55f, 0.04f, -16.13f), new Vector3(1.45f, 0.08f, 0.28f), carpet * 0.6f);
        CreateCube("Bathroom Front Wall Left", home.transform, new Vector3(-2.48f, 1.35f, -16.05f), new Vector3(0.95f, 2.7f, 0.12f), houseWall * 0.8f);
        CreateCube("Bathroom Front Wall Right", home.transform, new Vector3(0.35f, 1.35f, -16.05f), new Vector3(1.3f, 2.7f, 0.12f), houseWall * 0.8f);
        CreateCube("Bathroom Door Header", home.transform, new Vector3(-1.25f, 2.43f, -16.13f), new Vector3(1.2f, 0.58f, 0.22f), houseWall * 0.78f);
        CreateCube("Bathroom Door Left Jamb", home.transform, new Vector3(-1.9f, 1.35f, -16.13f), new Vector3(0.22f, 2.7f, 0.22f), houseWall * 0.78f);
        CreateCube("Bathroom Door Right Jamb", home.transform, new Vector3(-0.58f, 1.35f, -16.13f), new Vector3(0.22f, 2.7f, 0.22f), houseWall * 0.78f);
        CreateCube("Entrance Left Exterior Wall", home.transform, new Vector3(3.18f, 1.35f, -23.9f), new Vector3(0.24f, 2.7f, 2.8f), houseWall * 0.76f);
        CreateCube("Entrance Back Wall Left", home.transform, new Vector3(4.15f, 1.35f, -16.35f), new Vector3(1.9f, 2.7f, 0.24f), houseWall * 0.76f);
        CreateCube("Entrance Back Wall Right", home.transform, new Vector3(6.15f, 1.35f, -16.35f), new Vector3(1.7f, 2.7f, 0.24f), houseWall * 0.76f);
        CreateCube("Entrance Door Header", home.transform, new Vector3(5.4f, 2.42f, -25.25f), new Vector3(1.55f, 0.58f, 0.24f), houseWall * 0.72f);
        CreateCube("Entrance Door Left Jamb", home.transform, new Vector3(4.67f, 1.35f, -25.25f), new Vector3(0.24f, 2.7f, 0.24f), houseWall * 0.72f);
        CreateCube("Entrance Door Right Jamb", home.transform, new Vector3(6.13f, 1.35f, -25.25f), new Vector3(0.24f, 2.7f, 0.24f), houseWall * 0.72f);
        CreateCube("Front Door", home.transform, new Vector3(5.4f, 1.1f, -25.32f), new Vector3(1.12f, 2.2f, 0.1f), new Color(0.16f, 0.09f, 0.05f));
        CreateCube("Hallway Bottom Wall", home.transform, new Vector3(-4.6f, 1.35f, -19.55f), new Vector3(4.7f, 2.7f, 0.12f), houseWall * 0.8f);
        CreateCube("Entrance Bottom Left", home.transform, new Vector3(4.45f, 1.35f, -25.25f), new Vector3(1.4f, 2.7f, 0.12f), houseWall * 0.8f);
        CreateCube("Entrance Bottom Right", home.transform, new Vector3(6.4f, 1.35f, -25.25f), new Vector3(1.4f, 2.7f, 0.12f), houseWall * 0.8f);

        CreateCube("Bedroom Bed Base", home.transform, new Vector3(-5.5f, 0.35f, -14.4f), new Vector3(1.8f, 0.7f, 2.1f), new Color(0.18f, 0.13f, 0.12f));
        CreateCube("Bedroom Pillow", home.transform, new Vector3(-5.5f, 0.9f, -13.55f), new Vector3(1.4f, 0.18f, 0.55f), new Color(0.62f, 0.58f, 0.52f));
        bedroomDoor = CreateCube("Bedroom Door", home.transform, new Vector3(-4.55f, 1.1f, -16.05f), new Vector3(1f, 2.2f, 0.1f), new Color(0.18f, 0.1f, 0.055f));
        bedroomDoorCollider = bedroomDoor.GetComponent<Collider>();
        CreateCube("Homework Desk", home.transform, new Vector3(-3.28f, 0.45f, -14.55f), new Vector3(0.75f, 0.9f, 1.45f), deskColor);
        CreateCube("Bathroom Sink", home.transform, new Vector3(-1.4f, 0.55f, -13.65f), new Vector3(1f, 1.1f, 0.55f), new Color(0.52f, 0.55f, 0.54f));
        CreateCube("Kitchen Table", home.transform, new Vector3(3.6f, 0.45f, -14.8f), new Vector3(1.5f, 0.9f, 1.1f), new Color(0.26f, 0.18f, 0.12f));
        CreateCube("Kitchen Plate", home.transform, new Vector3(3.6f, 0.94f, -14.8f), new Vector3(0.48f, 0.04f, 0.48f), new Color(0.72f, 0.68f, 0.58f));
        CreateCube("Living Room Couch", home.transform, new Vector3(0f, 0.42f, -24.7f), new Vector3(2.2f, 0.85f, 1.05f), new Color(0.22f, 0.18f, 0.14f));
        CreateCube("Couch Cushion", home.transform, new Vector3(0f, 0.9f, -24.7f), new Vector3(2.05f, 0.16f, 0.9f), new Color(0.34f, 0.29f, 0.23f));
        CreateCube("TV Stand", home.transform, new Vector3(-2.55f, 0.45f, -22.4f), new Vector3(0.55f, 0.9f, 1.5f), new Color(0.12f, 0.08f, 0.06f));
        CreateCube("Old TV", home.transform, new Vector3(-2.72f, 1.05f, -22.4f), new Vector3(0.42f, 0.8f, 1.15f), new Color(0.04f, 0.04f, 0.045f));
        tvGlow = CreateCube("TV Glow", home.transform, new Vector3(-2.95f, 1.06f, -22.4f), new Vector3(0.04f, 0.58f, 0.9f), new Color(0.02f, 0.03f, 0.035f));
        CreateCube("Opened Cabinet Under TV", home.transform, new Vector3(-2.45f, 0.16f, -21.85f), new Vector3(0.55f, 0.28f, 0.7f), new Color(0.06f, 0.04f, 0.03f));
        CreateCube("Forgotten Keys", home.transform, new Vector3(-1.6f, 0.78f, -22.15f), new Vector3(0.42f, 0.04f, 0.12f), new Color(0.86f, 0.72f, 0.28f));
        CreateCube("Pill Bottle", home.transform, new Vector3(-1.25f, 0.78f, -22.45f), new Vector3(0.18f, 0.32f, 0.18f), new Color(0.75f, 0.74f, 0.66f));
        pillsLid = CreateCube("Pill Bottle Lid", home.transform, new Vector3(-1.25f, 1f, -22.45f), new Vector3(0.24f, 0.08f, 0.24f), new Color(0.38f, 0.38f, 0.36f));
        pillsLidRenderer = pillsLid.GetComponent<Renderer>();
        pillsLidBaseColor = pillsLidRenderer.material.color;
        CreateCube("Crooked Painting", home.transform, new Vector3(1.6f, 1.45f, -18.9f), new Vector3(0.9f, 0.65f, 0.05f), new Color(0.28f, 0.22f, 0.15f));
        foodPlate = CreateCube("Couch Food Plate", home.transform, new Vector3(-1f, 0.75f, -22.8f), new Vector3(0.48f, 0.05f, 0.48f), new Color(0.22f, 0.18f, 0.13f));
        foodPlate.SetActive(false);

        mom = CreatePersonObject("Mother", home.transform, new Vector3(4.8f, 0f, -19.2f), new Color(0.18f, 0.12f, 0.12f));
        dad = CreatePersonObject("Father", home.transform, new Vector3(5.55f, 0f, -19.6f), new Color(0.12f, 0.13f, 0.16f));
        mom.gameObject.SetActive(false);
        dad.gameObject.SetActive(false);
        hallwayFigure = CreatePersonObject("Hallway Figure", home.transform, new Vector3(3f, 0f, -18.9f), new Color(0.015f, 0.015f, 0.018f)).gameObject;
        hallwayFigure.SetActive(false);

        CreateLight("Bedroom Lamp", home.transform, new Vector3(-5.1f, 2.25f, -14.5f), LightType.Point, new Color(0.95f, 0.66f, 0.42f), 2.3f, 6f);
        CreateLight("Kitchen Overhead", home.transform, new Vector3(3.4f, 2.35f, -14.6f), LightType.Point, new Color(0.8f, 0.74f, 0.55f), 2.6f, 7f);
        livingRoomLight = CreateLight("Living Room TV Spill", home.transform, new Vector3(-1.5f, 1.9f, -22.3f), LightType.Point, new Color(0.35f, 0.48f, 0.65f), 1.1f, 7f);

        AddRoutineInteraction("Routine Bed Trigger", new Vector3(-5.5f, 0.8f, -14.4f), new Vector3(1.8f, 1.6f, 2.2f), "Wake", "Get up");
        AddInteraction("Bedroom Door Trigger", new Vector3(-4.55f, 0.9f, -15.78f), new Vector3(0.8f, 1.4f, 0.8f), "Open bedroom door", OpenBedroomDoor);
        AddRoutineInteraction("Routine Breakfast Trigger", new Vector3(3.6f, 0.8f, -14.8f), new Vector3(1.8f, 1.6f, 1.5f), "Breakfast", "Eat breakfast");
        AddRoutineInteraction("Routine Homework Trigger", new Vector3(-3.55f, 0.8f, -14.55f), new Vector3(1.1f, 1.5f, 1.6f), "Homework", "Do school work");
        AddRoutineInteraction("Routine TV Trigger", new Vector3(-1.6f, 0.8f, -22.4f), new Vector3(1.8f, 1.6f, 2f), "TV", "Watch TV");
        AddRoutineInteraction("Routine Bathroom Trigger", new Vector3(-1.4f, 0.8f, -13.95f), new Vector3(1.7f, 1.6f, 1.7f), "Bathroom", "Use bathroom");
        AddRoutineInteraction("Routine Sleep Trigger", new Vector3(-5.5f, 0.8f, -14.4f), new Vector3(1.8f, 1.6f, 2.2f), "Sleep", "Go back to bed");
        AddRoutineInteraction("Routine Couch Trigger", new Vector3(0f, 0.8f, -24.7f), new Vector3(2.4f, 1.6f, 1.4f), "Couch", "Rest on couch");

        AddCouchSpot("Couch TV Spot", new Vector3(-2.8f, 1.05f, -22.4f), new Vector3(0.6f, 1f, 1.4f), "TV", "The TV is the only thing that still moves for you.");
        AddCouchSpot("Couch Food Spot", new Vector3(-1f, 0.85f, -22.8f), new Vector3(0.9f, 0.5f, 0.9f), "Food", "It smells warm. It might as well be across the street.");
        AddCouchSpot("Couch Hallway Spot", new Vector3(3.05f, 1.25f, -20f), new Vector3(1.1f, 2f, 2.5f), "Hallway", "The hallway is where footsteps appear and disappear.");
        AddCouchSpot("Couch Window Spot", new Vector3(-2.6f, 1.25f, -24.2f), new Vector3(0.25f, 1.4f, 1.2f), "Window", "Something taps sometimes. You never see what made the sound.");
        AddCouchSpot("Parents Couch Spot", new Vector3(2.7f, 1.15f, -21.3f), new Vector3(1.7f, 1.8f, 1.1f), "Parents", string.Empty);
        AddCouchSpot("Keys Couch Spot", new Vector3(-1.6f, 0.86f, -22.15f), new Vector3(0.55f, 0.35f, 0.35f), "Key", "Looks like my parents forgot to take their keys with them.");
        AddCouchSpot("Pills Couch Spot", new Vector3(-1.25f, 0.92f, -22.45f), new Vector3(0.5f, 0.6f, 0.5f), "Pills", "The bottle is close enough to read, but too far to reach.");
        AddCouchSpot("Pills Lid Couch Spot", new Vector3(-1.25f, 1.02f, -22.45f), new Vector3(0.32f, 0.24f, 0.32f), "PillsLid", string.Empty);
        AddCouchSpot("Painting Couch Spot", new Vector3(1.6f, 1.45f, -18.9f), new Vector3(1.1f, 0.85f, 0.4f), "Painting", "The painting has been crooked for years. No one ever fixes it.");
        AddCouchSpot("Couch End Spot", new Vector3(1.25f, 0.8f, -24.7f), new Vector3(0.65f, 0.8f, 0.9f), "CouchEnd", "The end of the couch feels impossibly far away.");
        AddCouchSpot("Opened Cabinet Spot", new Vector3(-2.45f, 0.42f, -21.85f), new Vector3(0.75f, 0.65f, 0.85f), "Cabinet", "The cabinet under the TV is open. Dust collects where hands used to go.");
        AddCouchSpot("Window Part Spot", new Vector3(-2.6f, 1.25f, -23.65f), new Vector3(0.35f, 1.3f, 0.8f), "WindowPart", "A thin piece of window catches the light. Outside might as well be a photograph.");
    }

    private void EnterClassroom()
    {
        stage = Stage.Classroom;
        Teleport(new Vector3(-3.35f, 0.05f, -1.85f), 42f);
        StartCoroutine(FadeFromBlack("The bell never sounds right in this dream.\nClass is over. Someone is waiting by the door."));
    }

    private void TalkToClassmate()
    {
        if (stage != Stage.Classroom)
        {
            return;
        }

        StartCoroutine(ClassmateDialogue());
    }

    private IEnumerator ClassmateDialogue()
    {
        sequenceRunning = true;
        locked = true;
        yield return Say("Classmate: Meet me outside. Behind the school.", 2.5f);
        yield return Say("Lacey: Why?", 1.7f);
        yield return Say("Classmate: Just come alone.", 2.2f);
        locked = false;
        sequenceRunning = false;
        stage = Stage.Hallway;
        yield return Say("Objective: Leave the classroom and go outside.", 3f);
        Teleport(new Vector3(8f, 0.05f, -3.8f), 90f);
    }

    private void LeaveSchool()
    {
        if (stage != Stage.Hallway)
        {
            return;
        }

        stage = Stage.Alley;
        Teleport(new Vector3(24f, 0.05f, -3.8f), 90f);
        StartCoroutine(Say("The air outside is too quiet. The alley ends in brick.", 3f));
    }

    private void OpenBedroomDoor()
    {
        if (stage != Stage.HomeRoutine || bedroomDoorOpen)
        {
            return;
        }

        bedroomDoorOpen = true;
        if (bedroomDoorCollider != null)
        {
            bedroomDoorCollider.enabled = false;
        }

        bedroomDoor.transform.position += new Vector3(-0.45f, 0f, 0.55f);
        bedroomDoor.transform.rotation = Quaternion.Euler(0f, 75f, 0f);
        StartCoroutine(Say("The bedroom door opens.", 1.4f));
    }

    private void StartAttackSequence()
    {
        if (stage != Stage.Alley)
        {
            return;
        }

        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        stage = Stage.Attack;
        sequenceRunning = true;
        locked = true;
        yield return Say("Classmate: You really came.", 1.8f);
        yield return Say("Lacey: I want to go home.", 1.8f);
        yield return Say("The way back feels farther than it should.", 1.5f);

        float timer = 0f;
        while (timer < 3.5f)
        {
            timer += Time.deltaTime;
            float shake = Mathf.Sin(Time.time * 44f) * 0.055f;
            cameraPivot.localPosition = new Vector3(shake, 1.52f + Mathf.Abs(shake), 0f);
            volumeVignette.intensity.Override(Mathf.Lerp(0.35f, 0.78f, timer / 3.5f));
            chromaticAberration.intensity.Override(Mathf.Lerp(0.08f, 0.55f, timer / 3.5f));
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.9f, timer / 3.5f));
            yield return null;
        }

        cameraPivot.localPosition = new Vector3(0f, 1.52f, 0f);
        yield return Say("No faces. Only shoes. Brick. Breath. The thought of home.", 2.6f);
        yield return new WaitForSeconds(0.8f);
        EnterBedroom();
    }

    private void EnterBedroom()
    {
        stage = Stage.Bedroom;
        Teleport(new Vector3(-5.5f, 0.05f, -14.4f), 135f);
        playerCamera.fieldOfView = 52f;
        locked = true;
        StartCoroutine(WakeUpSequence());
    }

    private IEnumerator WakeUpSequence()
    {
        fade.color = Color.black;
        yield return new WaitForSeconds(0.8f);
        yield return FadeTo(0f, 2.6f);
        yield return Say("Lacey: It was that dream again.", 2.5f);
        yield return Say("Lacey: Public school. The alley. I hate that I still remember it.", 3.2f);
        yield return Say("Morning waits in the room, but it does not feel kind.", 3f);
        StartRoutineDay(1);
    }

    private void Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float lookSpeed = 2.5f;
        Vector2 lookDelta = mouse.delta.ReadValue();
        yaw += lookDelta.x * lookSpeed * 0.05f;
        pitch -= lookDelta.y * lookSpeed * 0.05f;
        pitch = Mathf.Clamp(pitch, -72f, 72f);
        player.localRotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void CouchLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (couchInspecting && inspectedCouchSpot != null)
        {
            Vector3 direction = inspectedCouchSpot.transform.position - playerCamera.transform.position;
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            player.rotation = Quaternion.Lerp(player.rotation, Quaternion.Euler(0f, lookRotation.eulerAngles.y, 0f), Time.deltaTime * 6f);
            cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation, Quaternion.Euler(NormalizeAngle(lookRotation.eulerAngles.x), 0f, 5f), Time.deltaTime * 6f);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, 34f, Time.deltaTime * 5f);
            chromaticAberration.intensity.Override(0.1f);
            volumeVignette.intensity.Override(0.48f);
            vignette.color = new Color(0f, 0f, 0f, 0.24f);
            return;
        }

        Vector2 viewport = mouse.position.ReadValue();
        float x = Mathf.Clamp01(viewport.x / Mathf.Max(1f, Screen.width));
        float y = Mathf.Clamp01(viewport.y / Mathf.Max(1f, Screen.height));
        float lookX = (x - 0.5f) * 2f;
        float lookY = (y - 0.5f) * 2f;
        yaw = couchBaseYaw + lookX * CouchYawLimit;
        pitch = couchBasePitch - lookY * CouchPitchLimit;
        player.localRotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 5f);

        float edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(lookX), Mathf.Abs(lookY)));
        chromaticAberration.intensity.Override(Mathf.Lerp(0.08f, 0.5f, edge));
        volumeVignette.intensity.Override(Mathf.Lerp(0.55f, 0.82f, edge));
        vignette.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.28f, 0.54f, edge));
    }

    private void Move()
    {
        if (locked)
        {
            character.SimpleMove(Vector3.zero);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            character.SimpleMove(Vector3.zero);
            return;
        }

        float x = 0f;
        float z = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            x -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            x += 1f;
        }
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            z -= 1f;
        }
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            z += 1f;
        }

        float speed = GetMovementSpeed();
        Vector3 input = new Vector3(x, 0f, z);
        input = Vector3.ClampMagnitude(input, 1f);
        Vector3 world = player.TransformDirection(input) * speed;
        character.SimpleMove(world);
    }

    private float GetMovementSpeed()
    {
        if (stage == Stage.Alley)
        {
            return 2.2f;
        }

        if (stage == Stage.HomeRoutine || stage == Stage.CouchSleep)
        {
            if (currentDay == 2)
            {
                return 2.35f;
            }

            if (currentDay >= 3)
            {
                return 1.75f;
            }
        }

        return 3.1f;
    }

    private void UpdateInteraction()
    {
        focusedInteractable = null;
        promptText.text = string.Empty;

        Collider[] hits = Physics.OverlapSphere(player.position + Vector3.up * 0.8f, interactionDistance);
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Interactable interactable = hits[i].GetComponent<Interactable>();
            if (interactable == null || !interactable.CanUse(stage))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(interactable.RoutineId) && interactable.RoutineId != activeRoutineId)
            {
                continue;
            }

            float distance = Vector3.Distance(player.position, interactable.transform.position);
            if (distance > interactionDistance)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                focusedInteractable = interactable;
            }
        }

        if (focusedInteractable != null && !sequenceRunning)
        {
            promptText.text = "E - " + focusedInteractable.Prompt;
        }
    }

    private void UpdateCouchInteraction()
    {
        promptText.text = string.Empty;
        Mouse mouse = Mouse.current;
        if (mouse == null || playerCamera == null)
        {
            return;
        }

        if (couchInspecting)
        {
            if (sequenceRunning)
            {
                return;
            }

            promptText.text = "Click - Stop inspecting";
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (inspectedCouchSpot != null && inspectedCouchSpot.Id == "Pills" && TryClickPillsLid(mouse.position.ReadValue()))
                {
                    return;
                }

                couchInspecting = false;
                inspectedCouchSpot = null;
                if (!goodEndingPillsLidFound)
                {
                    pillsLidFlashing = false;
                    if (pillsLidRenderer != null)
                    {
                        pillsLidRenderer.material.color = pillsLidBaseColor;
                    }
                }
                dialogueText.text = string.Empty;
                objectiveText.text = activeCouchObjective == "Investigate3" ? "Scene 3\nLook around. Click the TV when you are done." : string.Empty;
                playerCamera.fieldOfView = 58f;
            }
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && !sequenceRunning && activeCouchObjective == "GetUp")
        {
            StartCoroutine(TryGetUpFromCouch());
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && !sequenceRunning && activeCouchObjective == "Help")
        {
            StartCoroutine(CallForHelp());
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Collide);
        CouchSpot closestSpot = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            CouchSpot spot = hits[i].collider.GetComponent<CouchSpot>();
            if (spot != null && IsCouchSpotAvailable(spot))
            {
                if (hits[i].distance < closestDistance)
                {
                    closestDistance = hits[i].distance;
                    closestSpot = spot;
                }
            }
        }

        if (closestSpot != null)
        {
            promptText.text = "Click - " + closestSpot.Label;
            if (mouse.leftButton.wasPressedThisFrame && !sequenceRunning)
            {
                UseCouchSpot(closestSpot);
            }
        }
    }

    private void UseCouchSpot(CouchSpot spot)
    {
        if (activeCouchObjective == "Parents3" && spot.Id == "Parents")
        {
            StartCoroutine(InspectParentsSceneThree(spot));
            return;
        }

        if (activeCouchObjective == "Investigate3" && spot.Id == "TV")
        {
            StartCoroutine(SceneThreeDoorKnockSequence());
            return;
        }

        if (activeCouchObjective == "TVSleep3" && spot.Id == "TV")
        {
            StartCoroutine(FallAsleepIntoSceneFour());
            return;
        }

        if (activeCouchObjective == "Food3" && spot.Id == "Food")
        {
            StartCoroutine(InspectSceneThreeFood());
            return;
        }

        if (activeCouchObjective == "TVSleep" && spot.Id == "TV")
        {
            StartCoroutine(FallAsleepIntoSceneTwo());
            return;
        }

        if (activeCouchObjective == "Food" && spot.Id == "Food")
        {
            StartCoroutine(InspectFoodThenNight());
            return;
        }

        if (!string.IsNullOrEmpty(spot.Description))
        {
            StartInspectingCouchSpot(spot);
        }
    }

    private bool IsCouchSpotAvailable(CouchSpot spot)
    {
        if (spot.Id == "Parents")
        {
            return activeCouchObjective == "Parents3";
        }

        if (spot.Id == "Food")
        {
            return foodPlate != null && foodPlate.activeSelf && (activeCouchObjective == "Food" || activeCouchObjective == "Food3");
        }

        if (spot.Id == "TV")
        {
            return activeCouchObjective == "TVSleep" || activeCouchObjective == "TVSleep3" || activeCouchObjective == "Investigate3" || (string.IsNullOrEmpty(activeCouchObjective) && !sequenceRunning);
        }

        if (spot.Id == "PillsLid")
        {
            return couchInspecting && inspectedCouchSpot != null && inspectedCouchSpot.Id == "Pills" && pillsLidFlashing && !goodEndingPillsLidFound;
        }

        if (activeCouchObjective == "Investigate3")
        {
            return spot.Id == "Key" || spot.Id == "Pills" || spot.Id == "Painting" || spot.Id == "WindowPart" || spot.Id == "Hallway" || spot.Id == "CouchEnd" || spot.Id == "Cabinet";
        }

        return string.IsNullOrEmpty(activeCouchObjective) && !sequenceRunning && (spot.Id == "Hallway" || spot.Id == "Window");
    }

    private void StartInspectingCouchSpot(CouchSpot spot)
    {
        couchInspecting = true;
        inspectedCouchSpot = spot;
        dialogueText.text = spot.Description;
        objectiveText.text = "Inspecting\nClick again to look away.";
        pillsLidFlashing = spot.Id == "Pills" && !goodEndingPillsLidFound;
    }

    private bool TryClickPillsLid(Vector2 mousePosition)
    {
        Ray ray = playerCamera.ScreenPointToRay(mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            CouchSpot spot = hits[i].collider.GetComponent<CouchSpot>();
            if (spot != null && spot.Id == "PillsLid")
            {
                goodEndingPillsLidFound = true;
                pillsLidFlashing = false;
                if (pillsLidRenderer != null)
                {
                    pillsLidRenderer.material.color = new Color(1f, 0.86f, 0.12f);
                }

                dialogueText.text = "The lid catches your eye. Something about it feels important.";
                objectiveText.text = "Clue remembered";
                return true;
            }
        }

        return false;
    }

    private IEnumerator FallAsleepIntoSceneTwo()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        yield return Say("The TV fills the room because nothing else will.", 2.8f);
        yield return FadeTo(1f, 3f);
        yield return new WaitForSeconds(0.8f);
        StartCouchSceneTwo();
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return Mathf.Clamp(angle, -35f, 30f);
    }

    private IEnumerator FadeFromBlack(string line)
    {
        locked = true;
        fade.color = Color.black;
        yield return FadeTo(0f, 2f);
        locked = false;
        yield return Say(line, 4f);
    }

    private IEnumerator FadeTo(float alpha, float duration)
    {
        Color start = fade.color;
        Color end = new Color(0f, 0f, 0f, alpha);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            fade.color = Color.Lerp(start, end, timer / duration);
            yield return null;
        }

        fade.color = end;
    }

    private IEnumerator Say(string text, float seconds)
    {
        dialogueText.text = text;
        yield return new WaitForSeconds(seconds);
        dialogueText.text = string.Empty;
    }

    private void Teleport(Vector3 position, float newYaw)
    {
        character.enabled = false;
        player.position = position;
        yaw = newYaw;
        pitch = 0f;
        player.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.identity;
        character.enabled = true;
    }

    private void AddInteraction(string name, Vector3 position, Vector3 size, string prompt, System.Action action)
    {
        GameObject trigger = new GameObject(name);
        trigger.transform.position = position;
        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        Interactable interactable = trigger.AddComponent<Interactable>();
        interactable.Prompt = prompt;
        interactable.Action = action;
    }

    private void AddRoutineInteraction(string name, Vector3 position, Vector3 size, string routineId, string prompt)
    {
        GameObject trigger = new GameObject(name);
        trigger.transform.position = position;
        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        Interactable interactable = trigger.AddComponent<Interactable>();
        interactable.Prompt = prompt;
        interactable.RoutineId = routineId;
        interactable.Action = () => UseRoutineStation(routineId);
    }

    private void AddCouchSpot(string name, Vector3 position, Vector3 size, string id, string description)
    {
        GameObject spot = new GameObject(name);
        spot.transform.position = position;
        BoxCollider collider = spot.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        CouchSpot couchSpot = spot.AddComponent<CouchSpot>();
        couchSpot.Id = id;
        couchSpot.Label = id;
        couchSpot.Description = description;
    }

    private void CreateHalfCircleFloor(string name, Transform parent, Vector3 center, float radius, int segments, Color color)
    {
        GameObject floor = new GameObject(name);
        floor.transform.SetParent(parent);
        floor.transform.position = center;

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(180f, 360f, i / (float)segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = name + " Mesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        MeshFilter filter = floor.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        MeshRenderer renderer = floor.AddComponent<MeshRenderer>();
        renderer.material = CreateMaterial(color);
    }

    private void CreateArcWalls(Transform parent, Vector3 center, float radius, int segments, Color color)
    {
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            float angle = Mathf.Lerp(205f, 335f, t);
            float radians = angle * Mathf.Deg2Rad;
            Vector3 position = center + new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
            GameObject segment = CreateCube("Living Room Curved Wall", parent, position, new Vector3(0.82f, 2.7f, 0.12f), color);
            segment.transform.rotation = Quaternion.Euler(0f, 90f - angle, 0f);
        }
    }

    private void StartRoutineDay(int day)
    {
        currentDay = day;
        routineStep = 0;
        stage = Stage.HomeRoutine;
        couchMode = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        sequenceRunning = false;
        locked = true;
        bedroomDoorOpen = false;
        if (bedroomDoor != null)
        {
            bedroomDoor.transform.position = new Vector3(-4.55f, 1.1f, -16.05f);
            bedroomDoor.transform.rotation = Quaternion.identity;
        }
        if (bedroomDoorCollider != null)
        {
            bedroomDoorCollider.enabled = true;
        }
        playerCamera.fieldOfView = Mathf.Lerp(52f, 47f, Mathf.Clamp01((day - 1) / 3f));
        volumeVignette.intensity.Override(day == 1 ? 0.32f : day == 2 ? 0.44f : 0.58f);
        chromaticAberration.intensity.Override(day == 1 ? 0.06f : day == 2 ? 0.11f : 0.18f);

        Teleport(new Vector3(-5.5f, 0.05f, -14.4f), 135f);
        SetNextRoutineObjective();

        if (day == 1)
        {
            StartCoroutine(Say("Day 1. The house feels ordinary enough if you do not listen too closely.", 3.5f));
        }
        else if (day == 2)
        {
            StartCoroutine(Say("Day 2. Every small thing takes longer. Your body answers late.", 3.5f));
        }
        else if (day == 3)
        {
            StartCoroutine(Say("Day 3. The room has not changed, but every distance has.", 3.5f));
        }
        else
        {
            StartCoroutine(Say("Day 4. You are tired before the morning starts.", 3.5f));
        }
    }

    private void SetNextRoutineObjective()
    {
        string[] plan = GetRoutinePlan(currentDay);
        if (routineStep >= plan.Length)
        {
            activeRoutineId = string.Empty;
            objectiveText.text = string.Empty;
            return;
        }

        activeRoutineId = plan[routineStep];
        objectiveText.text = "Day " + currentDay + "\n" + GetObjectiveText(activeRoutineId);
    }

    private string[] GetRoutinePlan(int day)
    {
        if (day == 3)
        {
            return new[] { "Wake", "Breakfast", "Homework", "TV", "Sleep" };
        }

        if (day == 4)
        {
            return new[] { "Wake", "Breakfast", "Homework", "TV", "Couch" };
        }

        return new[] { "Wake", "Breakfast", "Homework", "TV", "Bathroom", "Sleep" };
    }

    private string GetObjectiveText(string routineId)
    {
        switch (routineId)
        {
            case "Wake":
                return "Get out of bed.";
            case "Breakfast":
                return "Eat breakfast.";
            case "Homework":
                return "Do school work.";
            case "TV":
                return "Watch TV.";
            case "Bathroom":
                return "Use the bathroom.";
            case "Sleep":
                return "Go back to bed.";
            case "Couch":
                return "Rest on the couch.";
            default:
                return string.Empty;
        }
    }

    private void UseRoutineStation(string routineId)
    {
        if (routineId != activeRoutineId || stage != Stage.HomeRoutine)
        {
            return;
        }

        StartCoroutine(DoRoutineTask(routineId));
    }

    private IEnumerator DoRoutineTask(string routineId)
    {
        sequenceRunning = true;
        locked = true;
        promptText.text = string.Empty;

        string label = GetRoutineTaskLine(routineId);
        float duration = GetRoutineDuration(routineId);
        yield return HoldTask(label, duration);

        if (routineId == "TV" && currentDay == 3)
        {
            yield return Say("Lacey: I cannot do the bathroom right now. I just need to lie down.", 3f);
        }

        if (routineId == "Couch")
        {
            yield return CouchSleepSequence();
            yield break;
        }

        routineStep++;
        string[] plan = GetRoutinePlan(currentDay);
        if (routineStep >= plan.Length)
        {
            yield return EndRoutineDay();
            yield break;
        }

        SetNextRoutineObjective();
        locked = false;
        sequenceRunning = false;
    }

    private string GetRoutineTaskLine(string routineId)
    {
        if (routineId == "Wake" && currentDay == 1)
        {
            return "Getting out of bed...";
        }

        if (routineId == "Wake" && currentDay >= 3)
        {
            return "Trying to get your legs under you...";
        }

        switch (routineId)
        {
            case "Breakfast":
                return currentDay == 1 ? "Eating breakfast..." : "Eating slowly...";
            case "Homework":
                return currentDay >= 3 ? "Forcing your hand across the page..." : "Doing school work...";
            case "TV":
                return currentDay == 4 ? "Watching TV. Your eyes keep closing..." : "Watching TV...";
            case "Bathroom":
                return currentDay == 2 ? "Showering, brushing teeth, using the toilet. It takes too long..." : "Showering, brushing teeth, using the toilet...";
            case "Sleep":
                return currentDay >= 3 ? "Getting back to bed..." : "Going back to bed...";
            case "Couch":
                return "Sitting down for a moment...";
            default:
                return "Waiting...";
        }
    }

    private float GetRoutineDuration(string routineId)
    {
        float baseDuration = routineId == "Bathroom" ? 4.4f : 2.8f;
        if (currentDay == 2)
        {
            return baseDuration * 1.3f;
        }

        if (currentDay == 3)
        {
            return baseDuration * 1.7f;
        }

        if (currentDay >= 4)
        {
            return baseDuration * 1.75f;
        }

        return baseDuration;
    }

    private IEnumerator HoldTask(string label, float seconds)
    {
        float timer = 0f;
        while (timer < seconds)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / seconds);
            dialogueText.text = label + "\n" + Mathf.RoundToInt(progress * 100f) + "%";
            cameraPivot.localPosition = new Vector3(Mathf.Sin(Time.time * 8f) * 0.01f, 1.52f - progress * 0.04f, 0f);
            yield return null;
        }

        cameraPivot.localPosition = new Vector3(0f, 1.52f, 0f);
        dialogueText.text = string.Empty;
    }

    private IEnumerator EndRoutineDay()
    {
        objectiveText.text = string.Empty;
        fade.color = new Color(0f, 0f, 0f, 0f);
        yield return FadeTo(1f, 1.6f);
        yield return new WaitForSeconds(0.6f);

        if (currentDay == 1)
        {
            yield return Say("Night comes easily. Morning does not.", 2.2f);
            yield return FadeTo(0f, 1.4f);
            StartRoutineDay(2);
        }
        else if (currentDay == 2)
        {
            yield return Say("Your body feels farther away than yesterday.", 2.6f);
            yield return FadeTo(0f, 1.4f);
            StartRoutineDay(3);
        }
        else if (currentDay == 3)
        {
            yield return Say("You skipped what you could not face. Sleep still finds you.", 3f);
            yield return FadeTo(0f, 1.4f);
            StartRoutineDay(4);
        }
    }

    private IEnumerator CouchSleepSequence()
    {
        stage = Stage.CouchSleep;
        objectiveText.text = string.Empty;
        yield return Say("The TV keeps talking after you stop listening.", 2.8f);
        yield return FadeTo(1f, 3f);
        yield return new WaitForSeconds(0.8f);
        StartCouchSceneOne();
    }

    private void StartCouchSceneOne()
    {
        couchScene = 1;
        couchMode = true;
        stage = Stage.CouchSleep;
        sequenceRunning = true;
        locked = true;
        activeCouchObjective = string.Empty;
        couchInspecting = false;
        inspectedCouchSpot = null;
        foodPlate.SetActive(false);
        tvGlow.GetComponent<Renderer>().material.color = new Color(0.02f, 0.03f, 0.035f);
        TeleportToCouch();
        StartCoroutine(CouchSceneOneSequence());
    }

    private void StartCouchSceneTwo()
    {
        couchScene = 2;
        couchMode = true;
        stage = Stage.CouchSleep;
        sequenceRunning = true;
        locked = true;
        activeCouchObjective = string.Empty;
        couchInspecting = false;
        inspectedCouchSpot = null;
        TeleportToCouch();
        StartCoroutine(CouchSceneTwoSequence());
    }

    private void StartCouchSceneThree()
    {
        couchScene = 3;
        couchMode = true;
        stage = Stage.CouchSleep;
        sequenceRunning = true;
        locked = true;
        activeCouchObjective = string.Empty;
        couchInspecting = false;
        inspectedCouchSpot = null;
        TeleportToCouch();
        StartCoroutine(CouchSceneThreeSequence());
    }

    private void TeleportToCouch()
    {
        character.enabled = false;
        player.position = new Vector3(0f, 0.05f, -24.7f);
        cameraPivot.localPosition = new Vector3(0.15f, 1.18f, 0.28f);
        couchBaseYaw = -18f;
        couchBasePitch = -10f;
        yaw = couchBaseYaw;
        pitch = couchBasePitch;
        player.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 5f);
        character.enabled = true;
        playerCamera.fieldOfView = 58f;
        playerCamera.nearClipPlane = 0.03f;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        volumeVignette.intensity.Override(0.62f);
        chromaticAberration.intensity.Override(0.12f);
    }

    private IEnumerator CouchSceneOneSequence()
    {
        yield return FadeTo(0f, 2.6f);
        yield return Say("Day 5. You wake up on the couch.", 2.5f);
        yield return Say("Lacey: Why am I still here?", 2.2f);
        yield return Say("Lacey: I need to get up.", 2f);
        activeCouchObjective = "GetUp";
        objectiveText.text = "Scene 1\nClick to try getting up.";
        sequenceRunning = false;
    }

    private IEnumerator TryGetUpFromCouch()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        float duration = 10f;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration) * 20f;
            dialogueText.text = "Trying to get up...\n" + Mathf.RoundToInt(progress) + "%";
            cameraPivot.localPosition = Vector3.Lerp(new Vector3(0.15f, 1.18f, 0.28f), new Vector3(0.15f, 1.34f, 0.45f), timer / duration);
            pitch = Mathf.Lerp(couchBasePitch, 6f, timer / duration);
            yield return null;
        }

        dialogueText.text = string.Empty;
        yield return Say("Your body gives out.", 1.6f);
        cameraPivot.localPosition = new Vector3(0.15f, 1.18f, 0.28f);
        pitch = couchBasePitch;
        activeCouchObjective = "Help";
        objectiveText.text = "Scene 1\nClick to call for help.";
        sequenceRunning = false;
    }

    private IEnumerator CallForHelp()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        yield return Say("Lacey: help", 1.8f);
        yield return Say("Lacey: What if they leave again?", 2.8f);
        yield return Say("Lacey: What if no one checks?", 2.8f);
        yield return new WaitForSeconds(5f);
        yield return DotPause();
        yield return Say("It's so quiet.", 2.4f);
        yield return new WaitForSeconds(10f);
        yield return ParentsVacationArgument();
    }

    private IEnumerator ParentsVacationArgument()
    {
        mom.gameObject.SetActive(true);
        dad.gameObject.SetActive(true);
        yield return MovePerson(mom, new Vector3(4.8f, 0f, -19.2f), new Vector3(2.5f, 0f, -21.2f), 2.4f);
        yield return MovePerson(dad, new Vector3(5.55f, 0f, -19.6f), new Vector3(3.15f, 0f, -21.55f), 1.8f);
        yield return Say("Mother: We are going on vacation again.", 2.4f);
        yield return Say("Lacey: Please don't leave me.", 2.6f);
        yield return Say("Father: We'll put the TV on before we go.", 2.4f);
        yield return Say("Lacey: I can't get up. I can't.", 2.6f);
        yield return Say("Mother: If you want to come with us, then get off the couch.", 3f);
        yield return Say("Lacey: I can't.", 2f);
        yield return Say("Mother: Then stay where you are.", 2.2f);
        tvGlow.GetComponent<Renderer>().material.color = new Color(0.12f, 0.19f, 0.24f);
        livingRoomLight.intensity = 2.4f;
        yield return MovePerson(mom, new Vector3(2.5f, 0f, -21.2f), new Vector3(6.3f, 0f, -24.7f), 2.8f);
        yield return MovePerson(dad, new Vector3(3.15f, 0f, -21.55f), new Vector3(6.6f, 0f, -24.2f), 2.8f);
        mom.gameObject.SetActive(false);
        dad.gameObject.SetActive(false);
        yield return Say("A car starts outside. It gets smaller until it is gone.", 4f);
        yield return Say("Lacey: They heard the words, but not what I meant.", 3.2f);
        activeCouchObjective = "TVSleep";
        objectiveText.text = "Scene 1\nLook at the TV and click it.";
        sequenceRunning = false;
    }

    private IEnumerator CouchSceneTwoSequence()
    {
        fade.color = Color.black;
        tvGlow.GetComponent<Renderer>().material.color = new Color(0.02f, 0.03f, 0.035f);
        foodPlate.SetActive(false);
        mom.gameObject.SetActive(true);
        dad.gameObject.SetActive(true);
        mom.position = new Vector3(5.9f, 0f, -23.5f);
        dad.position = new Vector3(6.4f, 0f, -23.9f);
        yield return FadeTo(0f, 2.5f);
        yield return Say("Mother: We're back!", 2f);
        yield return MovePerson(mom, mom.position, new Vector3(2.4f, 0f, -21.25f), 2.6f);
        yield return MovePerson(dad, dad.position, new Vector3(3.2f, 0f, -21.6f), 2.2f);
        yield return Say("Father: How have you been?", 2.2f);
        yield return Say("Lacey: ba-", 1.7f);
        yield return Say("The rest of the word will not come out.", 2.4f);
        yield return MovePerson(mom, new Vector3(2.4f, 0f, -21.25f), new Vector3(4.8f, 0f, -17f), 2f);
        yield return MovePerson(dad, new Vector3(3.2f, 0f, -21.6f), new Vector3(5.8f, 0f, -18f), 2f);
        dad.gameObject.SetActive(false);
        yield return DotPause();
        mom.position = new Vector3(3.8f, 0f, -17f);
        yield return MovePerson(mom, mom.position, new Vector3(0.2f, 0f, -22.4f), 2.4f);
        foodPlate.SetActive(true);
        foodPlate.GetComponent<Renderer>().material.color = new Color(0.68f, 0.56f, 0.36f);
        yield return Say("Mother: I brought you something to eat.", 2.4f);
        yield return Say("Lacey: I can't move.", 2.2f);
        yield return Say("Your mouth barely makes the shape of it.", 2.4f);
        yield return MovePerson(mom, new Vector3(0.2f, 0f, -22.4f), new Vector3(4.4f, 0f, -18.3f), 2.4f);
        mom.gameObject.SetActive(false);
        activeCouchObjective = "Food";
        objectiveText.text = "Scene 2\nClick the food.";
        sequenceRunning = false;
    }

    private IEnumerator InspectFoodThenNight()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        yield return Say("It looks good. Warm. Close.", 2.6f);
        yield return Say("All you can do is look at it.", 2.8f);
        float timer = 0f;
        float duration = 30f;
        bool figureShown = false;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            RenderSettings.fogDensity = Mathf.Lerp(0.018f, 0.036f, t);
            livingRoomLight.intensity = Mathf.Lerp(1.1f, 0.4f, t);
            if (timer > 8f && timer < 13f)
            {
                dialogueText.text = "Lacey: I am so bored.";
            }
            else if (timer > 17f && timer < 22f)
            {
                dialogueText.text = "Lacey: I want to stand up. Just once.";
            }
            else
            {
                dialogueText.text = string.Empty;
            }

            if (!figureShown && timer > 20f)
            {
                figureShown = true;
                StartCoroutine(ScareFigurePeek());
            }

            yield return null;
        }

        dialogueText.text = string.Empty;
        mom.gameObject.SetActive(true);
        mom.position = new Vector3(4.8f, 0f, -18.3f);
        yield return MovePerson(mom, mom.position, new Vector3(1.8f, 0f, -21.5f), 2.4f);
        tvGlow.GetComponent<Renderer>().material.color = new Color(0.12f, 0.19f, 0.24f);
        livingRoomLight.intensity = 1.8f;
        yield return Say("Mother: I'll turn this on for you. Goodnight.", 3f);
        yield return MovePerson(mom, new Vector3(1.8f, 0f, -21.5f), new Vector3(4.8f, 0f, -18.3f), 2.2f);
        mom.gameObject.SetActive(false);
        yield return FadeTo(1f, 3f);
        yield return new WaitForSeconds(0.8f);
        StartCouchSceneThree();
    }

    private IEnumerator CouchSceneThreeSequence()
    {
        fade.color = Color.black;
        foodPlate.SetActive(false);
        tvGlow.GetComponent<Renderer>().material.color = new Color(0.12f, 0.19f, 0.24f);
        livingRoomLight.intensity = 1.7f;
        RenderSettings.fogDensity = 0.028f;
        mom.gameObject.SetActive(true);
        dad.gameObject.SetActive(true);
        mom.position = new Vector3(2.3f, 0f, -21.2f);
        dad.position = new Vector3(3.05f, 0f, -21.55f);
        yield return FadeTo(0f, 2.3f);
        yield return Say("The TV is still running.", 2.2f);
        yield return Say("Your parents watch it like you are not in the room.", 3f);
        activeCouchObjective = "Parents3";
        objectiveText.text = "Scene 3\nClick your parents.";
        sequenceRunning = false;
    }

    private IEnumerator InspectParentsSceneThree(CouchSpot spot)
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        couchInspecting = true;
        inspectedCouchSpot = spot;
        dialogueText.text = string.Empty;
        yield return new WaitForSeconds(0.9f);
        yield return Say("Lacey: Do they even realise that I can't move?", 3f);
        yield return Say("Lacey: Do they really believe the only reason I'm not getting off is because I love this couch so much?", 4.2f);
        yield return Say("Lacey: I don't like it. Far from it.", 2.8f);
        yield return Say("Lacey: I wanna stand up more than anything.", 3f);
        couchInspecting = false;
        inspectedCouchSpot = null;
        playerCamera.fieldOfView = 58f;
        yield return new WaitForSeconds(5f);
        yield return Say("A breath moves somewhere behind you.", 2.3f);
        yield return Say("Lacey: Did I just hear something coming from behind the couch?", 3.2f);
        yield return Say("Lacey: I must just be imagining things.", 2.8f);
        yield return new WaitForSeconds(10f);
        yield return MovePerson(mom, mom.position, new Vector3(4.9f, 0f, -20.2f), 2.5f);
        yield return MovePerson(dad, dad.position, new Vector3(5.55f, 0f, -20.7f), 2.2f);
        mom.gameObject.SetActive(false);
        dad.gameObject.SetActive(false);
        activeCouchObjective = "Investigate3";
        objectiveText.text = "Scene 3\nLook around. Click the TV when you are done.";
        sequenceRunning = false;
    }

    private IEnumerator SceneThreeDoorKnockSequence()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        dialogueText.text = string.Empty;
        if (!goodEndingPillsLidFound && pillsLidRenderer != null)
        {
            pillsLidFlashing = false;
            pillsLidRenderer.material.color = pillsLidBaseColor;
        }

        yield return Say("A knock lands on the front door.", 2.3f);
        mom.gameObject.SetActive(true);
        dad.gameObject.SetActive(true);
        mom.position = new Vector3(4.9f, 0f, -20.2f);
        dad.position = new Vector3(5.55f, 0f, -20.7f);
        yield return MovePerson(mom, mom.position, new Vector3(5.05f, 0f, -24.65f), 2.4f);
        yield return MovePerson(dad, dad.position, new Vector3(5.85f, 0f, -24.4f), 2.1f);
        yield return Say("Their voices soften in the hall.", 2.4f);
        yield return Say("The other voice is too far away to understand.", 3f);
        yield return Say("Mother: No sir, everything is fine, thank you.", 3f);
        yield return Say("Father: We appreciate you checking.", 2.4f);
        yield return Say("Lacey: What was that about?", 2.4f);
        yield return MovePerson(dad, dad.position, new Vector3(5.55f, 0f, -20.7f), 2f);
        dad.gameObject.SetActive(false);
        yield return MovePerson(mom, mom.position, new Vector3(0.15f, 0f, -22.35f), 2.6f);
        foodPlate.SetActive(true);
        foodPlate.GetComponent<Renderer>().material.color = new Color(0.7f, 0.58f, 0.38f);
        yield return Say("Mother: I brought you something to eat.", 2.4f);
        yield return MovePerson(mom, mom.position, new Vector3(4.8f, 0f, -18.3f), 2.4f);
        mom.gameObject.SetActive(false);
        activeCouchObjective = "Food3";
        objectiveText.text = "Scene 3\nClick the food.";
        sequenceRunning = false;
    }

    private IEnumerator InspectSceneThreeFood()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        yield return Say("Another plate. Close enough to smell.", 2.5f);
        yield return Say("All you can do is look at it.", 2.5f);
        activeCouchObjective = "TVSleep3";
        objectiveText.text = "Scene 3\nLook at the TV and click it.";
        sequenceRunning = false;
    }

    private IEnumerator FallAsleepIntoSceneFour()
    {
        sequenceRunning = true;
        activeCouchObjective = string.Empty;
        objectiveText.text = string.Empty;
        yield return Say("The TV keeps talking until the words become light.", 3f);
        yield return FadeTo(1f, 3f);
        objectiveText.text = "Prototype end\nScene 4 begins next.";
        sequenceRunning = true;
    }

    private IEnumerator ScareFigurePeek()
    {
        hallwayFigure.SetActive(true);
        yield return new WaitForSeconds(2.2f);
        hallwayFigure.SetActive(false);
        yield return Say("A shape was there. Or you needed one to be there.", 2.2f);
    }

    private IEnumerator DotPause()
    {
        dialogueText.text = ".";
        yield return new WaitForSeconds(1f);
        dialogueText.text = "..";
        yield return new WaitForSeconds(1f);
        dialogueText.text = "...";
        yield return new WaitForSeconds(1f);
        dialogueText.text = string.Empty;
    }

    private IEnumerator MovePerson(Transform person, Vector3 from, Vector3 to, float duration)
    {
        float timer = 0f;
        person.position = from;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            person.position = Vector3.Lerp(from, to, timer / duration);
            yield return null;
        }

        person.position = to;
    }

    private GameObject CreateRoom(Transform parent, Vector3 center, Vector3 size, Color wall, Color floor)
    {
        GameObject container = new GameObject("Room Shell");
        container.transform.SetParent(parent);
        CreateCube("Floor", container.transform, center + new Vector3(0f, -size.y * 0.5f, 0f), new Vector3(size.x, 0.08f, size.z), floor);
        CreateCube("Ceiling", container.transform, center + new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, 0.08f, size.z), wall * 0.8f);
        CreateCube("Back Wall", container.transform, center + new Vector3(0f, 0f, size.z * 0.5f), new Vector3(size.x, size.y, 0.1f), wall);
        CreateCube("Front Wall", container.transform, center + new Vector3(0f, 0f, -size.z * 0.5f), new Vector3(size.x, size.y, 0.1f), wall * 0.75f);
        CreateCube("Left Wall", container.transform, center + new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(0.1f, size.y, size.z), wall * 0.85f);
        CreateCube("Right Wall", container.transform, center + new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(0.1f, size.y, size.z), wall * 0.85f);
        return container;
    }

    private GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().material = CreateMaterial(color);
        return cube;
    }

    private void CreatePerson(string name, Transform parent, Vector3 position, Color color, string label)
    {
        GameObject person = new GameObject(name);
        person.transform.SetParent(parent);
        person.transform.position = position;
        CreateCube(name + " Body", person.transform, position + Vector3.up * 0.75f, new Vector3(0.38f, 1.1f, 0.28f), color);
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = name + " Head";
        head.transform.SetParent(person.transform);
        head.transform.position = position + Vector3.up * 1.45f;
        head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        head.GetComponent<Renderer>().material = CreateMaterial(color * 1.15f);

        if (!string.IsNullOrEmpty(label))
        {
            GameObject labelObject = new GameObject(name + " Label");
            labelObject.transform.SetParent(person.transform);
            labelObject.transform.position = position + Vector3.up * 2.05f;
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.12f;
            text.color = new Color(0.9f, 0.86f, 0.74f);
        }
    }

    private Transform CreatePersonObject(string name, Transform parent, Vector3 position, Color color)
    {
        GameObject person = new GameObject(name);
        person.transform.SetParent(parent);
        person.transform.position = position;
        CreateCube(name + " Body", person.transform, position + Vector3.up * 0.75f, new Vector3(0.42f, 1.1f, 0.3f), color);
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = name + " Head";
        head.transform.SetParent(person.transform);
        head.transform.position = position + Vector3.up * 1.45f;
        head.transform.localScale = new Vector3(0.44f, 0.44f, 0.44f);
        head.GetComponent<Renderer>().material = CreateMaterial(color * 1.12f);
        return person.transform;
    }

    private Light CreateLight(string name, Transform parent, Vector3 position, LightType type, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        return light;
    }

    private Material CreateMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        return material;
    }

    private Image CreatePanel(string name, Color color, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private Text CreateText(string name, Transform parent, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private sealed class Interactable : MonoBehaviour
    {
        public string Prompt;
        public string RoutineId;
        public System.Action Action;

        public bool CanUse(Stage stage)
        {
            if (name.Contains("Classmate") && stage != Stage.Classroom)
            {
                return false;
            }

            if (name.Contains("Exit") && stage != Stage.Hallway)
            {
                return false;
            }

            if (name.Contains("Alley") && stage != Stage.Alley)
            {
                return false;
            }

            if (name.Contains("Bedroom Door"))
            {
                return stage == Stage.HomeRoutine && instance != null && !instance.bedroomDoorOpen;
            }

            if (!string.IsNullOrEmpty(RoutineId) && stage != Stage.HomeRoutine)
            {
                return false;
            }

            return true;
        }

        public void Use()
        {
            if (Action != null)
            {
                Action.Invoke();
            }
        }
    }

    private sealed class CouchSpot : MonoBehaviour
    {
        public string Id;
        public string Label;
        public string Description;
    }

    private sealed class FlickerLight : MonoBehaviour
    {
        private Light target;
        private float baseIntensity;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
        }

        private void Update()
        {
            float pulse = Mathf.PerlinNoise(Time.time * 14f, 0.3f);
            target.intensity = baseIntensity * Mathf.Lerp(0.35f, 1.15f, pulse);
        }
    }
}
