using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Artifact tersegel selama Guardian masih hidup.
    /// Diambil setelah Guardian kalah → WIN.
    /// </summary>
    public class ArtifactPickup : ProximityInteractable
    {
        [SerializeField] private GameObject sealVisual;
        private float nextHintTime;
        private bool sealBroken;

        protected override void Update()
        {
            if (sealVisual != null && GameManager.Instance != null)
                sealVisual.SetActive(!GameManager.Instance.GuardianDefeated);

            // Segel pecah saat Guardian kalah
            if (!sealBroken && GameManager.Instance != null && GameManager.Instance.GuardianDefeated)
            {
                sealBroken = true;
                Color purple = new Color(0.75f, 0.35f, 1f);
                VFXManager.Ring(transform.position, purple, 3.5f, 0.6f);
                VFXManager.Sparks(transform.position + Vector3.up, Vector3.up, purple, 24, 6f, 1.2f);
                AudioManager.Play(Sfx.SealBreak, transform.position);
                AudioManager.Play(Sfx.MageCharge, transform.position, 2f, 0.7f, 0.1f);
            }
            base.Update();
        }

        protected override void OnPlayerNear()
        {
            if (!GameManager.Instance.GuardianDefeated)
            {
                if (Time.time >= nextHintTime)
                {
                    nextHintTime = Time.time + 3f;
                    GameManager.Instance.ShowMessage("Artifact tersegel! Kalahkan Guardian terlebih dahulu.");
                }
                return;
            }

            GameManager.Instance.CollectArtifact();
            Color cyan = new Color(0.35f, 0.95f, 1f);
            VFXManager.PickupBurst(transform.position, cyan);
            VFXManager.Ring(transform.position, Color.white, 6f, 0.8f);
            VFXManager.Shake(0.35f);
            VFXManager.SlowMotion(0.35f, 0.8f);
            AudioManager.Play(Sfx.ArtifactPickup, transform.position);
            AudioManager.Play(Sfx.MageCharge, transform.position, 2.5f, 1.6f);
            gameObject.SetActive(false);
        }
    }
}
