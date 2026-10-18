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

        protected override void Update()
        {
            if (sealVisual != null && GameManager.Instance != null)
                sealVisual.SetActive(!GameManager.Instance.GuardianDefeated);
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
            gameObject.SetActive(false);
        }
    }
}
