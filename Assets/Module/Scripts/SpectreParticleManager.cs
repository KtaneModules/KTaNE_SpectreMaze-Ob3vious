using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpectreParticleManager
{
    private List<SpectreParticle> _particles = new List<SpectreParticle>();
    private List<SpectreWind> _wind = new List<SpectreWind>();

    public void AddParticle(SpectreParticle particle)
    {
        _particles.Add(particle);
    }

    public void ApplyWind(Vector3 wind, bool strike)
    {
        if (strike)
            _wind.Add(new SpectreStrikeWind(wind));
        else
            _wind.Add(new SpectrePassWind(wind));
    }

    public void UpdateParticles()
    {
        _wind = _wind.Where(x => !x.IsDone()).ToList();
        foreach (SpectreWind wind in _wind)
        {
            Vector3 translation = wind.UpdateVector();
            foreach (SpectreParticle particle in _particles)
                particle.Position += translation;
        }

        foreach (SpectreParticle particle in _particles)
            particle.ParticleUpdate();
    }

    internal void Terminate()
    {
        foreach (SpectreParticle particle in _particles)
            particle.Behaviour.Repeat = false;
    }

    private interface SpectreWind
    {
        Vector3 UpdateVector();
        bool IsDone();
    }

    private class SpectrePassWind : SpectreWind
    {
        private float _windTimer;
        private Vector3 _wind;

        public SpectrePassWind(Vector3 wind)
        {
            _windTimer = 0;
            _wind = wind;
        }

        public Vector3 UpdateVector()
        {
            float oldTime = _windTimer;
            float deltaTime = 1 / 60f;
            _windTimer += deltaTime;
            return _wind * (1 - Mathf.Cos(oldTime * 2 * Mathf.PI));
        }

        public bool IsDone()
        {
            return _windTimer > 1;
        }
    }

    private class SpectreStrikeWind : SpectreWind
    {
        private float _windTimer;
        private Vector3 _wind;

        public SpectreStrikeWind(Vector3 wind)
        {
            _windTimer = 0;
            _wind = wind;
        }

        public Vector3 UpdateVector()
        {
            float oldTime = _windTimer;
            float deltaTime = 1 / 60f;
            _windTimer += deltaTime;
            if (_windTimer > 0.25f && _windTimer < 0.5f)
                _windTimer = 1 - _windTimer;
            return _wind * (1 - Mathf.Cos(oldTime * 2 * Mathf.PI)) * (oldTime > 0.5f ? -0.25f : 1);
        }

        public bool IsDone()
        {
            return _windTimer > 1;
        }
    }
}
