using Newtonsoft.Json;
using KTMissionGetter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using System.Text.RegularExpressions;

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

    public KMModSettings _settings;

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
        _settings = GetComponent<KMModSettings>();

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

        if (_maze.GetStack() == null)
            return;

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

        ModSettings settings = GetMissionSettings();
        if (settings == null)
            settings = GetSettings();
        else
            Log("Mission description has been recognised and will be used for this instance.");

        yield return new WaitWhile(() => _isUsingThreads);
        _isUsingThreads = true;

        Log("The settings are: {0} goal layers, traversal score of {1}-{2} and porosity {3}.", settings.TargetLayerCount, settings.LowerScoreBound, settings.UpperScoreBound, settings.Porosity);

        _thread = new Thread(() =>
        {
            try
            {
                _maze = SpectreMazeTile.Generate(settings.TargetLayerCount + 1, settings.LowerScoreBound, settings.UpperScoreBound, settings.Porosity, rng);
            }
            catch
            {
                Log("An exception has been thrown. Please contact the developer.");
            }
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

            if (_maze.GetStack() == null)
            {
                moduleAudio.PlaySoundAtTransform("Solve", transform);
                Log("The module has been solved!");
                StartCoroutine(Solve());
                return false;
            }

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

                bool strike = _maze.GetStack() == null;
                SpectreMazeTile.TraversalData traversal = new SpectreMazeTile.TraversalData(null, null, x, -1, -1, null);

                if (!strike)
                {
                    traversal = _maze.Move(x);

                    strike = traversal.TravelPermission == null || !(bool)traversal.TravelPermission;
                }

                float angleRad = (15 - SpectreButton.Orientations[x]) / 180f * Mathf.PI;
                float windStrength = 0.1f / 60;

                _particles.ApplyWind(-windStrength * new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)), strike);

                if (strike)
                {
                    moduleAudio.PlaySoundAtTransform("MinThird" + new string[] { "C", "Csharp", "D", "Dsharp", "E", "F", "Fsharp", "G", "Gsharp", "A" }.PickRandom(), transform);
                    if (traversal.FromTile == null)
                        Log("A strike was dealt for attempting to traverse while being nowhere.");
                    else if (traversal.ToTile == null)
                        Log("A strike was dealt for hitting a wall while attempting to traverse through edge {0}, as there is nothing there? The current position is {1}.", traversal.EntryEdge, SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"));
                    else if (traversal.TravelPermission == null)
                        Log("A strike was dealt for entering the unknown while attempting to traverse through edge {0}. Try expanding your range. The current position is {1}.", traversal.EntryEdge, SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"));
                    else
                        Log("A strike was dealt for hitting a wall while attempting to traverse through edge {0}, as it does not pair with edge {1} of {2}. The current position is {3}.", traversal.EntryEdge, traversal.ExitEdge, SpectreMazeTile.ParseStack(traversal.ToTile).Join("-"), SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"));
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

        if (_maze.GetGoal() == null)
        {
            Log("The module could not generate any valid configuration. Pressing submit will solve the module.", SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"), SpectreMazeTile.ParseStack(_maze.GetGoal()).Join("-"));
        }
        else
        {
            Log("Spawning on {0}. The goal is any tile with the coordinate {1}.", SpectreMazeTile.ParseStack(_maze.GetStack()).Join("-"), SpectreMazeTile.ParseStack(_maze.GetGoal()).Join("-"));
            Log("The module currently displays {0}.", _maze.GetMemoryText().SkipWhile(x => x == "?").Join("-"));

            Log("The edge pairs that can be traversed through in this maze are: {0}.", _maze.GetAllEdgePairs().Join(", "));
        }

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
                yield return SetSelection(new bool[14]);
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

            foreach (int index in _solverPath)
            {
                while (_maze.Traverse(index).Layer >= _maze.GetFamiliarDepth())
                    yield return SetSelection(Enumerable.Range(0, 14).Select(x => _maze.GetUnknownEdges().Contains(x)).ToArray());

                yield return SetSelection(new bool[14]);

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

    private IEnumerator SetSelection(bool[] selection)
    {
        for (int i = 0; i < 14; i++)
            if (!_selected[i] && selection[i])
            {
                _state = ModuleState.Ready;
                _buttons[i].Selectable.OnHighlight();
                _buttons[i].Selectable.OnInteract();

                _state = ModuleState.Autosolving;
                yield return new WaitForSeconds(0.1f);
                yield return new WaitUntil(() => _selected.Any(x => x));

                _state = ModuleState.Ready;
                _buttons[i].Selectable.OnInteractEnded();
                _buttons[i].Selectable.OnHighlightEnded();

                _state = ModuleState.Autosolving;
                yield return new WaitForSeconds(0.1f);
            }

        for (int i = 0; i < 14; i++)
            if (_selected[i] && !selection[i])
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

        if (_selected.Any(x => x))
        {
            _state = ModuleState.Ready;
            GetComponent<KMSelectable>().Children[0].OnInteract();
            _state = ModuleState.Autosolving;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void Log(string format, params object[] args)
    {
        Debug.LogFormat("[{0} #{1}] {2}", _moduleName, _moduleId, string.Format(format, args));
    }



    public class ModSettings
    {
        public int TargetLayerCount = 3;
        public int LowerScoreBound = 64;
        public int UpperScoreBound = 80;
        public int Porosity = 1;
    }

    public ModSettings GetSettings()
    {
        try
        {
            bool changed = false;
            ModSettings settings = JsonConvert.DeserializeObject<ModSettings>(_settings.Settings);
            if (settings.TargetLayerCount < 1)
            {
                settings.TargetLayerCount = 1;
                changed = true;
            }

            if (settings.LowerScoreBound < 0)
            {
                settings.LowerScoreBound = 0;
                changed = true;
            }

            if (settings.UpperScoreBound <= settings.LowerScoreBound)
            {
                settings.UpperScoreBound = settings.LowerScoreBound + 1;
                changed = true;
            }

            if (settings.Porosity < 0)
            {
                settings.Porosity = 0;
                changed = true;
            }
            if (settings.Porosity > 10)
            {
                settings.Porosity = 10;
                changed = true;
            }

            if (changed)
            {
                File.WriteAllText(_settings.SettingsPath, JsonConvert.SerializeObject(settings));
            }

            return settings;
        }
        catch
        {
            ModSettings settings = new ModSettings();
            File.WriteAllText(_settings.SettingsPath, JsonConvert.SerializeObject(settings));
            return settings;
        }
    }



    public ModSettings GetMissionSettings()
    {
        string missionDesc = Mission.Description;
        if (missionDesc == null)
            return null;

        Regex regex = new Regex(@"\[Spectre Maze\]:\d+;\d+-\d+;\d+");
        var match = regex.Match(missionDesc);
        if (!match.Success)
            return null;

        string[] options = match.Value.Replace("[Spectre Maze]:", "").Split(';');

        ModSettings setting = new ModSettings();

        int a;
        if (!int.TryParse(options[0], out a))
            return null;
        if (a < 1)
            return null;

        setting.TargetLayerCount = a;

        int b;
        if (!int.TryParse(options[1].Split('-')[0], out a))
            return null;
        if (!int.TryParse(options[1].Split('-')[1], out b))
            return null;
        if (a < 0 || b <= a)
            return null;

        setting.LowerScoreBound = a;
        setting.UpperScoreBound = b;

        if (!int.TryParse(options[2], out a))
            return null;
        if (a < 0 || a > 10)
            return null;

        setting.Porosity = a;

        return setting;
    }
}
