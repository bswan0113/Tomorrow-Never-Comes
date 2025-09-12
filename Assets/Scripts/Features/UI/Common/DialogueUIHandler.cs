using System.Collections;
using System.Collections.Generic;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
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

        // 필드 주입 방식 사용 (생성자 주입과 함께 사용하지 않음)
        [Inject] private IDialogueService _dialogueService;

        void Awake()
        {
            gameObject.SetActive(false);
        }

        // Start 메서드에서 주입 확인
        void Start()
        {
            if (_dialogueService == null)
            {
                CoreLogger.LogError("DialogueUIHandler: IDialogueService가 주입되지 않았습니다.");
            }
            else
            {
                CoreLogger.Log("[DialogueUIHandler] IDialogueService 주입 확인됨.");
            }
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
            }
            m_TypingCoroutine = StartCoroutine(TypeDialogueCoroutine(dialogue));
        }

        private IEnumerator TypeDialogueCoroutine(string textToShow)
        {
            IsTyping = true;
            dialogueText.text = "";

            foreach (char letter in textToShow.ToCharArray())
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(typingSpeed);
            }

            IsTyping = false;
            m_TypingCoroutine = null;
        }

        /// <summary>
        /// 타이핑 효과를 즉시 완료시키는 스킵 메서드
        /// </summary>
        public void SkipTypingEffect()
        {
            if (m_TypingCoroutine != null)
            {
                StopCoroutine(m_TypingCoroutine);
                m_TypingCoroutine = null;
            }
            dialogueText.text = m_FullText;
            IsTyping = false;
        }

        /// <summary>
        /// 선택지 목록을 받아 화면에 버튼들을 생성합니다.
        /// </summary>
        public void ShowChoices(List<ChoiceData> choices)
        {
            if (_dialogueService == null)
            {
                CoreLogger.LogError("DialogueUIHandler: IDialogueService가 주입되지 않았습니다.");
                return;
            }

            if (IsTyping)
            {
                SkipTypingEffect();
            }

            dialogueText.gameObject.SetActive(false);
            choiceBox.SetActive(true);

            foreach (Transform child in choiceBox.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (var choice in choices)
            {
                GameObject buttonGO = Instantiate(choiceButtonPrefab, choiceBox.transform);
                buttonGO.GetComponentInChildren<TextMeshProUGUI>().text = choice.choiceText;
                buttonGO.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _dialogueService.ProcessChoice(choice);
                });
            }
        }

        // UI 전체 켜고 끄기
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}