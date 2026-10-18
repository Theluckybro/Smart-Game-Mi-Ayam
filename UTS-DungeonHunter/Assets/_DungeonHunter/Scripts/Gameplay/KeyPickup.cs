using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Dungeon Key: membuka pintu ruang Guardian.</summary>
    public class KeyPickup : ProximityInteractable
    {
        protected override void OnPlayerNear()
        {
            GameManager.Instance.CollectKey();
            Color gold = new Color(1f, 0.82f, 0.3f);
            VFXManager.PickupBurst(transform.position, gold);
            VFXManager.FloatingLabel(transform.position + Vector3.up * 2.2f, "DUNGEON KEY!", gold, 30f);
            VFXManager.Shake(0.2f);
            gameObject.SetActive(false);
        }
    }
}
