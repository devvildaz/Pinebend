using UnityEngine;

namespace Pinebend
{
    public class RallyDust : MonoBehaviour
    {
        ParticleSystem dust;
        float nextEmission;

        void Awake()
        {
            var puff = new GameObject("Rear tire dust");
            puff.transform.SetParent(transform, false);
            dust = puff.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .58f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .29f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var emission = dust.emission;
            emission.enabled = false;
            var shape = dust.shape;
            shape.enabled = false;
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.55f),new Keyframe(.4f,1),new Keyframe(1,0)));
            // Reuse the circuit surface material for little angular gravel puffs.
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            var surface = GameObject.Find("Apricot gravel");
            if (surface) renderer.sharedMaterial = surface.GetComponent<MeshRenderer>().sharedMaterial;
            dust.Play();
        }

        public void Clear() => dust.Clear();

        public void EmitFrom(WheelCollider[] wheels, bool sliding, Vector3 velocity)
        {
            if (!sliding || Time.time < nextEmission) return;
            nextEmission = Time.time + .045f;
            for (int i = 2; i < 4; i++)
            {
                if (!wheels[i].GetGroundHit(out var hit)) continue;
                dust.Emit(new ParticleSystem.EmitParams {
                    position = hit.point + Vector3.up * .12f,
                    velocity = velocity * .09f + Vector3.up * Random.Range(.6f,1.4f) + Random.insideUnitSphere * .55f
                }, 2);
            }
        }
    }
}
