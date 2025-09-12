using System.Collections.Generic;
using Core.Logging;
using UnityEngine;
using VContainer;
using VContainer.Unity; // IObjectResolver를 위해 필요

namespace Features.World
{
    public class DefaultInteractionObjectFactory : IInteractionObjectFactory
    {
        private readonly IObjectResolver _resolver; // VContainer의 Resolver를 주입받음

        [Inject]
        public DefaultInteractionObjectFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
            CoreLogger.Log("DefaultInteractionObjectFactory 초기화 완료.");
        }

        public InteractionObject Create(InteractionObjectData data, Vector3 position, Transform parent = null)
        {
            if (data == null)
            {
                CoreLogger.LogError("InteractionObjectData가 null입니다. InteractionObject를 생성할 수 없습니다.");
                return null;
            }
            if (data.InteractionObjectPrefab == null)
            {
                CoreLogger.LogError($"InteractionObjectData '{data.name}'에 연결된 프리팹이 없습니다. InteractionObject를 생성할 수 없습니다.");
                return null;
            }

            // 1. 프리팹 인스턴스화
            // VContainer.Unity의 GameObject.InstantiateAndInject를 사용하면 더 간편합니다.
            // 하지만 수동으로 Inject를 호출하는 방식도 유효합니다.
            GameObject instanceGO = GameObject.Instantiate(data.InteractionObjectPrefab, position, Quaternion.identity, parent);
            InteractionObject interactionObject = instanceGO.GetComponent<InteractionObject>();

            if (interactionObject == null)
            {
                CoreLogger.LogError($"프리팹 '{data.InteractionObjectPrefab.name}'에 InteractionObject 컴포넌트가 없습니다. 생성 실패.");
                GameObject.Destroy(instanceGO);
                return null;
            }

            // 2. VContainer를 사용하여 인스턴스화된 MonoBehaviour에 [Inject] 메서드 호출 (Construct)
            // VContainer는 MonoBehaviour 인스턴스에 대한 의존성 주입을 자동으로 처리하지 않으므로,
            // 팩토리에서 명시적으로 resolver.Inject()를 호출하여 Construct 메서드를 트리거해야 합니다.
            _resolver.Inject(interactionObject);

            // 3. InteractionObjectData를 기반으로 InteractionObject 초기화
            // ConditionalEvent 리스트는 레퍼런스 복사를 방지하기 위해 새 리스트로 만듭니다.
            interactionObject.actionPointCost = data.actionPointCost;
            interactionObject.conditionalEvents = new List<ConditionalEvent>(data.conditionalEvents);
            interactionObject.defaultEvent = data.defaultEvent;
            interactionObject.onInteractionFailure = data.onInteractionFailure;

            // 추가적인 초기화 로직 (예: 오브젝트 이름 설정)
            instanceGO.name = $"InteractionObject_{data.name}_{System.Guid.NewGuid().ToString().Substring(0, 4)}";

            CoreLogger.Log($"InteractionObject '{instanceGO.name}' 생성 및 초기화 완료.");
            return interactionObject;
        }
    }
}