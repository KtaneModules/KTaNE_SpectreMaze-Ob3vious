using UnityEngine;

public class SpectreText : MonoBehaviour
{

    private float _currentOpacity = 0;
    public float Opacity { get; set; }
    public Vector3 Speed { get; set; }

    private TextMesh _text;

    void Awake()
    {
        Opacity = 0;
        Speed = Vector3.zero;
        _text = GetComponent<TextMesh>();
        SetText("", new Color());
    }

    void Update()
    {
        Opacity = Mathf.Clamp(Opacity, 0, 1);

        float speed = 1 / 5f;
        if (_currentOpacity > Opacity)
        {
            _currentOpacity -= speed * Time.deltaTime;
            _currentOpacity = Mathf.Clamp(_currentOpacity, Opacity, 1);
        }
        else if (_currentOpacity < Opacity)
        {
            _currentOpacity += speed * Time.deltaTime;
            if (_currentOpacity >= Opacity)
            {
                _currentOpacity = 2 * Opacity - _currentOpacity;
                Opacity = 0;
            }
        }

        transform.localPosition += Speed * speed * Time.deltaTime;
        _text.color = new Color(_text.color.r, _text.color.g, _text.color.b, _currentOpacity);

    }

    public void SetText(string text, Color color)
    {
        _text.text = text;
        _text.color = color;
    }

    public bool IsAvailable()
    {
        return _currentOpacity <= 0;
    }
}
