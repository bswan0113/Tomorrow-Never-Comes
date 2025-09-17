using System.Collections;
using System.Collections.Generic;
using Core.Interface;
using Core.Logging;
using ScriptableObjects.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Features.UI.Common
{
    /// <summary>
    /// 씬에 존재하는 대화 UI 오브젝트들을 직접 제어하고, DialogueManager에 자신을 등록하는 역할을 합니다.
    /// </summary>
    public class DialogueUIHandler : MonoBehaviour, IDialogueUIHandler
    {
        [Header("UI 기본 컴포넌트")]
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Header("선택지 관련")]
        [SerializeField] private GameObject choiceBox;
        [SerializeField] private GameObject choiceButtonPrefab;

        [Header("타이핑 효과")]
        [SerializeField] private float typingSpeed = 0.05f;

        private Coroutine m_TypingCoroutine;
        private string m_FullText;
        public bool IsTyping { get; private set; } = false;

        // 필드 주입 방식 사용 (VContainer에 의해 주입)
        [Inject] private IDialogueService _dialogueService;

        void Awake()
        {
            gameObject.SetActive(false);
        }

        void Start()
        {
            // VContainer를 사용하고 필드에 [Inject]가 붙어 있다면,
            // _dialogueService는 Awake() 전에 주입되거나, 최소한 Start() 시점에는 주입되어 있어야 합니다.
            // 따라서 이 null 체크는 VContainer 설정 오류 시의 방어 코드이며, 일반적으로는 불필요합니다.
            // 성공 로그는 자주 발생하여 노이즈가 될 수 있으므로 제거하고, 에러 발생 시에만 로그를 남깁니다.
            if (_dialogueService == null)
            {
                CoreLogger.LogError("DialogueUIHandler: IDialogueService가 주입되지 않았습니다. VContainer 설정 오류를 확인하세요.", this);
            }
            // else
            // {
            //     CoreLogger.Log("[DialogueUIHandler] IDialogueService 주입 확인됨.");
            // }
        }

        private void OnDestroy()
        {
            // GameObject가 파괴될 때 모든 코루틴을 중지하여 잠재적인 메모리 누수 방지
            StopAllCoroutines();
            m_TypingCoroutine = null; // 코루틴 참조 해제
        }

        /// <summary>
        /// 대사 한 줄의 정보를 받아 화면에 표시합니다.
        /// </summary>
        public void ShowLine(string speakerName, string dialogue)
        {
            choiceBox.SetActive(false);
            dialogueText.gameObject.SetActive(true);

            bool isMonologue = string.IsNullOrEmpty(speakerName);
            speakerNameText.gameObject.SetActive(!isMonologue);
            speakerNameText.text = speakerName;

            m_FullText = dialogue;

            if (m_TypingCoroutine != null)
            {
                StopCoroutine(m_TypingCoroutine);
                m_TypingCoroutine = null; // 코루틴 중지 후 참조 해제
            }
            m_TypingCoroutine = StartCoroutine(TypeDialogueCoroutine(dialogue));
        }

        private IEnumerator TypeDialogueCoroutine(string textToShow)
        {
            IsTyping = true;
            dialogueText.text = "";

            foreach (char letter in textToShow.ToCharArray())
            {
                // 코루틴 중간에 게임 오브젝트가 파괴될 수 있으므로, 방어 코드 추가 (선택 사항)
                if (this == null) yield break;

                dialogueText.text += letter;
                yield return new WaitForSeconds(typingSpeed);
            }

            IsTyping = false;
            m_TypingCoroutine = null; // 코루틴 완료 후 참조 해제
        }

        /// <summary>
        /// 타이핑 효과를 즉시 완료시키는 스킵 메서드
        /// </summary>
        public void SkipTypingEffect()
        {
            if (m_TypingCoroutine != null)
            {
                StopCoroutine(m_TypingCoroutine);
                m_TypingCoroutine = null; // 코루틴 중지 후 참조 해제
            }
            if (dialogueText != null) // 혹시 모를 경우를 대비한 null 체크
            {
                dialogueText.text = m_FullText;
            }
            IsTyping = false;
        }

        /// <summary>
        /// 선택지 목록을 받아 화면에 버튼들을 생성합니다.
        /// </summary>
        public void ShowChoices(List<ChoiceData> choices)
        {
            // IDialogueService가 [Inject] 되었으므로, Start()에서 null 체크가 실패하지 않았다면 여기에선 항상 유효하다고 가정합니다.
            // 만약 여전히 null일 가능성이 있다면, 이는 VContainer 설정 문제를 나타냅니다.
            if (_dialogueService == null)
            {
                CoreLogger.LogError("DialogueUIHandler: IDialogueService가 유효하지 않아 선택지를 처리할 수 없습니다.", this);
                return;
            }

            if (IsTyping)
            {
                SkipTypingEffect();
            }

            dialogueText.gameObject.SetActive(false);
            choiceBox.SetActive(true);

            // 기존 선택지 버튼들을 모두 제거
            foreach (Transform child in choiceBox.transform)
            {
                Destroy(child.gameObject);
            }

            if (choiceButtonPrefab == null)
            {
                CoreLogger.LogError("DialogueUIHandler: 선택지 버튼 프리팹(choiceButtonPrefab)이 할당되지 않았습니다.", this);
                return;
            }

            foreach (var choice in choices)
            {
                if (choice == null)
                {
                    CoreLogger.LogWarning("DialogueUIHandler: 선택지 목록에 null 항목이 있습니다. 건너뜜.", this);
                    continue;
                }

                GameObject buttonGO = Instantiate(choiceButtonPrefab, choiceBox.transform);

                // TextMeshProUGUI 컴포넌트 확인 및 텍스트 설정
                TextMeshProUGUI buttonText = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = choice.choiceText;
                }
                else
                {
                    CoreLogger.LogWarning($"DialogueUIHandler: 선택지 버튼 프리팹 '{choiceButtonPrefab.name}'에 TextMeshProUGUI 컴포넌트가 없습니다.", this);
                }

                // Button 컴포넌트 확인 및 리스너 추가
                Button button = buttonGO.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        // 선택 처리 후 바로 UI를 숨기거나 비활성화하는 로직은 DialogueManager가 담당하는 것이 더 적절합니다.
                        // 여기서는 선택 이벤트를 DialogueService에 전달하는 역할만 수행합니다.
                        _dialogueService.ProcessChoice(choice);
                    });
                }
                else
                {
                    CoreLogger.LogWarning($"DialogueUIHandler: 선택지 버튼 프리팹 '{choiceButtonPrefab.name}'에 Button 컴포넌트가 없습니다. 버튼이 동작하지 않을 수 있습니다.", this);
                    // Button 컴포넌트가 없으면 동작하지 않으므로, 이 경우 버튼을 파괴하거나 오류 처리 필요
                    // Destroy(buttonGO); // 완전히 제거하는 것도 고려해볼 수 있습니다.
                }
            }
        }

        // UI 전체 켜고 끄기
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}