// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\UI\Common\DialogueSystemUpdater.cs (REVISED)

using UnityEngine;
using VContainer;
using Core.Interface; // IDialogueUIHandler를 위해 추가
using Core.Logging;

namespace Features.UI.Common
{
    public class DialogueSystemUpdater : MonoBehaviour
    {
        private DialogueManager _dialogueManager;
        private IObjectResolver _resolver; // 의존성을 직접 꺼내 쓸 수 있는 리졸버

        // 생성자 주입 시점에서는 DialogueManager만 받습니다.
        [Inject]
        public void Construct(DialogueManager dialogueManager, IObjectResolver resolver)
        {
            _dialogueManager = dialogueManager;
            _resolver = resolver;

            // 코루틴 실행자는 이 시점에 등록해도 안전합니다.
            _dialogueManager.SetCoroutineRunner(this);
        }

        // Start는 씬의 모든 오브젝트가 Awake를 마친 후에 호출됩니다.
        // PlayerRoomLifetimeScope에서 DialogueUIHandler가 등록된 이후 시점입니다.
        private void Start()
        {
            // 컨테이너에서 IDialogueUIHandler를 직접 꺼내옵니다.
            // 이 시점에는 자식 스코프에 등록된 핸들러를 찾을 수 있습니다.
            try
            {
                var uiHandler = _resolver.Resolve<IDialogueUIHandler>();
                _dialogueManager.RegisterDialogueUI(uiHandler);
            }
            catch (VContainerException ex)
            {
                // UI 핸들러가 없는 씬일 수도 있으므로, 오류 대신 경고를 기록합니다.
                CoreLogger.LogWarning($"[DialogueSystemUpdater] Could not resolve IDialogueUIHandler in this scene. Dialogue will not be available. Error: {ex.Message}", this);
            }
        }

        private void Update()
        {
            _dialogueManager.OnUpdate();
        }
    }
}