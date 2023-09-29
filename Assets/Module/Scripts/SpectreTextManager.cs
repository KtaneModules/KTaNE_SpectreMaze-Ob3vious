using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class SpectreTextManager
{
    public SpectreMazeTile Tile { get; private set; }

    private List<SpectreText> _texts;
    protected float Timer = 0;
    protected List<string> MemoryText;

    private static Dictionary<char, Color> _colourRefs = new Dictionary<char, Color>();

    public static void InitialiseColours()
    {
        _colourRefs.Clear();
        _colourRefs.Add('Γ', new Color32(0xff, 0xc0, 0xc0, 0x00));
        _colourRefs.Add('Δ', new Color32(0xc0, 0xff, 0xff, 0x00));
        _colourRefs.Add('Θ', new Color32(0xff, 0xc0, 0xff, 0x00));
        _colourRefs.Add('Λ', new Color32(0xc0, 0xc0, 0xc0, 0x00));
        _colourRefs.Add('Ξ', new Color32(0xe0, 0xc0, 0xff, 0x00));
        _colourRefs.Add('Π', new Color32(0xc0, 0xc0, 0xff, 0x00));
        _colourRefs.Add('Σ', new Color32(0xc0, 0xff, 0xc0, 0x00));
        _colourRefs.Add('Φ', new Color32(0xff, 0xe0, 0xc0, 0x00));
        _colourRefs.Add('Ψ', new Color32(0xff, 0xff, 0xc0, 0x00));
        _colourRefs.Add('?', new Color32(0xe0, 0xe0, 0xe0, 0x00));
    }

    public SpectreTextManager(SpectreText refText, SpectreMazeTile tile)
    {
        Tile = tile;
        _texts = new List<SpectreText> { refText };
    }

    public void Update()
    {
        Timer += Time.deltaTime;
        UpdateText();
    }

    public void Terminate()
    {
        _texts.ForEach(x => x.Opacity = 0);
    }

    protected void AssignText(string text, Vector3 position, float speed)
    {
        if (_texts.All(x => !x.IsAvailable()))
            _texts.Add(Object.Instantiate(_texts.First(), _texts.First().transform.parent));

        SpectreText selectedText = _texts.First(x => x.IsAvailable());

        selectedText.SetText(text, _colourRefs[text[0]]);
        selectedText.transform.localPosition = position;
        float angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
        selectedText.Speed = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * speed;
        selectedText.Opacity = 0.375f;
    }

    protected abstract void UpdateText();
}

public class SpectreTextCycle : SpectreTextManager
{
    private int _lastIteration = -1;

    private Vector3 _minPos;
    private Vector3 _maxPos;
    private float _distortion;

    public SpectreTextCycle(SpectreText refText, SpectreMazeTile tile, Vector3 minPos, Vector3 maxPos, float distortion) : base(refText, tile)
    {
        _minPos = minPos;
        _maxPos = maxPos;
        _distortion = distortion;
    }

    protected override void UpdateText()
    {
        float timeSpan = 1.5f;
        Timer %= timeSpan;

        //these are moments where I wish this version of c# had null-propagation
        List<string> memoryText = Tile.GetMemoryText();
        if (memoryText != null)
            memoryText = memoryText.SkipWhile(x => x == "?").ToList();

        if (memoryText == null || MemoryText == null || !MemoryText.SequenceEqual(memoryText))
        {
            MemoryText = memoryText;
            Timer = 0;
            _lastIteration = -1;
        }

        if (MemoryText == null)
            return;

        int currentIteration = (int)(MemoryText.Count * Timer / timeSpan);
        if (_lastIteration != currentIteration)
        {
            _lastIteration = currentIteration;
            float angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
            AssignText(
                MemoryText[currentIteration],
                Vector3.Lerp(_minPos, _maxPos, (currentIteration + 0.5f) / MemoryText.Count) + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _distortion * UnityEngine.Random.Range(-1f, 1f),
                _distortion);
        }
    }
}

public class SpectreTextSingle : SpectreTextManager
{
    private int _lastIteration = -1;

    private Vector3 _position;
    private float _distortion;

    public SpectreTextSingle(SpectreText refText, SpectreMazeTile tile, Vector3 position, float distortion) : base(refText, tile)
    {
        _position = position;
        _distortion = distortion;
    }

    protected override void UpdateText()
    {
        int animMult = 1;
        float timeSpan = 1.5f;

        if (MemoryText != null)
            Timer = ((Timer / animMult / timeSpan + 1) % (MemoryText.Count + 1) - 1) * animMult * timeSpan;

        //these are moments where I wish this version of c# had null-propagation
        List<string> memoryText = Tile.CurrentMemory != null && Tile.CurrentMemory.ShowAllData ? SpectreMazeTile.ParseStack(Tile.GetGoal()) : null;
        if (memoryText != null)
            memoryText = memoryText.SkipWhile(x => x == "?").ToList();

        if (memoryText == null || MemoryText == null || !MemoryText.SequenceEqual(memoryText))
        {
            MemoryText = memoryText;
            Timer = 0;
            _lastIteration = -animMult;
        }

        if (MemoryText == null)
            return;

        int currentIteration = (int)(Timer / timeSpan + animMult) - animMult;
        if (_lastIteration != currentIteration)
        {
            _lastIteration = currentIteration;
            if (currentIteration < 0)
                return;

            float angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
            AssignText(
                MemoryText[currentIteration / animMult],
                _position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _distortion * UnityEngine.Random.Range(0f, 1f),
                _distortion);
        }
    }
}