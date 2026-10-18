using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Feedback saat aktor (player / enemy) terkena damage atau mati:
    /// percikan + kilatan, angka damage yang melompat, camera shake, knockback,
    /// dan ledakan pecahan tulang + asap saat mati.
    /// </summary>
    public class DamageFeedback : MonoBehaviour
    {
        [SerializeField] private bool isPlayer;
        [SerializeField] private bool isBoss;
        [SerializeField] private Color sparkColor = new Color(1f, 0.85f, 0.55f);
        [SerializeField] private Color debrisColor = new Color(0.9f, 0.86f, 0.75f);
        [SerializeField] private float knockback = 4f;
        [SerializeField] private float bodyHeight = 1.2f;

        private Health health;
        private EnemyAI enemy;
        private PlayerController player;

        private void Awake()
        {
            health = GetComponent<Health>();
            enemy = GetComponent<EnemyAI>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            if (health == null)
                return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health == null)
                return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDamaged(float amount, Vector3 source)
        {
            Vector3 body = transform.position + Vector3.up * bodyHeight;
            Vector3 dir = body - source;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;

            VFXManager.Glow(body, Color.white, isBoss ? 2.2f : 1.5f, 0.1f);
            VFXManager.Sparks(body, dir, sparkColor, isBoss ? 18 : 12, 7f);
            VFXManager.DamageNumber(body + Vector3.up * (isBoss ? 2f : 1f), amount,
                isPlayer ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.95f, 0.6f), amount >= 20f);

            PlayHitSound(body, amount);

            if (isPlayer)
            {
                VFXManager.Shake(0.45f);
                if (player != null)
                    player.ApplyKnockback(dir * knockback);
            }
            else
            {
                VFXManager.Shake(isBoss ? 0.2f : 0.12f);
                if (enemy != null)
                    enemy.ApplyKnockback(dir * knockback);
            }
        }

        private void OnDied()
        {
            Vector3 body = transform.position + Vector3.up * bodyHeight;
            VFXManager.Glow(body, isBoss ? new Color(0.8f, 0.4f, 1f) : Color.white, isBoss ? 6f : 2.5f, 0.25f);
            VFXManager.Debris(body, debrisColor, isBoss ? 30 : 16, isBoss ? 8f : 5f, isBoss ? 0.22f : 0.14f);
            VFXManager.Smoke(transform.position + Vector3.up * 0.4f, new Color(0.3f, 0.28f, 0.34f, 0.8f), isBoss ? 18 : 10, isBoss ? 1.8f : 1.1f, 2.5f);

            PlayDeathSound(body);

            if (isBoss)
            {
                VFXManager.Ring(transform.position, new Color(0.8f, 0.4f, 1f), 9f, 0.8f);
                VFXManager.Shake(1f);
                VFXManager.SlowMotion(0.25f, 1.2f);
                VFXManager.FloatingLabel(body + Vector3.up * 2f, "GUARDIAN DEFEATED", new Color(1f, 0.85f, 0.3f), 34f);
                TopDownCamera.Focus(transform.position, 2.4f, 0.6f, 0.85f);
            }
            else if (isPlayer)
            {
                VFXManager.Shake(0.9f);
                VFXManager.SlowMotion(0.3f, 1f);
            }
            else
            {
                VFXManager.Shake(0.3f);
            }
        }

        /// <summary>Suara kena hit: player = pukulan + armor, Guardian = pelat logam, skeleton = tulang beradu.</summary>
        private void PlayHitSound(Vector3 body, float amount)
        {
            float vol = Mathf.Clamp(amount / 25f, 0.6f, 1.2f);
            if (isPlayer)
            {
                AudioManager.Play(Sfx.PlayerHurt, body, vol);
                AudioManager.Play(Sfx.PlayerHurtArmor, body, vol);
            }
            else if (isBoss)
            {
                AudioManager.Play(Sfx.SwordHitArmor, body, vol);
                AudioManager.Play(Sfx.SwordHitBody, body, vol, 0.8f);
            }
            else
            {
                AudioManager.Play(Sfx.SwordHitBone, body, vol);
                AudioManager.Play(Sfx.SwordHitBody, body, vol * 0.8f);
            }
        }

        /// <summary>Suara mati: skeleton runtuh (tulang berjatuhan), Guardian dentuman + gong, player jatuh.</summary>
        private void PlayDeathSound(Vector3 body)
        {
            if (isPlayer)
            {
                AudioManager.Play(Sfx.BodyFall, transform.position);
                AudioManager.Play(Sfx.PlayerHurtArmor, body, 1f, 0.8f);
            }
            else if (isBoss)
            {
                AudioManager.Play(Sfx.EnrageBoom, body);
                AudioManager.Play(Sfx.Gong, body, 1f, 0.9f, 0.15f);
                AudioManager.PlayBurst(Sfx.SwordHitBone, transform.position, 8, 0.07f, 1f);
                AudioManager.Play(Sfx.SwordHitArmor, body, 1f, 0.7f, 0.35f);
            }
            else
            {
                AudioManager.Play(Sfx.BoneBreak, body);
                AudioManager.PlayBurst(Sfx.SwordHitBone, transform.position, 4, 0.08f, 0.9f);
                AudioManager.Play(Sfx.BodyFall, transform.position, 0.5f, 1.3f, 0.2f);
            }
        }
    }
}
