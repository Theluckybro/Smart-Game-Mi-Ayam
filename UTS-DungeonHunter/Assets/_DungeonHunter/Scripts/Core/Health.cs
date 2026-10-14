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

        /// <summary>Dipanggil saat menerima damage: (jumlah damage, posisi sumber).</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;

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

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(amount, sourcePosition);

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

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }
    }
}
