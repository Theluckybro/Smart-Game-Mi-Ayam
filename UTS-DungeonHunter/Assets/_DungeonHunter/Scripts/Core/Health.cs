using System;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Komponen HP generik yang dipakai Player maupun Enemy.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
        public bool IsDead { get; private set; }

        /// <summary>Kebal sementara (i-frame saat dodge). Damage yang masuk dibatalkan dan memicu event Evaded.</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Dipanggil saat menerima damage: (jumlah damage, posisi sumber).</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;
        public event Action<float, Vector3> Evaded;
        public event Action<float> Healed;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = value;
            if (refill)
                CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (IsDead || amount <= 0f)
                return;

            if (Invulnerable)
            {
                Evaded?.Invoke(amount, sourcePosition);
                return;
            }

            float applied = Mathf.Min(amount, CurrentHealth); // damage sebenarnya (tidak melebihi sisa HP)
            CurrentHealth -= applied;
            Damaged?.Invoke(applied, sourcePosition);

            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            float before = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            if (CurrentHealth > before)
                Healed?.Invoke(CurrentHealth - before);
        }
    }
}
