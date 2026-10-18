using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonHunter
{
    /// <summary>
    /// Kontrol player top-down:
    /// WASD = bergerak, Shift = sprint (berisik), Mouse = arah hadap,
    /// Space / klik kanan = dodge (menerjang cepat + kebal sesaat / i-frame).
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

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed = 15f;
        [SerializeField] private float dodgeDuration = 0.28f;
        [SerializeField] private float dodgeCooldown = 0.75f;
        [SerializeField] private float invulnerableTime = 0.32f;
        [SerializeField] private float dodgeNoiseRadius = 4f;

        private CharacterController controller;
        private Health health;
        private CharacterAnimator characterAnimator;
        private Camera cam;
        private float verticalVelocity;
        private float nextFootstepTime;
        private Vector3 knockbackVelocity;

        private Vector3 dodgeDirection;
        private float dodgeTimer;
        private float invulnerableTimer;
        private float nextDodgeTime;
        private float nextGhostTime;
        private float lastEvadeFxTime = -10f;

        public bool IsSprinting { get; private set; }
        public bool IsDodging => dodgeTimer > 0f;
        public Vector3 Velocity { get; private set; }

        /// <summary>0 = baru saja dodge, 1 = dodge siap dipakai (untuk HUD).</summary>
        public float DodgeReady01 => Mathf.Clamp01(1f - (nextDodgeTime - Time.time) / dodgeCooldown);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            characterAnimator = GetComponent<CharacterAnimator>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.Evaded += OnEvaded;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Evaded -= OnEvaded;
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
            Mouse mouse = Mouse.current;
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

                bool dodgePressed = kb.spaceKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame);
                if (dodgePressed && !IsDodging && Time.time >= nextDodgeTime)
                    StartDodge(move);

                if (!IsDodging)
                    AimAtMouse();
            }

            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            Velocity = move * speed;

            if (IsDodging)
                UpdateDodge();
            UpdateInvulnerability();

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 25f * Time.deltaTime);
            controller.Move((Velocity + knockbackVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

            EmitFootsteps(move.sqrMagnitude > 0.01f && !IsDodging);
        }

        // =====================================================
        // DODGE
        // =====================================================

        /// <summary>Dodge ke arah input WASD; tanpa input → ke arah hadap.</summary>
        private void StartDodge(Vector3 moveInput)
        {
            dodgeDirection = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : transform.forward;
            dodgeDirection.y = 0f;
            dodgeTimer = dodgeDuration;
            invulnerableTimer = invulnerableTime;
            nextDodgeTime = Time.time + dodgeCooldown;
            nextGhostTime = 0f;
            knockbackVelocity = Vector3.zero;
            transform.rotation = Quaternion.LookRotation(dodgeDirection);

            if (health != null)
                health.Invulnerable = true;
            if (characterAnimator != null)
                characterAnimator.Dodge();

            NoiseSystem.Emit(transform.position, dodgeNoiseRadius);
            VFXManager.Smoke(transform.position + Vector3.up * 0.1f, new Color(0.55f, 0.5f, 0.45f, 0.6f), 6, 0.7f, 1.6f);
            AudioManager.Play(Sfx.SwordWhoosh, transform.position, 1f, 0.75f);
            AudioManager.Play(Sfx.PlayerStep, transform.position, 1.3f, 0.8f);
        }

        private void UpdateDodge()
        {
            dodgeTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(dodgeTimer / dodgeDuration);
            // cepat di awal, melambat di akhir (ease-out)
            Velocity = dodgeDirection * dodgeSpeed * (1f - t * t * 0.7f);

            if (Time.time >= nextGhostTime)
            {
                nextGhostTime = Time.time + 0.05f;
                DodgeGhost.Spawn(gameObject, new Color(0.18f, 0.42f, 0.75f, 0.3f));
            }

            if (dodgeTimer <= 0f)
                VFXManager.Smoke(transform.position + Vector3.up * 0.1f, new Color(0.55f, 0.5f, 0.45f, 0.5f), 3, 0.5f, 0.8f);
        }

        private void UpdateInvulnerability()
        {
            if (invulnerableTimer <= 0f)
                return;
            invulnerableTimer -= Time.deltaTime;
            if (invulnerableTimer <= 0f && health != null)
                health.Invulnerable = false;
        }

        /// <summary>Serangan berhasil dihindari saat i-frame → feedback "DODGE!".</summary>
        private void OnEvaded(float amount, Vector3 source)
        {
            if (Time.unscaledTime - lastEvadeFxTime < 0.4f)
                return;
            lastEvadeFxTime = Time.unscaledTime;

            Vector3 head = transform.position + Vector3.up * 2.2f;
            VFXManager.FloatingLabel(head, amount >= 20f ? "PERFECT DODGE!" : "DODGE!", new Color(0.5f, 0.9f, 1f), amount >= 20f ? 30f : 24f);
            VFXManager.Glow(transform.position + Vector3.up, new Color(0.45f, 0.8f, 1f), 2.5f, 0.2f);
            AudioManager.Play(Sfx.AlertSpotted, head, 0.6f, 1.3f);
            if (amount >= 20f)
                VFXManager.SlowMotion(0.4f, 0.35f);
        }

        // =====================================================
        // AIM & FOOTSTEPS
        // =====================================================

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

            // Sprint = berisik → kepulan debu di kaki sebagai petunjuk visual
            if (IsSprinting)
                VFXManager.Smoke(transform.position + Vector3.up * 0.1f, new Color(0.55f, 0.5f, 0.45f, 0.5f), 3, 0.5f, 0.8f);
        }

        /// <summary>Dorongan singkat saat terkena serangan.</summary>
        public void ApplyKnockback(Vector3 velocity)
        {
            if (IsDodging)
                return;
            velocity.y = 0f;
            knockbackVelocity = velocity;
        }
    }
}
