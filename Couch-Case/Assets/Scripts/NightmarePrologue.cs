using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        if (instance != null)
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
    private float interactionDistance = 2.8f;
    private Interactable focusedInteractable;
    private int currentDay = 1;
    private int routineStep;
    private string activeRoutineId = string.Empty;

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
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }

        Look();
        Move();
        UpdateInteraction();

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame && focusedInteractable != null && !sequenceRunning)
        {
            focusedInteractable.Use();
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
        BuildBedroom(root.transform);
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

        CreatePerson("Classmate", room.transform, new Vector3(4.15f, 0f, -2.2f), new Color(0.11f, 0.12f, 0.16f), "He keeps looking back at you.");
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

        CreatePerson("Classmate Shadow", alley.transform, new Vector3(32.2f, 0f, -3.8f), new Color(0.02f, 0.02f, 0.025f), "Waiting at the end.");
        CreatePerson("Friend Shadow A", alley.transform, new Vector3(30.9f, 0f, -4.85f), new Color(0.025f, 0.025f, 0.03f), string.Empty);
        CreatePerson("Friend Shadow B", alley.transform, new Vector3(31.1f, 0f, -2.75f), new Color(0.025f, 0.025f, 0.03f), string.Empty);
        CreatePerson("Friend Shadow C", alley.transform, new Vector3(33.1f, 0f, -4.55f), new Color(0.025f, 0.025f, 0.03f), string.Empty);
        AddInteraction("Alley Meeting Trigger", new Vector3(31.9f, 0.8f, -3.8f), new Vector3(2.4f, 1.8f, 2.4f), "Approach", StartAttackSequence);

        Light alleyLight = CreateLight("Flickering Alley Light", alley.transform, new Vector3(30f, 3.05f, -3.8f), LightType.Point, warningColor, 4.4f, 8f);
        alleyLight.shadows = LightShadows.Soft;
        alleyLight.gameObject.AddComponent<FlickerLight>();
    }

    private void BuildBedroom(Transform root)
    {
        GameObject bedroom = new GameObject("Lacey Bedroom");
        bedroom.transform.SetParent(root);
        CreateRoom(bedroom.transform, new Vector3(0f, 1.35f, -18f), new Vector3(9f, 2.7f, 8f), new Color(0.22f, 0.2f, 0.19f), new Color(0.11f, 0.095f, 0.085f));
        CreateCube("Bed Base", bedroom.transform, new Vector3(-2.4f, 0.35f, -19f), new Vector3(2.1f, 0.7f, 3.3f), new Color(0.18f, 0.13f, 0.12f));
        CreateCube("Blanket", bedroom.transform, new Vector3(-2.4f, 0.82f, -19f), new Vector3(2f, 0.16f, 3f), new Color(0.35f, 0.32f, 0.28f));
        CreateCube("Pillow", bedroom.transform, new Vector3(-2.4f, 1f, -20.25f), new Vector3(1.6f, 0.22f, 0.6f), new Color(0.62f, 0.58f, 0.52f));
        CreateCube("Desk", bedroom.transform, new Vector3(2.2f, 0.45f, -15.25f), new Vector3(1.7f, 0.9f, 0.8f), deskColor);
        CreateCube("Old TV", bedroom.transform, new Vector3(3.4f, 0.8f, -18.2f), new Vector3(1.05f, 0.75f, 0.7f), new Color(0.04f, 0.04f, 0.045f));
        CreateCube("TV Glow", bedroom.transform, new Vector3(3.38f, 0.83f, -18.57f), new Vector3(0.82f, 0.46f, 0.03f), new Color(0.12f, 0.19f, 0.22f));
        CreateCube("Breakfast Table", bedroom.transform, new Vector3(0.5f, 0.45f, -15.45f), new Vector3(1.3f, 0.9f, 0.9f), new Color(0.26f, 0.18f, 0.12f));
        CreateCube("Breakfast Plate", bedroom.transform, new Vector3(0.5f, 0.94f, -15.45f), new Vector3(0.45f, 0.04f, 0.45f), new Color(0.72f, 0.68f, 0.58f));
        CreateCube("Bathroom Sink", bedroom.transform, new Vector3(-3.7f, 0.55f, -15.45f), new Vector3(0.9f, 1.1f, 0.55f), new Color(0.52f, 0.55f, 0.54f));
        CreateCube("Bathroom Marker", bedroom.transform, new Vector3(-3.7f, 0.02f, -16.45f), new Vector3(1.35f, 0.04f, 1.35f), new Color(0.12f, 0.16f, 0.16f));
        CreateCube("Living Room Couch", bedroom.transform, new Vector3(1.2f, 0.42f, -20.95f), new Vector3(2.4f, 0.85f, 1.05f), new Color(0.22f, 0.18f, 0.14f));
        CreateCube("Couch Cushion", bedroom.transform, new Vector3(1.2f, 0.9f, -20.95f), new Vector3(2.25f, 0.16f, 0.9f), new Color(0.34f, 0.29f, 0.23f));
        CreateLight("Bedroom Lamp", bedroom.transform, new Vector3(-0.4f, 2.25f, -17.6f), LightType.Point, new Color(0.95f, 0.66f, 0.42f), 2.9f, 7f);

        AddRoutineInteraction("Routine Bed Trigger", new Vector3(-2.4f, 0.8f, -19.1f), new Vector3(1.6f, 1.6f, 2.2f), "Wake", "Get up");
        AddRoutineInteraction("Routine Breakfast Trigger", new Vector3(0.5f, 0.8f, -15.45f), new Vector3(1.6f, 1.6f, 1.4f), "Breakfast", "Eat breakfast");
        AddRoutineInteraction("Routine Homework Trigger", new Vector3(2.2f, 0.8f, -15.25f), new Vector3(1.7f, 1.6f, 1.3f), "Homework", "Do school work");
        AddRoutineInteraction("Routine TV Trigger", new Vector3(3.1f, 0.8f, -18.2f), new Vector3(1.5f, 1.6f, 1.5f), "TV", "Watch TV");
        AddRoutineInteraction("Routine Bathroom Trigger", new Vector3(-3.7f, 0.8f, -16.05f), new Vector3(1.7f, 1.6f, 1.9f), "Bathroom", "Use bathroom");
        AddRoutineInteraction("Routine Sleep Trigger", new Vector3(-2.4f, 0.8f, -20.2f), new Vector3(1.8f, 1.6f, 1.2f), "Sleep", "Go back to bed");
        AddRoutineInteraction("Routine Couch Trigger", new Vector3(1.2f, 0.8f, -20.95f), new Vector3(2.4f, 1.6f, 1.4f), "Couch", "Rest on couch");
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
        Teleport(new Vector3(-2.4f, 0.05f, -19.1f), 0f);
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

    private void StartRoutineDay(int day)
    {
        currentDay = day;
        routineStep = 0;
        stage = Stage.HomeRoutine;
        sequenceRunning = false;
        locked = false;
        playerCamera.fieldOfView = Mathf.Lerp(52f, 47f, Mathf.Clamp01((day - 1) / 3f));
        volumeVignette.intensity.Override(day == 1 ? 0.32f : day == 2 ? 0.44f : 0.58f);
        chromaticAberration.intensity.Override(day == 1 ? 0.06f : day == 2 ? 0.11f : 0.18f);

        Teleport(new Vector3(-2.4f, 0.05f, -19.1f), 0f);
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
        Teleport(new Vector3(1.2f, 0.05f, -20.95f), 180f);
        playerCamera.fieldOfView = 43f;
        volumeVignette.intensity.Override(0.68f);
        yield return FadeTo(0f, 2.6f);
        yield return Say("Day 5. You wake up on the couch.", 2.5f);
        yield return Say("You try to stand. Nothing moves enough.", 3f);
        objectiveText.text = "Prototype end\nThe twelve couch-year scenes begin next.";
        locked = true;
        sequenceRunning = true;
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
