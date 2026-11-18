#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Developer panel for testing effects without having to play for them. F8 shows / hides it.
/// It only calls the juice components directly: it never raises game events or changes the game, so the
/// score, Pot and tower counts stay exactly as they were.
///
///   Hand slams    the Play-hand slam then the score slam, for every poker hand (and Royal Flush)
///   Any drama     the same slams at any drama from the slider
///   Shakes        camera only, HUD and hand only, everything
///   Pot sign      the broken-cards jolt, the collapse shatter
///
/// Added by itself to any scene that has a HandPlaqueJuice; editor and development builds only.
public class JuiceDebugPanel : MonoBehaviour
{
    const float Width = 290f;

    HandPlaqueJuice plaque;
    HandJuice handJuice;
    DeckJuice deckJuice;
    BankJuice bankJuice;
    GlassJuice glassJuice;
    BlindResultJuice blindResult;
    BlindMenuJuice blindMenu;
    RunEndJuice runEnd;
    RunIntroJuice runIntro;
    CameraShaker cameraShaker;
    PotSignJuice potSign;

    bool visible = true;
    float drama = 0.5f;
    string scoreText = "1234";
    Vector2 scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddToFirstScene()
    {
        SceneManager.sceneLoaded += (scene, mode) => AddIfJuiceScene();
        AddIfJuiceScene();
    }

    static void AddIfJuiceScene()
    {
        if (FindAnyObjectByType<JuiceDebugPanel>() != null) return;
        if (FindAnyObjectByType<HandPlaqueJuice>() == null) return;
        new GameObject("JuiceDebugPanel").AddComponent<JuiceDebugPanel>();
    }

    void Start()
    {
        plaque = FindAnyObjectByType<HandPlaqueJuice>();
        cameraShaker = FindAnyObjectByType<CameraShaker>();
        handJuice = FindAnyObjectByType<HandJuice>();
        deckJuice = FindAnyObjectByType<DeckJuice>();
        bankJuice = FindAnyObjectByType<BankJuice>();
        glassJuice = FindAnyObjectByType<GlassJuice>();
        blindResult = FindAnyObjectByType<BlindResultJuice>();
        blindMenu = FindAnyObjectByType<BlindMenuJuice>();
        runEnd = FindAnyObjectByType<RunEndJuice>();
        runIntro = FindAnyObjectByType<RunIntroJuice>();
        potSign = FindAnyObjectByType<PotSignJuice>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) visible = !visible;
    }

    // ---------- Panel ----------

    void OnGUI()
    {
        if (!visible) return;

        Rect area = new Rect(Screen.width - Width - 10f, 40f, Width, Screen.height - 80f);
        GUILayout.BeginArea(area, GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("<b>Juice debug</b>   (F8 hide)", RichLabel());
        DrawHandSlams();
        DrawAnyDrama();
        DrawShakes();
        DrawPotSign();
        DrawGlass();
        DrawBlindScreens();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawHandSlams()
    {
        Section("Hand slams (name, then score)");
        if (plaque == null) { GUILayout.Label("No HandPlaqueJuice in the scene"); return; }

        long score = ReadScore();
        foreach (PokerHand hand in System.Enum.GetValues(typeof(PokerHand)))
            HandButton(PokerHandEvaluator.GetName(hand), hand, false, score);
        HandButton("Royal Flush", PokerHand.StraightFlush, true, score);
    }

    void HandButton(string label, PokerHand hand, bool royal, long score)
    {
        float handDrama = plaque.DebugDramaOf(hand, royal);
        if (GUILayout.Button($"{label}   (drama {handDrama:0.00})"))
            plaque.DebugSlamHand(hand, royal, score);
    }

    void DrawAnyDrama()
    {
        Section("Any drama");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Drama {drama:0.00}", GUILayout.Width(80f));
        drama = GUILayout.HorizontalSlider(drama, 0f, 1f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Score", GUILayout.Width(80f));
        scoreText = GUILayout.TextField(scoreText);
        GUILayout.EndHorizontal();

        if (plaque != null && GUILayout.Button("Slam at this drama")) plaque.DebugSlamAtDrama("Test Slam", drama, ReadScore());
        if (plaque != null && GUILayout.Button("Score pops and flies to the Pot")) plaque.DebugScoreToPot(ReadScore());
        if (bankJuice != null && GUILayout.Button("Bank flights: Pot to Safe to Run Score")) bankJuice.DebugFlights(ReadScore());
    }

    void DrawShakes()
    {
        Section("Shakes (at the drama slider's strength)");
        if (cameraShaker != null && GUILayout.Button("One card breaking (camera bump)")) cameraShaker.AddShake(cameraShaker.energyPerBrokenCard);
        if (GUILayout.Button("Camera only") && cameraShaker != null) cameraShaker.AddShake(drama);
        if (GUILayout.Button("HUD and hand only")) ShakeOffset.ShakeAll(drama);
        if (GUILayout.Button("HUD hop and rock")) ShakeOffset.JoltAll(drama);
        if (handJuice != null && GUILayout.Button("Hand cards jump and rock")) handJuice.PlaySlamJump(drama);
        if (deckJuice != null && GUILayout.Button("Deck slam (hop, rock, red flash)")) deckJuice.PlaySlam(drama);
        if (GUILayout.Button("Everything"))
        {
            if (cameraShaker != null) cameraShaker.AddShake(drama);
            ShakeOffset.ShakeAll(drama);
        }
    }

    void DrawPotSign()
    {
        Section("Pot sign");
        if (potSign == null) { GUILayout.Label("No PotSignJuice in the scene"); return; }
        if (GUILayout.Button("Cards broke (jolt + red flash)")) potSign.PlayLoss();
        if (GUILayout.Button("Collapse (shatter, bulbs pop off)")) potSign.PlayShatter();
        GUILayout.Label("Bulbs come back when the next hand is drawn");
    }

    void DrawGlass()
    {
        Section("Glass (look only; F4 cracks it for real)");
        if (glassJuice == null) { GUILayout.Label("No GlassJuice in the scene"); return; }
        if (GUILayout.Button("Crack burst on the right crystal")) glassJuice.PlayCrack(glassJuice.stars.Count - 1);
        if (GUILayout.Button("Heal: wobble and break apart (right crystal)")) glassJuice.PlayRepair(glassJuice.stars.Count - 1);
    }

    // The slams only: the panels themselves open only for a real result
    void DrawBlindScreens()
    {
        Section("Blind screens (slams only)");
        if (blindResult != null && GUILayout.Button("WON! at the slider's drama")) blindResult.Play(true, drama);
        if (blindResult != null && GUILayout.Button("FAILED")) blindResult.Play(false, blindResult.failedDrama);
        if (blindMenu != null && GUILayout.Button("ANTE 3")) blindMenu.PlayAnte(3);
        if (runEnd != null && GUILayout.Button("RUN CLEARED (with confetti)")) runEnd.Play(true);
        if (runEnd != null && GUILayout.Button("GLASS SHATTERED (crystals burst)")) runEnd.Play(false);
        if (runIntro != null && GUILayout.Button("Run intro: cards fly in and form the deck")) runIntro.Play();
    }

    // ---------- Helpers ----------

    long ReadScore()
    {
        return long.TryParse(scoreText, out long score) ? score : 0;
    }

    static void Section(string title)
    {
        GUILayout.Space(8f);
        GUILayout.Label($"<b>{title}</b>", RichLabel());
    }

    static GUIStyle RichLabel()
    {
        return new GUIStyle(GUI.skin.label) { richText = true };
    }
}
#endif
