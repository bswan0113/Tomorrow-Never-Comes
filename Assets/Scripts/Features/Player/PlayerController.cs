using System.Collections;
using System.Linq;
using Core.Interface;
using Core.Interface.Core.Interface;
using Features.World;
using UnityEngine;
using VContainer;

namespace Features.Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;

        [Header("Interaction")]
        public float interactionDistance = 1.5f;
        public KeyCode interactionKey = KeyCode.Space;
        [Tooltip("상호작용 후 다시 상호작용이 가능해지기까지의 짧은 지연 시간")]
        public float interactionBufferTime = 0.2f;
        [Tooltip("플레이어 전방으로 인식할 각도 (0: 정면, 1: 90도까지, -1: 180도까지)")]
        [Range(-1f, 1f)]
        public float interactionDotProductThreshold = 0.5f; // 예를 들어 0.5면 약 60도 이내의 전방

        private Rigidbody2D rb;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Vector2 moveDirection;
        private Vector2 _lastNonZeroMoveDirection = Vector2.down; // 기본값은 아래쪽 (캐릭터가 처음에 바라보는 방향)

        private bool canControl = true;
        private bool isInteractionLocked = false;

        private IDialogueService _dialogueService;
        private IGameService _gameService;

        [Inject]
        public void Construct(IDialogueService dialogueService, IGameService gameService)
        {
            _dialogueService = dialogueService ?? throw new System.ArgumentNullException(nameof(dialogueService));
            _gameService = gameService ?? throw new System.ArgumentNullException(nameof(gameService));
            Debug.Log($"{gameObject.name}: 서비스 주입 완료");
        }

        private void OnEnable()
        {
            if (_dialogueService != null)
            {
                _dialogueService.OnDialogueStateChanged += HandleDialogueStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_dialogueService != null)
            {
                _dialogueService.OnDialogueStateChanged -= HandleDialogueStateChanged;
            }
        }

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void HandleDialogueStateChanged(bool isDialogueActive)
        {
            canControl = !isDialogueActive;
            if (!canControl)
            {
                rb.linearVelocity = Vector2.zero;
                animator.SetBool("isWalking", false);
            }
            else
            {
                StartCoroutine(LockInteractionForBufferTime());
            }
        }

        void Update()
        {
            if (!canControl) return;

            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float verticalInput = Input.GetAxisRaw("Vertical");
            moveDirection = new Vector2(horizontalInput, verticalInput).normalized;

            // 플레이어의 이동 방향이 있다면 최종 이동 방향을 업데이트 (정지 시에도 마지막 바라보는 방향 유지)
            if (moveDirection != Vector2.zero)
            {
                _lastNonZeroMoveDirection = moveDirection;
            }

            if (Input.GetKeyDown(interactionKey) && !isInteractionLocked)
            {
                TryInteract();
            }

            UpdateAnimationParameters();
        }

        void FixedUpdate()
        {
            if (canControl)
            {
                rb.linearVelocity = moveDirection * moveSpeed;
            }
        }

        void UpdateAnimationParameters()
        {
            bool isMoving = moveDirection.magnitude > 0.1f;
            animator.SetBool("isWalking", isMoving);
            if (isMoving)
            {
                animator.SetFloat("moveX", Mathf.Abs(moveDirection.x));
                animator.SetFloat("moveY", moveDirection.y);

                if (moveDirection.x < 0)
                {
                    spriteRenderer.flipX = true;
                }
                else if (moveDirection.x > 0)
                {
                    spriteRenderer.flipX = false;
                }
            }
        }

        private void TryInteract()
        {
            if (_dialogueService != null && _dialogueService.IsDialogueActive())
            {
                return;
            }

            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionDistance);

            // 상호작용 가능한 오브젝트들 중 플레이어의 전방에 있는 오브젝트만 필터링
            var potentialInteractables = colliders.Select(c => c.GetComponent<IInteractable>())
                .Where(i => i != null)
                .OfType<MonoBehaviour>()
                .Where(m => {
                    // 플레이어 위치에서 오브젝트까지의 방향 벡터
                    Vector2 directionToObject = (m.transform.position - transform.position).normalized;
                    // 플레이어의 현재 바라보는 방향과 오브젝트 방향의 내적
                    float dotProduct = Vector2.Dot(_lastNonZeroMoveDirection, directionToObject);
                    // 내적 값이 임계값보다 크면 전방에 있다고 판단
                    return dotProduct > interactionDotProductThreshold;
                })
                .Select(m => m.GetComponent<IInteractable>());

            // 필터링된 오브젝트들 중 가장 가까운 오브젝트 선택
            var nearestInteractable = potentialInteractables.OrderBy(m => Vector2.Distance(transform.position, (m as MonoBehaviour).transform.position))
                .FirstOrDefault();

            if (nearestInteractable != null)
            {
                Debug.Log($"<color=green>Nearest interactable found: {(nearestInteractable as MonoBehaviour).gameObject.name}. Calling Interact()...</color>");
                nearestInteractable.Interact();
            }
            else
            {
                Debug.Log("주변에 상호작용할 수 있는 것이 없습니다.");
            }
        }

        private IEnumerator LockInteractionForBufferTime()
        {
            isInteractionLocked = true;
            yield return new WaitForSeconds(interactionBufferTime);
            isInteractionLocked = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);

            // Gizmos로 플레이어의 바라보는 방향 시각화
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + _lastNonZeroMoveDirection * interactionDistance);

            // Gizmos로 상호작용 범위 (Cone) 시각화 (대략적)
            // Cosine 값을 이용하여 각도 계산 (interactionDotProductThreshold 가 cosine 값)
            float angle = Mathf.Acos(interactionDotProductThreshold) * Mathf.Rad2Deg;
            if (float.IsNaN(angle)) angle = 0f; // handle threshold 1 (angle 0)

            Vector3 fwd = _lastNonZeroMoveDirection;
            Vector3 left = Quaternion.Euler(0, 0, angle) * fwd;
            Vector3 right = Quaternion.Euler(0, 0, -angle) * fwd;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + (Vector2)left * interactionDistance);
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + (Vector2)right * interactionDistance);
        }
    }
}