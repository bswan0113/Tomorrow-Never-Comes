// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerController.cs

using System.Collections;
using System.Linq;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
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

        private bool _canMove = true; // 이동 가능 여부
        private bool _canInteract = true; // 상호작용 가능 여부 (interactionBufferTime과 별개로 전체 제어)
        private bool _isInteractionLockedByBuffer = false; // interactionBufferTime에 의한 상호작용 잠금

        private IDialogueService _dialogueService;
        private IGameService _gameService;
        private ISceneTransitionService _sceneTransitionService;

        [Inject]
        public void Construct(IDialogueService dialogueService, IGameService gameService, ISceneTransitionService sceneTransitionService)
        {
            _dialogueService = dialogueService ?? throw new System.ArgumentNullException(nameof(dialogueService));
            _gameService = gameService ?? throw new System.ArgumentNullException(nameof(gameService));
            _sceneTransitionService = sceneTransitionService ?? throw new System.ArgumentNullException(nameof(sceneTransitionService));
            CoreLogger.LogDebug($"{gameObject.name}: 서비스 주입 완료"); // Debug 레벨로 변경
        }

        // OnEnable에서 Start로 이벤트 구독 시점을 변경합니다.
        // OnEnable 시점에는 [Inject]가 완료되지 않았을 수 있기 때문입니다.
        // OnDisable은 여전히 구독 해지 역할을 합니다.
        private void OnDisable()
        {
            if (_dialogueService != null)
            {
                _dialogueService.OnDialogueStateChanged -= HandleDialogueStateChanged;
                CoreLogger.LogDebug("[PlayerController] DialogueService 이벤트 구독 해제.");
            }

            if (_sceneTransitionService != null)
            {
                _sceneTransitionService.OnTransitionStateChanged -= HandleTransitionStateChanged;
                CoreLogger.LogDebug("[PlayerController] SceneTransitionService 이벤트 구독 해제.");
            }
        }

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) CoreLogger.LogError("PlayerController: Rigidbody2D 컴포넌트를 찾을 수 없습니다!", this);

            animator = GetComponent<Animator>();
            if (animator == null) CoreLogger.LogError("PlayerController: Animator 컴포넌트를 찾을 수 없습니다!", this);

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) CoreLogger.LogError("PlayerController: SpriteRenderer 컴포넌트를 찾을 수 없습니다!", this);

            // Start 시점에 서비스 주입이 완료되었으므로, 이제 안전하게 이벤트를 구독합니다.
            if (_dialogueService != null)
            {
                _dialogueService.OnDialogueStateChanged += HandleDialogueStateChanged;
                CoreLogger.LogDebug("[PlayerController] DialogueService 이벤트 구독.");
            }
            else
            {
                CoreLogger.LogWarning("[PlayerController] _dialogueService가 null입니다. Dialogue 이벤트 구독을 건너뜜.");
            }

            if (_sceneTransitionService != null)
            {
                _sceneTransitionService.OnTransitionStateChanged += HandleTransitionStateChanged;
                CoreLogger.LogDebug("[PlayerController] SceneTransitionService 이벤트 구독.");
            }
            else
            {
                CoreLogger.LogWarning("[PlayerController] _sceneTransitionService가 null입니다. SceneTransition 이벤트 구독을 건너뜜.");
            }

            // 초기 상태 설정
            UpdateControlState();
            UpdateAnimationParameters(); // 초기 애니메이션 상태 설정
        }

        private void HandleDialogueStateChanged(bool isDialogueActive)
        {
            UpdateControlState(); // 대화 상태 변경 시 제어 상태 업데이트
            if (isDialogueActive) // 대화 시작 시
            {
                StopPlayerMovementAndAnimation();
            }
            else // 대화 종료 시
            {
                StartCoroutine(LockInteractionForBufferTime()); // 상호작용 버퍼 시간 적용
            }
        }

        private void HandleTransitionStateChanged(bool isTransitioning)
        {
            UpdateControlState(); // 씬 전환 상태 변경 시 제어 상태 업데이트
            if (isTransitioning) // 씬 전환 시작 시
            {
                StopPlayerMovementAndAnimation();
            }
        }

        private void StopPlayerMovementAndAnimation()
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetBool("isWalking", false);
        }

        /// <summary>
        /// 플레이어의 전반적인 제어 가능 상태를 업데이트하는 통합 메서드.
        /// 외부 요인(대화, 씬 전환) 및 내부 요인(상호작용 버퍼)을 고려하여 _canMove, _canInteract 상태를 결정합니다.
        /// </summary>
        private void UpdateControlState()
        {
            bool isBlockedByDialogue = _dialogueService != null && _dialogueService.IsDialogueActive();
            bool isBlockedByTransition = _sceneTransitionService != null && _sceneTransitionService.IsTransitioning;

            // 대화 중이거나 씬 전환 중이면 이동 및 상호작용 불가능
            bool isGloballyBlocked = isBlockedByDialogue || isBlockedByTransition;

            _canMove = !isGloballyBlocked;
            _canInteract = !isGloballyBlocked && !_isInteractionLockedByBuffer;

            // 이동이 불가능해지면 즉시 플레이어를 멈춤
            if (!_canMove)
            {
                StopPlayerMovementAndAnimation();
            }
            CoreLogger.LogDebug($"[PlayerController] Control State Updated: CanMove={_canMove}, CanInteract={_canInteract}", this);
        }

        void Update()
        {
            HandleMovementInput();
            HandleInteractionInput();
            UpdateAnimationParameters();
        }

        private void HandleMovementInput()
        {
            if (_canMove)
            {
                float horizontalInput = Input.GetAxisRaw("Horizontal");
                float verticalInput = Input.GetAxisRaw("Vertical");
                Vector2 currentMoveInput = new Vector2(horizontalInput, verticalInput).normalized;

                if (currentMoveInput != Vector2.zero)
                {
                    moveDirection = currentMoveInput; // 실제 이동 방향
                    _lastNonZeroMoveDirection = currentMoveInput; // 마지막으로 바라본 방향 업데이트
                }
                else
                {
                    moveDirection = Vector2.zero; // 입력이 없으면 이동 정지
                }
            }
            else
            {
                moveDirection = Vector2.zero; // 제어 불가능 상태에서는 이동 입력 무시 및 강제 정지
            }
        }

        private void HandleInteractionInput()
        {
            if (_canInteract && Input.GetKeyDown(interactionKey))
            {
                TryInteract();
            }
        }

        void FixedUpdate()
        {
            if (rb != null)
            {
                rb.linearVelocity = moveDirection * moveSpeed;
            }
        }

        /// <summary>
        /// 애니메이터 파라미터를 업데이트합니다.
        /// </summary>
        void UpdateAnimationParameters()
        {
            if (animator == null || spriteRenderer == null) return;

            bool isMoving = moveDirection.magnitude > 0.1f; // 실제 이동 입력이 있는지 확인

            animator.SetBool("isWalking", isMoving);

            if (isMoving)
            {
                animator.SetFloat("moveX", Mathf.Abs(_lastNonZeroMoveDirection.x));
                animator.SetFloat("moveY", _lastNonZeroMoveDirection.y);

                // SpriteRenderer flipX는 _lastNonZeroMoveDirection 기준으로 업데이트
                if (_lastNonZeroMoveDirection.x < 0)
                {
                    spriteRenderer.flipX = true;
                }
                else if (_lastNonZeroMoveDirection.x > 0)
                {
                    spriteRenderer.flipX = false;
                }
            }
            // 이동이 중단된 경우에도 캐릭터는 마지막 바라보는 방향을 유지해야 하므로, flipX는 여기서 변경하지 않습니다.
        }

        /// <summary>
        /// 주변 상호작용 오브젝트를 찾고 상호작용을 시도합니다.
        /// </summary>
        private void TryInteract()
        {
            if (!_canInteract)
            {
                CoreLogger.LogDebug("Interaction currently blocked. (_canInteract is false)", this);
                return;
            }

            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionDistance);

            var nearestInteractable = colliders
                .Select(c => c.GetComponent<IInteractable>())
                .Where(i => i != null)
                .OfType<MonoBehaviour>()
                .Where(m => {
                    Vector2 directionToObject = ((Vector2)m.transform.position - (Vector2)transform.position).normalized;
                    float dotProduct = Vector2.Dot(_lastNonZeroMoveDirection, directionToObject);
                    return dotProduct > interactionDotProductThreshold;
                })
                .OrderBy(m => Vector2.Distance(transform.position, m.transform.position))
                .Select(m => m.GetComponent<IInteractable>())
                .FirstOrDefault();

            if (nearestInteractable != null)
            {
                CoreLogger.LogDebug($"<color=green>Nearest interactable found: {(nearestInteractable as MonoBehaviour)?.gameObject.name}. Calling Interact()...</color>", this);
                nearestInteractable.Interact();
                StartCoroutine(LockInteractionForBufferTime());
            }
            else
            {
                CoreLogger.LogDebug("주변에 상호작용할 수 있는 것이 없습니다.", this);
            }
        }

        /// <summary>
        /// 일정 시간 동안 상호작용을 잠그는 코루틴.
        /// </summary>
        private IEnumerator LockInteractionForBufferTime()
        {
            _isInteractionLockedByBuffer = true;
            UpdateControlState();
            yield return new WaitForSeconds(interactionBufferTime);
            _isInteractionLockedByBuffer = false;
            UpdateControlState();
        }

        // 디버그 시각화 (유니티 에디터 전용)
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                // 플레이 중이 아닐 때는 _lastNonZeroMoveDirection이 제대로 초기화되지 않을 수 있으므로,
                // 기본적으로 (0,-1) 방향으로 가정하거나, 편집기에서 값을 지정할 수 있도록 할 수 있습니다.
            }

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