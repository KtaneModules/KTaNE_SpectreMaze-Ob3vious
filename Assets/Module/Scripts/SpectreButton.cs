using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpectreButton : MonoBehaviour
{
    public float Speed { get; set; }

    public KMSelectable Selectable;
    public float Extended { get; set; }
    private float _currentExtended = 0;

    private int _index;

    public static readonly Vector2[] Coordinates =
    {
        new Vector2(0, 1/2f),
        new Vector2(Mathf.Sqrt(3)/4f, 5/4f),
        new Vector2(Mathf.Sqrt(3)/2f - 1/4f, 3/2f + Mathf.Sqrt(3)/4f),
        new Vector2(Mathf.Sqrt(3)/2f - 1/4f, 3/2f + 3 * Mathf.Sqrt(3)/4f),
        new Vector2(3 * Mathf.Sqrt(3)/4f, 5/4f + Mathf.Sqrt(3)),
        new Vector2(5 * Mathf.Sqrt(3)/4f, 5/4f + Mathf.Sqrt(3)),
        new Vector2(1/4f + 3 * Mathf.Sqrt(3)/2f, 3/2f + 3 * Mathf.Sqrt(3)/4f),
        new Vector2(1 + 3 * Mathf.Sqrt(3)/2f, 3/2f + Mathf.Sqrt(3)/2f),
        new Vector2(3/2f + 3 * Mathf.Sqrt(3)/2f, 1 + Mathf.Sqrt(3)/2f),
        new Vector2(3/2f + 5 * Mathf.Sqrt(3)/4f, 1/4f + Mathf.Sqrt(3)/2f),
        new Vector2(3/2f + 3 * Mathf.Sqrt(3)/4f, Mathf.Sqrt(3)/2f - 1/4f),
        new Vector2(3/2f + Mathf.Sqrt(3)/4f, Mathf.Sqrt(3)/2f - 1/4f),
        new Vector2(5/4f, Mathf.Sqrt(3)/4f),
        new Vector2(1/2f, 0),
    };
    public static readonly float[] Orientations =
    {
        90,
        150,
        60,
        120,
        210,
        150,
        240,
        180,
        270,
        330,
        330,
        30,
        300,
        0,
    };
    private static readonly Color[] _colours =
    {
        new Color32(0xc0, 0xb8, 0xb0, 0xff),
        new Color32(0xb0, 0xa4, 0xa0, 0xff),
        new Color32(0xb0, 0xb8, 0xc0, 0xff),
        new Color32(0xa0, 0xac, 0xb0, 0xff)
    };


    void Awake()
    {
        Selectable = GetComponent<KMSelectable>();
        Extended = 1;
    }

    public List<SpectreButton> BuildClones()
    {
        List<SpectreButton> buttons = new List<SpectreButton>();
        for (int i = 0; i < 14; i++)
        {
            SpectreButton button = Instantiate(this, transform.parent);
            button.transform.localEulerAngles = new Vector3(-90, Orientations[i], 0);
            button.transform.localPosition = new Vector3(Coordinates[i].x * -0.01f, transform.localPosition.y, Coordinates[i].y * -0.01f);
            buttons.Add(button);
            button._index = i;
            button.GetComponent<MeshRenderer>().material.color = Color.Lerp(_colours[(i % 2) * 2], _colours[(i % 2) * 2 + 1], (Coordinates[i].x + Coordinates[i].y) / Coordinates.Max(x => x.x + x.y));
        }
        transform.localScale = Vector3.zero;
        Destroy(this);
        return buttons;
    }

    void Update()
    {
        Extended = Mathf.Clamp(Extended, 0, 1);

        if (_currentExtended > Extended)
        {
            _currentExtended -= Speed * Time.deltaTime;
            _currentExtended = Mathf.Clamp(_currentExtended, Extended, 1);
        }
        else if (_currentExtended < Extended)
        {
            _currentExtended += Speed * Time.deltaTime;
            _currentExtended = Mathf.Clamp(_currentExtended, 0, Extended);
        }

        float angleRad = Orientations[_index] * Mathf.PI / 180;
        transform.localPosition = new Vector3(Coordinates[_index].x * -0.01f, transform.localPosition.y, Coordinates[_index].y * -0.01f) + (1 - _currentExtended) * new Vector3(Mathf.Sin(angleRad), 0, Mathf.Cos(angleRad)) * -0.005f;
    }

}
