using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Dungeon Key: membuka pintu ruang Guardian.</summary>
    public class KeyPickup : ProximityInteractable
    {
        protected override void OnPlayerNear()
        {
            GameManager.Instance.CollectKey();
            gameObject.SetActive(false);
        }
    }
}
