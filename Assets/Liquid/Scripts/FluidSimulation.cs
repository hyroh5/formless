using UnityEngine;
using static UnityEngine.ParticleSystem;

public class FluidSimulation : MonoBehaviour
{
    private Particle[] allParticles;

    void Update()
    {
        allParticles = FindObjectsOfType<Particle>();
        foreach (Particle p in allParticles)
        {
            p.UpdateState();
        }
    }
}
