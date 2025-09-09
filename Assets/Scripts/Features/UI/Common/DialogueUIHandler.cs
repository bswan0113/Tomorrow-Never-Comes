// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueUIHandler.cs

using System.Collections; // ▼▼▼ 추가 ▼▼▼: 코루틴을 사용하기 위해 필요합니다.
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 씬에 존재하는 대화 UI 오브젝트들을 직접 제어하고, DialogueManager에 자신을 등록하는 역할을 합니다.
/// </summary>
public class DialogueUIHandler : MonoBehaviour
{
    [Header("UI 기본 컴포넌트")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("선택지 관련")]
    [SerializeField] private GameObject choiceBox;
    [SerializeField] private GameObject choiceButtonPrefab;

    // ▼▼▼ 추가 ▼▼▼: 타이핑 효과 관련 변수들
    [Header("타이핑 효과")]
    [SerializeField] private float typingSpeed = 0.05f; // 한 글자당 출력 시간

    private Coroutine m_TypingCoroutine; // 현재 실행 중인 타이핑 코루틴을 저장
    private string m_FullText; // 스킵 시 표시할 전체 텍스트 원본
    public bool IsTyping { get; private set; } = false; // 현재 타이핑 중인지 여부

    void Awake()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.RegisterDialogueUI(this);
        }
        gameObject.SetActive(false); // 등록 후에는 즉시 비활성화
    }

    /// <summary>
    /// 대사 한 줄의 정보를 받아 화면에 표시합니다.
    /// </summary>
    public void ShowLine(string speakerName, string dialogue)
    {
        // ▼▼▼ 수정 ▼▼▼: 기존 로직을 코루틴 시작 로직으로 변경
        choiceBox.SetActive(false);
        dialogueText.gameObject.SetActive(true);

        bool isMonologue = string.IsNullOrEmpty(speakerName);
        speakerNameText.gameObject.SetActive(!isMonologue);
        speakerNameText.text = speakerName;

        m_FullText = dialogue; // 전체 텍스트 저장

        // 만약 이전 코루틴이 실행 중이었다면 중지
        if (m_TypingCoroutine != null)
        {
            StopCoroutine(m_TypingCoroutine);
        }
        m_TypingCoroutine = StartCoroutine(TypeDialogueCoroutine(dialogue));
    }

    // ▼▼▼ 추가 ▼▼▼: 타이핑 효과를 처리하는 코루틴
    private IEnumerator TypeDialogueCoroutine(string textToShow)
    {
        IsTyping = true;
        dialogueText.text = ""; // 텍스트 초기화

        foreach (char letter in textToShow.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        IsTyping = false;
        m_TypingCoroutine = null;
    }

    // ▼▼▼ 추가 ▼▼▼: 타이핑 효과를 즉시 완료시키는 스킵 메서드
    public void SkipTypingEffect()
    {
        if (m_TypingCoroutine != null)
        {
            StopCoroutine(m_TypingCoroutine);
            m_TypingCoroutine = null;
        }
        dialogueText.text = m_FullText; // 전체 텍스트 즉시 표시
        IsTyping = false;
    }


    /// <summary>
    /// 선택지 목록을 받아 화면에 버튼들을 생성합니다.
    /// </summary>
    public void ShowChoices(List<ChoiceData> choices)
    {
        // ▼▼▼ 추가 ▼▼▼: 선택지가 표시될 때는 타이핑을 확실히 멈춤
        if (IsTyping)
        {
            SkipTypingEffect();
        }

        dialogueText.gameObject.SetActive(false); // 대사 텍스트는 잠시 가림
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
                DialogueManager.Instance.ProcessChoice(choice);
            });
        }
    }

    // UI 전체 켜고 끄기
    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}