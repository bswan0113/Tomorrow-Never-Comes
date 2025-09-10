using UnityEngine;

namespace Features.World
{
    public interface IInteractionObjectFactory
    {
        // InteractionObjectData를 받아 InteractionObject를 생성하고 초기화합니다.
        // position과 parent는 선택 사항으로, 월드 위치와 계층 구조를 지정합니다.
        InteractionObject Create(InteractionObjectData data, Vector3 position, Transform parent = null);
    }
}