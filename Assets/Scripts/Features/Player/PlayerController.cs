// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerController.cs

using System.Collections;
using System.Linq;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
using Features.World; // ISceneTransitionService를 사용하기 위해 추가
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

        private bool _canMove = true; // 이동 가능 여부
        private bool _canInteract = true; // 상호작용 가능 여부 (interactionBufferTime과 별개로 전체 제어)
        private bool _isInteractionLockedByBuffer = false; // interactionBufferTime에 의한 상호작용 잠금

        private IDialogueService _dialogueService;
        private IGameService _gameService;
        private ISceneTransitionService _sceneTransitionService; // 추가: 씬 전환 서비스

        [Inject]
        public void Construct(IDialogueService dialogueService, IGameService gameService, ISceneTransitionService sceneTransitionService) // ISceneTransitionService 주입
        {
            _dialogueService = dialogueService ?? throw new System.ArgumentNullException(nameof(dialogueService));
            _gameService = gameService ?? throw new System.ArgumentNullException(nameof(gameService));
            _sceneTransitionService = sceneTransitionService ?? throw new System.ArgumentNullException(nameof(sceneTransitionService)); // 씬 전환 서비스 null 체크
            CoreLogger.Log($"{gameObject.name}: 서비스 주입 완료");
        }

        private void OnEnable()
        {
            if (_dialogueService != null)
            {
                _dialogueService.OnDialogueStateChanged += HandleDialogueStateChanged;
            }
            if (_sceneTransitionService != null) // 추가: 씬 전환 이벤트 구독
            {
                _sceneTransitionService.OnTransitionStateChanged += HandleTransitionStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_dialogueService != null)
            {
                _dialogueService.OnDialogueStateChanged -= HandleDialogueStateChanged;
            }
            if (_sceneTransitionService != null) // 추가: 씬 전환 이벤트 구독 해제
            {
                _sceneTransitionService.OnTransitionStateChanged -= HandleTransitionStateChanged;
            }
        }

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            // 초기 상태 설정
            UpdateControlState();
        }

        private void HandleDialogueStateChanged(bool isDialogueActive)
        {
            UpdateControlState();
            if (isDialogueActive) // 대화 시작 시
            {
                // 즉시 이동 중단 및 애니메이션 정지
                rb.linearVelocity = Vector2.zero;
                animator.SetBool("isWalking", false);
            }
            else // 대화 종료 시
            {
                // 상호작용 버퍼 시간 적용 (대화 종료 후 바로 다른 상호작용 막기 위함)
                StartCoroutine(LockInteractionForBufferTime());
            }
        }

        // 추가: 씬 전환 상태 변경 핸들러
        private void HandleTransitionStateChanged(bool isTransitioning)
        {
            UpdateControlState();
            if (isTransitioning) // 씬 전환 시작 시
            {
                // 즉시 이동 중단 및 애니메이션 정지
                rb.linearVelocity = Vector2.zero;
                animator.SetBool("isWalking", false);
            }
        }

        // 플레이어의 전반적인 제어 가능 상태를 업데이트하는 통합 메서드
        private void UpdateControlState()
        {
            // 대화 중이거나 씬 전환 중이면 이동 및 상호작용 불가능
            bool isBlockedByDialogueOrTransition = (_dialogueService != null && _dialogueService.IsDialogueActive()) ||
                                                  (_sceneTransitionService != null && _sceneTransitionService.IsTransitioning);

            _canMove = !isBlockedByDialogueOrTransition;
            _canInteract = !isBlockedByDialogueOrTransition && !_isInteractionLockedByBuffer;
        }

        void Update()
        {
            // 이동 처리
            if (_canMove)
            {
                float horizontalInput = Input.GetAxisRaw("Horizontal");
                float verticalInput = Input.GetAxisRaw("Vertical");
                moveDirection = new Vector2(horizontalInput, verticalInput).normalized;

                if (moveDirection != Vector2.zero)
                {
                    _lastNonZeroMoveDirection = moveDirection;
                }
            }
            else
            {
                moveDirection = Vector2.zero; // 제어 불가능 상태에서는 이동 입력 무시
            }

            // 상호작용 처리
            if (_canInteract && Input.GetKeyDown(interactionKey))
            {
                TryInteract();
            }

            UpdateAnimationParameters();
        }

        void FixedUpdate()
        {
            if (_canMove)
            {
                rb.linearVelocity = moveDirection * moveSpeed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero; // 제어 불가능 상태에서는 플레이어 강제 정지
            }
        }

        void UpdateAnimationParameters()
        {
            bool isMoving = moveDirection.magnitude > 0.1f; // _canMove 상태와 무관하게 실제 moveDirection으로 판단
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
            // 이동이 중단된 경우에도 캐릭터는 마지막 바라보는 방향을 유지해야 하므로, flipX는 여기서 변경하지 않습니다.
        }

        private void TryInteract()
        {
            if (!_canInteract) // 상호작용이 불가능한 상태라면 즉시 반환
            {
                CoreLogger.Log("Interaction currently blocked.");
                return;
            }

            // _isInteractionLockedByBuffer는 _canInteract에 포함되므로 이 중복 체크는 제거 가능하지만,
            // 명시성을 위해 남겨둘 수도 있습니다. 여기서는 _canInteract에 포함시켰습니다.

            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionDistance);

            var potentialInteractables = colliders.Select(c => c.GetComponent<IInteractable>())
                .Where(i => i != null)
                .OfType<MonoBehaviour>()
                .Where(m => {
                    Vector2 directionToObject = (m.transform.position - transform.position).normalized;
                    float dotProduct = Vector2.Dot(_lastNonZeroMoveDirection, directionToObject);
                    return dotProduct > interactionDotProductThreshold;
                })
                .Select(m => m.GetComponent<IInteractable>());

            var nearestInteractable = potentialInteractables.OrderBy(m => Vector2.Distance(transform.position, (m as MonoBehaviour).transform.position))
                .FirstOrDefault();

            if (nearestInteractable != null)
            {
                CoreLogger.Log($"<color=green>Nearest interactable found: {(nearestInteractable as MonoBehaviour).gameObject.name}. Calling Interact()...</color>");
                nearestInteractable.Interact();
                // 상호작용 성공 후 버퍼 시간 적용
                StartCoroutine(LockInteractionForBufferTime());
            }
            else
            {
                CoreLogger.Log("주변에 상호작용할 수 있는 것이 없습니다.");
            }
        }

        private IEnumerator LockInteractionForBufferTime()
        {
            _isInteractionLockedByBuffer = true;
            UpdateControlState(); // 버퍼 잠금 상태가 변경되었으므로 제어 상태 업데이트
            yield return new WaitForSeconds(interactionBufferTime);
            _isInteractionLockedByBuffer = false;
            UpdateControlState(); // 버퍼 잠금 해제 상태가 변경되었으므로 제어 상태 업데이트
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + _lastNonZeroMoveDirection * interactionDistance);

            float angle = Mathf.Acos(interactionDotProductThreshold) * Mathf.Rad2Deg;
            if (float.IsNaN(angle)) angle = 0f;

            Vector3 fwd = _lastNonZeroMoveDirection;
            Vector3 left = Quaternion.Euler(0, 0, angle) * fwd;
            Vector3 right = Quaternion.Euler(0, 0, -angle) * fwd;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + (Vector2)left * interactionDistance);
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + (Vector2)right * interactionDistance);
        }
    }
}