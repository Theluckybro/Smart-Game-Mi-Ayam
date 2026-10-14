using System;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Sistem suara sederhana untuk Hearing perception.
    /// Player memancarkan noise (serangan, sprint), enemy yang berada
    /// di dalam radius noise akan "mendengar" posisi tersebut.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>(posisi sumber, radius terdengar)</summary>
        public static event Action<Vector3, float> NoiseEmitted;

        public static void Emit(Vector3 position, float radius)
        {
            NoiseEmitted?.Invoke(position, radius);
        }
    }
}
