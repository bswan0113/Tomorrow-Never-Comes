// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerController.cs

using UnityEngine;
using System.Linq;
using System.Collections; // ▼▼▼ 코루틴을 위해 추가 ▼▼▼

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Interaction")]
    public float interactionDistance = 1.5f;
    public KeyCode interactionKey = KeyCode.Space;
    [Tooltip("상호작용 후 다시 상호작용이 가능해지기까지의 짧은 지연 시간")]
    public float interactionBufferTime = 0.2f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveDirection;

    private bool canControl = true;
    private bool isInteractionLocked = false; // ▼▼▼ 추가: 입력 버퍼를 위한 플래그

    private void OnEnable()
    {
        DialogueManager.OnDialogueStateChanged += HandleDialogueStateChanged;
    }

    private void OnDisable()
    {
        DialogueManager.OnDialogueStateChanged -= HandleDialogueStateChanged;
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
            // ▼▼▼ 핵심 추가 ▼▼▼: 대화가 "끝났을 때" 입력 잠금을 시작
            StartCoroutine(LockInteractionForBufferTime());
        }
    }

    void Update()
    {


        if (!canControl) return;

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector2(horizontalInput, verticalInput).normalized;

        // ▼▼▼ 핵심 수정 ▼▼▼: 입력 잠금 상태가 아닐 때만 상호작용을 시도
        if (Input.GetKeyDown(interactionKey) && !isInteractionLocked)
        {
            Debug.Log($"[PlayerController.Update] canControl: {canControl}, isInteractionLocked: {isInteractionLocked}, IsDialogueActive: {DialogueManager.Instance?.IsDialogueActive()}");
            Debug.Log("<color=yellow>Interaction Key Pressed!</color>");
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
            // 블렌드 트리에 방향 값 전달
            animator.SetFloat("moveX", Mathf.Abs(moveDirection.x));
            animator.SetFloat("moveY", moveDirection.y);

            // 좌우 반전 로직
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


        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
        {
            return;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionDistance);
        var nearestInteractable = colliders.Select(c => c.GetComponent<IInteractable>()).Where(i => i != null).OfType<MonoBehaviour>().OrderBy(m => Vector2.Distance(transform.position, m.transform.position)).Select(m => m.GetComponent<IInteractable>()).FirstOrDefault();

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

    // ▼▼▼ 추가: 입력 버퍼를 처리하는 코루틴
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
    }
}