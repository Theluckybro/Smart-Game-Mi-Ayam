using UnityEngine;

namespace DungeonHunter
{
    /// <summary>
    /// Pintu ruang Guardian (tempat Artifact). Terbuka jika player membawa Key.
    /// Memakai NavMeshObstacle (carve) sehingga NavMesh terbuka setelah pintu dibuka.
    /// </summary>
    public class LockedDoor : ProximityInteractable
    {
        [SerializeField] private Transform doorPanel;
        [SerializeField] private float openDepth = 2.6f;
        [SerializeField] private float openSpeed = 3f;

        private bool opening;
        private float nextHintTime;
        private Vector3 closedPos;

        protected override void Start()
        {
            base.Start();
            if (doorPanel != null)
                closedPos = doorPanel.localPosition;
        }

        protected override void Update()
        {
            if (opening && doorPanel != null)
            {
                Vector3 openPos = closedPos + Vector3.down * openDepth;
                doorPanel.localPosition = Vector3.MoveTowards(doorPanel.localPosition, openPos, openSpeed * Time.deltaTime);
                if ((doorPanel.localPosition - openPos).sqrMagnitude < 0.0001f)
                {
                    foreach (Collider c in GetComponentsInChildren<Collider>())
                        c.enabled = false;
                    var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
                    if (obstacle != null)
                        obstacle.enabled = false;
                    enabled = false;
                }
                return;
            }
            base.Update();
        }

        protected override void OnPlayerNear()
        {
            if (opening)
                return;

            if (!GameManager.Instance.HasKey)
            {
                if (Time.time >= nextHintTime)
                {
                    nextHintTime = Time.time + 3f;
                    GameManager.Instance.ShowMessage("Pintu terkunci. Butuh Dungeon Key.");
                }
                return;
            }

            opening = true;
            NoiseSystem.Emit(transform.position, 12f);
            for (int i = -1; i <= 1; i++)
                VFXManager.Smoke(transform.position + transform.right * (i * 1.4f) + Vector3.up * 0.2f,
                    new Color(0.45f, 0.42f, 0.4f, 0.8f), 5, 1.1f, 1.5f);
            VFXManager.Shake(0.25f);
            AudioManager.Play(Sfx.DoorUnlock, transform.position);
            AudioManager.Play(Sfx.DoorOpen, transform.position, 1f, 1f, 0.2f);
            AudioManager.Play(Sfx.DoorCreak, transform.position, 1f, 1f, 0.45f);
            GameManager.Instance.NotifyDoorOpened();
            IntroduceGuardian();
        }

        /// <summary>Intro boss: kamera menyorot Guardian sesaat (letterbox) + bunyi gong.</summary>
        private void IntroduceGuardian()
        {
            GuardianEnemy guardian = FindFirstObjectByType<GuardianEnemy>();
            if (guardian == null || guardian.State == EnemyState.Dead)
                return;
            Vector3 p = guardian.transform.position;
            TopDownCamera.Focus(p, 2.4f, 0.8f, 0.6f);
            AudioManager.Play(Sfx.Gong, p, 0.9f, 1f, 0.5f);
        }
    }
}
