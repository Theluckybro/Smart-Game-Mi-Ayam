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
            GameManager.Instance.NotifyDoorOpened();
        }
    }
}
