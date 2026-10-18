using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Health Potion: memulihkan HP player saat didekati.
    /// Tidak diambil kalau HP masih penuh (tersimpan untuk nanti).
    /// </summary>
    public class HealthPotion : ProximityInteractable
    {
        [SerializeField] private float healAmount = 35f;

        private float nextHintTime;

        protected override void OnPlayerNear()
        {
            Health hp = GameManager.Instance.PlayerHealth;
            if (hp == null || hp.IsDead)
                return;

            if (hp.CurrentHealth >= hp.MaxHealth - 0.5f)
            {
                if (Time.time >= nextHintTime)
                {
                    nextHintTime = Time.time + 4f;
                    GameManager.Instance.ShowMessage("HP masih penuh. Potion disimpan untuk nanti.");
                }
                return;
            }

            float before = hp.CurrentHealth;
            hp.Heal(healAmount);
            float healed = hp.CurrentHealth - before;

            Color red = new Color(1f, 0.35f, 0.4f);
            Color green = new Color(0.45f, 1f, 0.55f);
            VFXManager.PickupBurst(transform.position, red);
            VFXManager.Heal(hp.transform.position, 8);
            VFXManager.Glow(hp.transform.position + Vector3.up, green, 3f, 0.3f);
            VFXManager.FloatingLabel(hp.transform.position + Vector3.up * 2.4f, $"+{Mathf.RoundToInt(healed)} HP", green, 30f);
            AudioManager.Play(Sfx.PotionPickup, transform.position);
            AudioManager.Play(Sfx.Heal, hp.transform.position);
            gameObject.SetActive(false);
        }
    }
}
