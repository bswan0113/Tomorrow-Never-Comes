namespace Features.World
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "InteractionObjectData", menuName = "GameData/InteractionObject Data")]
    public class InteractionObjectData : ScriptableObject
    {
        [Header("프리팹 연결")]
        [Tooltip("이 데이터에 연결될 InteractionObject 프리팹 (InteractionObject 컴포넌트 필수)")]
        public GameObject InteractionObjectPrefab;

        [Header("행동력 비용")]
        [Tooltip("이 상호작용에 필요한 행동력. 0이면 비용 없음.")]
        public int actionPointCost = 0;

        [Header("이벤트 연결")]
        [Tooltip("위에서부터 순서대로 조건을 검사하여, 가장 먼저 모든 조건을 만족하는 이벤트 하나만 실행됩니다.")]
        public List<ConditionalEvent> conditionalEvents = new List<ConditionalEvent>();

        [Tooltip("만족하는 조건이 하나도 없을 경우 실행될 기본 시퀀서입니다.")]
        public ActionSequencer defaultEvent;

        [Tooltip("행동력이 부족할 때 실행될 시퀀서입니다.")]
        public ActionSequencer onInteractionFailure;

        // TODO: 이외에도 InteractionObject가 가질 수 있는 모든 데이터를 여기에 정의합니다.
        // 예를 들어, 초기화 시 필요한 메시지, 고유 ID 등.
    }
}