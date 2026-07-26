using UnityEngine;

namespace ULTRAKILLSplitScreen.Plugin;

internal sealed class HotkeySplitScreenMenu
{
    private readonly Func<HotkeyLaunchRequest, string?> _launch;
    private Rect _windowRect = new(0f, 0f, 620f, 560f);
    private bool _shown;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLock;
    private float _previousTimeScale = 1f;
    private int _playerChoice;
    private int _profileChoice;
    private int _monitorChoice;
    private bool _fillScreen;
    private string _status = string.Empty;

    private static readonly string[] PlayerOptions = ["2 joueurs", "3 joueurs", "4 joueurs"];
    private static readonly string[] ProfileOptions =
    [
        "Recherche automatique",
        "Manettes Xbox / XInput",
        "PlayStation PS4 / PS5",
        "Nintendo Switch Pro"
    ];

    public HotkeySplitScreenMenu(Func<HotkeyLaunchRequest, string?> launch)
    {
        _launch = launch;
    }

    public bool Shown => _shown;

    public void Toggle()
    {
        if (_shown)
            Close();
        else
            Open();
    }

    public void Draw()
    {
        if (!_shown)
            return;

        _windowRect.x = (Screen.width - _windowRect.width) / 2f;
        _windowRect.y = (Screen.height - _windowRect.height) / 2f;
        _windowRect = GUI.ModalWindow(938201, _windowRect, DrawWindow, "ULTRAKILL SPLIT-SCREEN — Ctrl+P");
    }

    private void Open()
    {
        _shown = true;
        _status = string.Empty;
        _previousCursorVisible = Cursor.visible;
        _previousCursorLock = Cursor.lockState;
        _previousTimeScale = Time.timeScale;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    private void Close()
    {
        _shown = false;
        Cursor.visible = _previousCursorVisible;
        Cursor.lockState = _previousCursorLock;
        Time.timeScale = _previousTimeScale;
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Space(8f);
        GUILayout.Label("Nombre total de fenêtres / joueurs");
        _playerChoice = GUILayout.SelectionGrid(_playerChoice, PlayerOptions, 3, GUILayout.Height(42f));

        GUILayout.Space(12f);
        GUILayout.Label("Type de manette à rechercher pour le mapping");
        _profileChoice = GUILayout.SelectionGrid(_profileChoice, ProfileOptions, 2, GUILayout.Height(76f));

        GUILayout.Space(8f);
        GUILayout.Label("Manettes actuellement détectées");
        GUILayout.TextArea(GamepadIsolation.DescribeAvailable(), GUILayout.Height(54f));

        GUILayout.Space(10f);
        GUILayout.Label("Écran utilisé pour le split-screen");
        string[] monitorOptions = Display.displays.Length > 1
            ? ["Écran principal", "Deuxième écran"]
            : ["Écran principal", "Deuxième écran (non détecté)"];
        _monitorChoice = GUILayout.SelectionGrid(_monitorChoice, monitorOptions, 2, GUILayout.Height(42f));

        _fillScreen = GUILayout.Toggle(
            _fillScreen,
            "Remplir entièrement l’écran cible (sinon conserver chaque fenêtre en 16:9)");

        GUILayout.Space(10f);
        GUILayout.Label(
            "Le joueur 1 reste dans la partie actuelle. Les autres instances sont lancées et disposées automatiquement. " +
            "Jaket tentera de créer puis rejoindre le même lobby.");

        if (!string.IsNullOrWhiteSpace(_status))
        {
            GUILayout.Space(8f);
            GUILayout.TextArea(_status, GUILayout.Height(42f));
        }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Annuler", GUILayout.Height(38f)))
            Close();

        if (GUILayout.Button("Activer le split-screen", GUILayout.Height(38f)))
        {
            var request = new HotkeyLaunchRequest(
                _playerChoice + 2,
                ProfileValue(_profileChoice),
                _monitorChoice,
                _fillScreen);

            string? error = _launch(request);
            if (string.IsNullOrWhiteSpace(error))
                Close();
            else
                _status = error;
        }
        GUILayout.EndHorizontal();
    }

    private static string ProfileValue(int choice)
    {
        return choice switch
        {
            1 => "xbox",
            2 => "playstation",
            3 => "switch",
            _ => "auto"
        };
    }
}

internal readonly struct HotkeyLaunchRequest
{
    public HotkeyLaunchRequest(int totalPlayers, string controllerProfile, int targetMonitor, bool fillScreen)
    {
        TotalPlayers = totalPlayers;
        ControllerProfile = controllerProfile;
        TargetMonitor = targetMonitor;
        FillScreen = fillScreen;
    }

    public int TotalPlayers { get; }
    public string ControllerProfile { get; }
    public int TargetMonitor { get; }
    public bool FillScreen { get; }
}