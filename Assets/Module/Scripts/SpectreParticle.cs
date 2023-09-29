using UnityEngine;

public class SpectreParticle : MonoBehaviour
{
    public static readonly Vector3 MinBounds = new Vector3(-.075f, .04f, -.075f);
    public static readonly Vector3 MaxBounds = new Vector3(.075f, .06f, .075f);
    public static readonly Vector3 OpacityBounds = new Vector3(.005f, .005f, .005f);
    public static readonly float MaxVelocity = 0.025f / 60 / 5;

    public Vector3 Position;
    public Vector3 Speed;
    public float Opacity;
    public float LifeSpan;
    public ParticleBehaviour Behaviour;

    private MeshRenderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        new IdleBehaviour().Spawn(this);
        LifeSpan = UnityEngine.Random.Range(0f, 5f);
        Opacity = 0;
    }

    public void ParticleUpdate()
    {
        if (Behaviour != null)
        {
            Behaviour.Update(this);
        }

        Vector3 dimensions = MaxBounds - MinBounds;

        while (Position.x < MinBounds.x)
            Position.x += dimensions.x;
        while (Position.x > MaxBounds.x)
            Position.x -= dimensions.x;
        while (Position.y < MinBounds.y)
            Position.y += dimensions.y;
        while (Position.y > MaxBounds.y)
            Position.y -= dimensions.y;
        while (Position.z < MinBounds.z)
            Position.z += dimensions.z;
        while (Position.z > MaxBounds.z)
            Position.z -= dimensions.z;

        float borderOpacity = 1;
        if (Position.x < MinBounds.x + OpacityBounds.x)
            borderOpacity *= (Position.x - MinBounds.x) / OpacityBounds.x;
        if (Position.x > MaxBounds.x - OpacityBounds.x)
            borderOpacity *= (MaxBounds.x - Position.x) / OpacityBounds.x;
        if (Position.y < MinBounds.y + OpacityBounds.y)
            borderOpacity *= (Position.y - MinBounds.y) / OpacityBounds.y;
        if (Position.y > MaxBounds.y - OpacityBounds.y)
            borderOpacity *= (MaxBounds.y - Position.y) / OpacityBounds.y;
        if (Position.z < MinBounds.z + OpacityBounds.z)
            borderOpacity *= (Position.z - MinBounds.z) / OpacityBounds.z;
        if (Position.z > MaxBounds.z - OpacityBounds.z)
            borderOpacity *= (MaxBounds.z - Position.z) / OpacityBounds.z;

        _renderer.material.color = new Color(1, 1, 1, borderOpacity * Opacity / 4);
        transform.localPosition = Position;
    }



    public interface ParticleBehaviour
    {
        bool Repeat { get; set; }

        void Spawn(SpectreParticle particle);
        void Update(SpectreParticle particle);
    }

    public class IdleBehaviour : ParticleBehaviour
    {
        public bool Repeat { get; set; }

        public void Spawn(SpectreParticle particle)
        {
            Vector3 position = new Vector3(
                UnityEngine.Random.Range(MinBounds.x, MaxBounds.x),
                UnityEngine.Random.Range(MinBounds.y, MaxBounds.y),
                UnityEngine.Random.Range(MinBounds.z, MaxBounds.z));

            float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
            float verticalSpeed = UnityEngine.Random.Range(0, 2 * ((MinBounds.y + MaxBounds.y) / 2 - position.y) / 60) / 5;
            float horizontalSpeed = Mathf.Sqrt(MaxVelocity * MaxVelocity - verticalSpeed * verticalSpeed);
            Vector3 speed = new Vector3(horizontalSpeed * Mathf.Cos(angle), verticalSpeed, horizontalSpeed * Mathf.Sin(angle));

            particle.Position = position;
            particle.Speed = speed;
            particle.LifeSpan = 0;
            particle.Opacity = 0;

            particle.Behaviour = this;

            Repeat = true;
        }

        public void Update(SpectreParticle particle)
        {
            //using FixedUpdate
            float deltaTime = 1 / 60f;

            particle.LifeSpan += deltaTime;
            particle.Opacity = (1 - Mathf.Cos(particle.LifeSpan * Mathf.PI * 2 / 5)) / 2f;
            particle.Position += particle.Speed;

            if (particle.LifeSpan > 5)
            {
                if (!Repeat)
                {
                    particle.Behaviour = null;
                    particle.Opacity = 0;
                    return;
                }

                Spawn(particle);
            }
        }
    }
}
