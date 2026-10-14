using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Ancient Artifact: diambil player → WIN.</summary>
    public class ArtifactPickup : ProximityInteractable
    {
        protected override void OnPlayerNear()
        {
            GameManager.Instance.CollectArtifact();
            gameObject.SetActive(false);
        }
    }
}
