using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonHunter
{
    /// <summary>
    /// Kontrol player top-down:
    /// WASD = bergerak, Shift = sprint (berisik), Mouse = arah hadap.
    /// Langkah kaki memancarkan noise yang bisa didengar enemy.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float gravity = -20f;

        [Header("Noise (Hearing AI)")]
        [SerializeField] private float walkNoiseRadius = 2.5f;
        [SerializeField] private float sprintNoiseRadius = 8f;
        [SerializeField] private float footstepInterval = 0.4f;

        private CharacterController controller;
        private Health health;
        private Camera cam;
        private float verticalVelocity;
        private float nextFootstepTime;

        public bool IsSprinting { get; private set; }
        public Vector3 Velocity { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        private void Start()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            bool canControl = (GameManager.Instance == null || GameManager.Instance.IsPlaying)
                              && (health == null || !health.IsDead);

            Vector3 move = Vector3.zero;
            IsSprinting = false;

            Keyboard kb = Keyboard.current;
            if (canControl && kb != null)
            {
                Vector2 input = Vector2.zero;
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;

                move = new Vector3(input.x, 0f, input.y);
                if (move.sqrMagnitude > 1f)
                    move.Normalize();

                IsSprinting = kb.leftShiftKey.isPressed && move.sqrMagnitude > 0.01f;
                AimAtMouse();
            }

            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            Velocity = move * speed;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((Velocity + Vector3.up * verticalVelocity) * Time.deltaTime);

            EmitFootsteps(move.sqrMagnitude > 0.01f);
        }

        private void AimAtMouse()
        {
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null)
                return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

            if (!ground.Raycast(ray, out float enter))
                return;

            Vector3 look = ray.GetPoint(enter) - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(look);
        }

        private void EmitFootsteps(bool moving)
        {
            if (!moving || Time.time < nextFootstepTime)
                return;

            nextFootstepTime = Time.time + footstepInterval;
            NoiseSystem.Emit(transform.position, IsSprinting ? sprintNoiseRadius : walkNoiseRadius);
        }
    }
}
