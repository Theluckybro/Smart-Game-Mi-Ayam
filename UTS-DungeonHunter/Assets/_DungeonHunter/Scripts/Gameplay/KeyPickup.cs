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
            TopDownCamera.Focus(transform.position, 0.9f, 0.82f, 0f, false);
            AudioManager.Play(Sfx.KeyPickup, transform.position);
            AudioManager.Play2D(Sfx.JingleKey, 1f, 1f, 0.1f);
            gameObject.SetActive(false);
        }
    }
}
