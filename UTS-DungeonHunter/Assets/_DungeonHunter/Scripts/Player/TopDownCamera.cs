using UnityEngine;

namespace DungeonHunter
{
    /// <summary>Kamera top-down yang mengikuti player dengan halus.</summary>
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 16f, -9f);
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 velocity;

        public void SetTarget(Transform newTarget) => target = newTarget;

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.rotation = Quaternion.LookRotation(target.position - (target.position + offset));
        }
    }
}
