using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

public class SpectreMazeScript : MonoBehaviour
{
    private enum ModuleState
    {
        Preparing,
        Ready,
        Autosolving,
        Solved
    }

    private static float _minHoldTime = 0.5f;

    private SpectreParticleManager _particles;
    private List<SpectreTextManager> _texts;
    private float _holdTimer = 0;
    private int _holding = -1;
    private int _highlighting = -1;
    private bool[] _selected;

    private List<SpectreButton> _buttons = new List<SpectreButton>();
    private SpectreText _textRef;

    private ModuleState _state;
    private static bool _isUsingThreads = false;
    private Thread _thread = null;
    private bool _solved = false;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private readonly string _moduleName = "Spectre Maze";

    private SpectreMazeTile _maze;

    void Awake()
    {
        _moduleId = _moduleIdCounter++;
    }

    private float _t;

    void Start()
    {
        _maze = null;

        _state = ModuleState.Preparing;
        SpectreTextManager.InitialiseColours();

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        System.Random random = new System.Random(seed);
        Log("The seed is: {0}.", seed.ToString());
        StartCoroutine(Generate(random));

        _particles = new SpectreParticleManager();
        SpectreParticle particle = GetComponentInChildren<SpectreParticle>();
        _particles.AddParticle(particle);
        for (int i = 0; i < 32 - 1; i++)
            _particles.AddParticle(Instantiate(particle, particle.transform.parent));

        _buttons = GetComponentInChildren<SpectreButton>().BuildClones();

        foreach (SpectreButton button in _buttons)
        {
            button.Speed = 0.5f;
            button.Extended = 0.5f;
        }
    }

    void Update()
    {
        _t += Time.deltaTime;

        if (_state != ModuleState.Ready && _state != ModuleState.Autosolving)
            return;

        foreach (SpectreTextManager text in _texts)
        {
            text.Update();
        }

        if (_holding != -1 && _holdTimer < 1)
        {
            _holdTimer += Time.deltaTime / _minHoldTime;
            if (_holdTimer >= 1)
            {
                GetComponent<KMAudio>().PlaySoundAtTransform("Fourth" + new string[] { "C", "Csharp", "D", "Dsharp", "E", "F" }.PickRandom(), transform);
                _selected[_holding] = true;
                _holding = -1;
            }
        }
    }

    void FixedUpdate()
    {
        _particles.UpdateParticles();
    }

    void OnDestroy()
    {
        if (_thread != null)
        {
            _thread.Interrupt();
            _isUsingThreads = false;
        }
    }

    private IEnumerator Generate(System.Random rng)
    {
        yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.975f, 2f));

        yield return new WaitWhile(() => _isUsingThreads);
        _isUsingThreads = true;

        _thread = new Thread(() =>
        {
            _maze = SpectreMazeTile.Generate(4, 0.5f, rng);
        });
        _thread.Start();

        yield return new WaitWhile(() => _maze == null);
        _isUsingThreads = false;
        _thread = null;

        _maze.Log = false;

        KMSelectable moduleSelectable = GetComponent<KMSelectable>();
        KMAudio moduleAudio = GetComponent<KMAudio>();

        moduleSelectable.Children[0].OnInteract += () =>
        {
            if (_state != ModuleState.Ready)
                return false;

            bool strike = false;
            if (_selected.Any(y => y))
            {
                strike = !_maze.TryExplore(_selected);

                if (strike)
                {
                    IEnumerable<int> unknownEdges = _maze.GetUnknownEdges();
                    if (unknownEdges.Any())
                        Log("A strike was dealt for incorrectly inputting unknown borders. The entered edges were [{0}], where the correct answer would have been [{1}].", Enumerable.Range(0, 14).Where(x => _selected[x]).Join(", "), unknownEdges.Join(", "));
                    else
                        Log("A strike was dealt for incorrectly inputting unknown borders. All surrounding edges are part of tiles that should already be known.");
                }
                else
                {
                    Log("A new layer of information is now given to the player.");
                    Log("The module currently displays {0}.", _maze.GetMemoryText().SkipWhile(x => x == "?").Join("-"));

                    moduleAudio.PlaySoundAtTransform("MajThird" + new string[] { "C", "Csharp", "D", "Dsharp", "E", "F", "Fsharp", "G", "Gsharp" }.PickRandom(), transform);
                }

                _selected = new bool[_selected.Length];
                for (int i = 0; i < _selected.Length; i++)
                    _buttons[i].Extended = 1;

                _holdTimer = 0;
            }
            else if (SpectreMazeTile.IsOfType(_maze.GetStack(), _maze.GetGoal()))
            {
                moduleAudio.PlaySoundAtTransform("Solve", transform);
                Log("The module has been solved!");
                StartCoroutine(Solve());
            }
            else
            {
                Log("A strike was dealt for incorrectly submitting the goal location. The current position is [{0}], where the correct answer would have been [{1}].", SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"), SpectreMazeTile.ParseStack(_maze.GetGoal()).Join("-"));

                strike = true;
            }

            if (strike)
            {
                moduleAudio.PlaySoundAtTransform("MinThird" + new string[] { "C", "Csharp", "D", "Dsharp", "E", "F", "Fsharp", "G", "Gsharp", "A" }.PickRandom(), transform);

                _maze.HandleFail();
                Log("The module currently displays {0}.", _maze.GetMemoryText().SkipWhile(x => x == "?").Join("-"));
                GetComponent<KMBombModule>().HandleStrike();
            }

            return false;
        };

        _selected = new bool[_buttons.Count];
        for (int i = 0; i < _buttons.Count; i++)
        {
            int x = i;

            _buttons[i].Selectable.OnHighlight += () =>
            {
                if (_state != ModuleState.Ready)
                    return;

                _highlighting = x;

                if (_selected.Any(y => y) || _holding != -1 || _maze.CurrentMemory == null || !_maze.CurrentMemory.ShowAllData)
                    return;

                for (int j = 0; j < 14; j++)
                    _buttons[j].Extended = 0.5f;
                foreach (int edge in _maze.GetConnected(x))
                    _buttons[edge].Extended = 1;
                _buttons[x].Extended = 1;
            };

            _buttons[i].Selectable.OnHighlightEnded += () =>
            {
                if (_state != ModuleState.Ready)
                    return;

                _highlighting = -1;

                if (_selected.Any(y => y) || _holding != -1 || _maze.CurrentMemory == null || !_maze.CurrentMemory.ShowAllData)
                    return;

                for (int j = 0; j < 14; j++)
                    _buttons[j].Extended = 1;
            };

            _buttons[i].Selectable.OnInteract += () =>
            {
                if (_state != ModuleState.Ready)
                    return false;

                for (int j = 0; j < 14; j++)
                    _buttons[j].Extended = _selected[j] ? 0.75f : 1;

                _buttons[x].Extended = 0.5f;
                if (_selected.Any(y => y))
                    _selected[x] = !_selected[x];
                else
                {
                    _holding = x;
                    _holdTimer = 0;
                }


                return false;
            };

            _buttons[i].Selectable.OnInteractEnded += () =>
            {
                if (_state != ModuleState.Ready)
                    return;

                for (int j = 0; j < 14; j++)
                    _buttons[j].Extended = _selected[j] ? 0.75f : 1;

                _holding = -1;
                if (_holdTimer >= 1 && !_selected.Any(y => y))
                {
                    _buttons[x].Extended = 1;
                    _holdTimer = 0;
                    return;
                }

                if (_holdTimer >= 1)
                    return;

                bool strike = !_maze.Move(x);

                float angleRad = (15 - SpectreButton.Orientations[x]) / 180f * Mathf.PI;
                float windStrength = 0.1f / 60;

                _particles.ApplyWind(-windStrength * new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)), strike);

                if (strike)
                {
                    moduleAudio.PlaySoundAtTransform("MinThird" + new string[] { "C", "Csharp", "D", "Dsharp", "E", "F", "Fsharp", "G", "Gsharp", "A" }.PickRandom(), transform);
                    Log("A strike was dealt for hitting a wall while attempting to traverse through edge {0}. The current position is {1}.", x, SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"));
                    _maze.HandleFail();
                    Log("The module currently displays {0}.", _maze.GetMemoryText().SkipWhile(y => y == "?").Join("-"));
                    StartCoroutine(DelayStrike(0.25f));
                }
                else
                {
                    moduleAudio.PlaySoundAtTransform("Fifth" + new string[] { "C", "Csharp", "D", "Dsharp", "E", "F" }.PickRandom(), transform);
                    Log("Successfully traversed through edge {0}. The current position is {1}.", x, SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"));
                }

                if (_highlighting != -1)
                {
                    int hl = _highlighting;
                    _buttons[hl].Selectable.OnHighlightEnded();
                    _buttons[hl].Selectable.OnHighlight();
                }
            };
        }

        moduleSelectable.Children = moduleSelectable.Children.Concat(_buttons.Select(x => x.Selectable)).ToArray();
        moduleSelectable.UpdateChildren();

        _texts = new List<SpectreTextManager>();
        _textRef = GetComponentInChildren<SpectreText>();
        _texts.Add(new SpectreTextCycle(Instantiate(_textRef, _textRef.transform.parent), _maze, new Vector3(-0.025f, 0.0351f, 0.05f), new Vector3(-0.025f, 0.0351f, -0.05f), 0.005f));
        _texts.Add(new SpectreTextSingle(Instantiate(_textRef, _textRef.transform.parent), _maze, new Vector3(0.045f, 0.0351f, -0.05f), 0.01f));

        Log("Spawning on {0}. The goal is any tile with the coordinate {1}.", SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"), SpectreMazeTile.ParseStack(_maze.GetGoal()).Join("-"));
        Log("The module currently displays {0}.", _maze.GetMemoryText().SkipWhile(x => x == "?").Join("-"));

        Log("The edge pairs that can be traversed through in this maze are: {0}.", _maze.GetAllEdgePairs().Join(", "));

        _buttons.ForEach(x => x.Extended = 1);

        yield return new WaitForSeconds(1f);

        _buttons.ForEach(x => x.Speed = 4f);
        _state = ModuleState.Ready;
    }

    private IEnumerator Solve()
    {
        _state = ModuleState.Solved;

        foreach (SpectreTextManager text in _texts)
            text.Terminate();

        foreach (SpectreButton button in _buttons)
            button.Extended = 1;

        _particles.Terminate();

        if (GetComponent<KMBombInfo>().GetTime() < 60)
        {
            GetComponent<KMBombModule>().HandlePass();
            _solved = true;
        }

        int buttonParities = UnityEngine.Random.Range(0, 2);

        for (int j = 0; j < 2; j++)
        {
            int buttonParity = buttonParities ^ j;

            for (int i = buttonParity; i < _buttons.Count; i += 2)
            {
                _buttons[i].Speed = 0.25f;
                _buttons[i].Extended = 0;
            }
            yield return new WaitForSeconds(4f);
        }

        if (!_solved)
            GetComponent<KMBombModule>().HandlePass();
        _solved = true;

        MeshRenderer pieceRenderer = GetComponent<KMSelectable>().Children[0].GetComponent<MeshRenderer>();

        for (float t = 0; t < 1; t += Time.deltaTime / 8f)
        {
            pieceRenderer.material.color = Color.Lerp(new Color32(0x22, 0x22, 0x22, 0xff), new Color32(0x44, 0x44, 0x44, 0xff), t);
            yield return null;
        }
    }

    private IEnumerator DelayStrike(float time)
    {
        yield return new WaitForSeconds(time);
        GetComponent<KMBombModule>().HandleStrike();
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "'!{0} highlight 0 13' to hover over those edges. '!{0} press 0 13' to press those edges. '!{0} select 0 13' to (de)select those edges. '!{0} submit' to press the piece.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        yield return null;
        if (_state == ModuleState.Preparing)
        {
            yield return "sendtochaterror The module is not ready yet.";
            yield break;
        }
        if (_state == ModuleState.Solved)
        {
            yield return "sendtochaterror The module is solved.";
            yield break;
        }

        command = command.ToLowerInvariant();
        string[] commands = command.Split(' ');
        if (commands.Length >= 1)
        {
            switch (commands[0])
            {
                case "highlight":
                case "press":
                case "select":
                    if (commands[0] == "highlight" && (_maze.CurrentMemory == null || !_maze.CurrentMemory.ShowAllData || _selected.Any(x => x)))
                    {
                        yield return "sendtochaterror Highlighting doesn't have any use in the module's current state.";
                        yield break;
                    }

                    if (commands[0] == "press" && _selected.Any(x => x))
                    {
                        yield return "sendtochaterror Please use the select command to press edges when in selection mode.";
                        yield break;
                    }

                    List<int> indices = new List<int>();
                    foreach (string item in commands.Skip(1))
                    {
                        int index = -1;
                        if (int.TryParse(item, out index))
                            indices.Add(index);
                        else
                        {
                            indices.Add(-1);
                            break;
                        }
                    }

                    if (indices.Count == 0 || indices.Any(x => x < 0 || x >= 14))
                        break;

                    foreach (int index in indices)
                    {
                        switch (commands[0])
                        {
                            case "highlight":
                                _buttons[index].Selectable.OnHighlight();

                                yield return new WaitForSeconds(0.875f);

                                _buttons[index].Selectable.OnHighlightEnded();

                                yield return new WaitForSeconds(0.125f);
                                break;
                            case "press":
                                _buttons[index].Selectable.OnHighlight();
                                _buttons[index].Selectable.OnInteract();

                                yield return new WaitForSeconds(0.125f);

                                _buttons[index].Selectable.OnInteractEnded();
                                _buttons[index].Selectable.OnHighlightEnded();

                                yield return new WaitForSeconds(0.375f);
                                break;
                            case "select":
                                bool selectionMode = _selected.Any(x => x);

                                _buttons[index].Selectable.OnHighlight();
                                _buttons[index].Selectable.OnInteract();

                                if (selectionMode)
                                    yield return new WaitForSeconds(0.125f);
                                else
                                    yield return new WaitForSeconds(0.625f);

                                _buttons[index].Selectable.OnInteractEnded();
                                _buttons[index].Selectable.OnHighlightEnded();

                                yield return new WaitForSeconds(0.375f);
                                break;
                        }

                    }

                    yield break;

                case "submit":
                    if (commands.Length > 1)
                        break;
                    GetComponent<KMSelectable>().Children[0].OnInteract();
                    yield break;
            }
        }

        yield return "sendtochaterror Invalid command.";
        yield break;
    }

    private List<int> _solverPath = null;
    IEnumerator TwitchHandleForcedSolve()
    {
        while (_state == ModuleState.Preparing)
            yield return true;
        while (_state != ModuleState.Solved)
        {
            if (SpectreMazeTile.IsOfType(_maze.GetStack(), _maze.GetGoal()))
            {
                _state = ModuleState.Ready;
                GetComponent<KMSelectable>().Children[0].OnInteract();
                break;
            }

            _state = ModuleState.Autosolving;
            _solverPath = null;

            //because the solver coroutine doesn't run in the background during solves
            StartCoroutine(SolverThreadStealer());

            while (_solverPath == null)
                yield return true;


            for (int i = 0; i < 14; i++)
                if (_selected[i])
                {
                    _state = ModuleState.Ready;
                    _buttons[i].Selectable.OnHighlight();
                    _buttons[i].Selectable.OnInteract();

                    _state = ModuleState.Autosolving;
                    yield return new WaitForSeconds(0.1f);

                    _state = ModuleState.Ready;
                    _buttons[i].Selectable.OnInteractEnded();
                    _buttons[i].Selectable.OnHighlightEnded();

                    _state = ModuleState.Autosolving;
                    yield return new WaitForSeconds(0.1f);
                }

            foreach (int index in _solverPath)
            {
                _state = ModuleState.Ready;
                _buttons[index].Selectable.OnHighlight();
                _buttons[index].Selectable.OnInteract();

                _state = ModuleState.Autosolving;
                yield return new WaitForSeconds(0.1f);

                _holdTimer = 0;
                _holding = -1;

                _state = ModuleState.Ready;
                _buttons[index].Selectable.OnInteractEnded();
                _buttons[index].Selectable.OnHighlightEnded();

                _state = ModuleState.Autosolving;
                yield return new WaitForSeconds(0.1f);
            }
        }
        while (!_solved)
            yield return true;
    }

    private IEnumerator SolverThreadStealer()
    {
        yield return new WaitWhile(() => _isUsingThreads);
        _isUsingThreads = true;

        _thread = new Thread(() =>
        {
            _solverPath = _maze.GetSolvePath();
            if (_solverPath == null)
                Log("The autosolver failed. Please contact the developer about this.");
        });
        _thread.Start();

        yield return new WaitWhile(() => _solverPath == null);

        _isUsingThreads = false;
        _thread = null;
    }

    private void Log(string format, params object[] args)
    {
        Debug.LogFormat("[{0} #{1}] {2}", _moduleName, _moduleId, string.Format(format, args));
    }
}
