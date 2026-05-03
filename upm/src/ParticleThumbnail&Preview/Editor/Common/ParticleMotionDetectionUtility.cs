using System.Collections.Generic;
using UnityEngine;
// Analyzes particle-system configurations to determine whether motion-driven simulation is required for accurate preview and thumbnail playback.

namespace ParticleThumbnailAndPreview.Editor
{
    /// <summary>
    /// Shared motion detection used by both thumbnail and preview systems.
    /// A particle setup requires root motion when it emits by traveled distance
    /// in world simulation space.
    /// </summary>
    internal static class ParticleMotionDetectionUtility
    {
        private const float RateOverDistanceThreshold = 0.0001f;
        private static readonly HashSet<ParticleSystem> SubEmitterSystems = new();

        public static bool NeedsMotion(ParticleSystem[] systems)
        {
            if (systems == null)
                return false;

            return NeedsMotionInternal(systems);
        }

        public static bool NeedsMotion(IReadOnlyList<ParticleSystem> systems)
        {
            if (systems == null)
                return false;

            return NeedsMotionInternal(systems);
        }

        private static bool NeedsMotionInternal(IReadOnlyList<ParticleSystem> systems)
        {
            if (systems.Count == 0)
                return false;

            BuildSubEmitterSystemSet(systems);
            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                if (SubEmitterSystems.Contains(ps))
                    continue;

                ParticleSystem.MainModule main = ps.main;
                if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                    continue;

                if (ps.emission.rateOverDistanceMultiplier > RateOverDistanceThreshold)
                    return true;
            }

            return false;
        }

        private static void BuildSubEmitterSystemSet(IReadOnlyList<ParticleSystem> systems)
        {
            SubEmitterSystems.Clear();

            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                ParticleSystem.SubEmittersModule subEmitters = ps.subEmitters;
                int count = subEmitters.subEmittersCount;
                for (int j = 0; j < count; j++)
                {
                    ParticleSystem sub = subEmitters.GetSubEmitterSystem(j);
                    if (sub != null)
                        SubEmitterSystems.Add(sub);
                }
            }
        }
    }
}
