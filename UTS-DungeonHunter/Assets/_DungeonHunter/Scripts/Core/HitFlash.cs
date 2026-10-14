using System.Collections;
using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Visual feedback: renderer berkedip saat terkena damage,
    /// dan bisa dipakai untuk "telegraph" serangan enemy.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Coroutine routine;
        private Health health;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount, Vector3 source)
        {
            Flash(flashColor, flashDuration);
        }

        public void Flash(Color color, float duration)
        {
            if (!isActiveAndEnabled)
                return;

            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            SetColor(color, true);
            yield return new WaitForSeconds(duration);
            SetColor(color, false);
            routine = null;
        }

        private void SetColor(Color color, bool on)
        {
            foreach (Renderer r in renderers)
            {
                if (r == null || r is LineRenderer)
                    continue;

                if (on)
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, color);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
