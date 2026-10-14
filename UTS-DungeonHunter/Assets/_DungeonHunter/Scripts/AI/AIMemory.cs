using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Memory sederhana enemy:
    /// Last Known Position, Last Seen Time, Last Heard Position, dan sumber informasinya.
    /// Memory "kadaluarsa" setelah memoryDuration detik tanpa update.
    /// </summary>
    public class AIMemory : MonoBehaviour
    {
        public enum InfoSource
        {
            None,
            Vision,
            Hearing,
            Damage
        }

        [SerializeField] private float memoryDuration = 12f;

        public bool HasLastKnownPosition { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public InfoSource LastSource { get; private set; } = InfoSource.None;
        public float LastSeenTime { get; private set; } = -999f;
        public float LastUpdateTime { get; private set; } = -999f;
        public Vector3 LastHeardPosition { get; private set; }

        public float TimeSinceSeen => Time.time - LastSeenTime;
        public bool IsFresh => HasLastKnownPosition && Time.time - LastUpdateTime <= memoryDuration;

        public void RememberSeen(Vector3 position)
        {
            Store(position, InfoSource.Vision);
            LastSeenTime = Time.time;
        }

        public void RememberHeard(Vector3 position)
        {
            LastHeardPosition = position;
            Store(position, InfoSource.Hearing);
        }

        public void RememberDamage(Vector3 position)
        {
            Store(position, InfoSource.Damage);
        }

        public void Forget()
        {
            HasLastKnownPosition = false;
            LastSource = InfoSource.None;
        }

        private void Store(Vector3 position, InfoSource source)
        {
            HasLastKnownPosition = true;
            LastKnownPosition = position;
            LastSource = source;
            LastUpdateTime = Time.time;
        }

        private void Update()
        {
            if (HasLastKnownPosition && !IsFresh)
                Forget();
        }
    }
}
