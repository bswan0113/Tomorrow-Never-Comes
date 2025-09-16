// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionView.cs (REVISED)

using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// 씬에 존재하는 페이드 효과용 Image를 관리하고 노출하는 역할만 담당하는 MonoBehaviour 입니다.
    /// </summary>
    public class SceneTransitionView : MonoBehaviour
    {
        [Tooltip("페이드 효과에 사용될 UI Image 컴포넌트입니다.")]
        [SerializeField] private Image fadeImage;

        public Image FadeImage => fadeImage;

        private void Awake()
        {
            // 씬 전환 시 파괴되지 않도록 이 View가 속한 최상위 Canvas를 DontDestroyOnLoad로 만듭니다.
            // 이렇게 하면 Canvas 전체가 유지됩니다.
            var rootCanvas = GetComponentInParent<Canvas>(true)?.transform.root;
            if (rootCanvas != null)
            {
                DontDestroyOnLoad(rootCanvas.gameObject);
            }
            else
            {
                // 만약 최상위 Canvas를 찾지 못하면, 이 오브젝트만이라도 유지합니다.
                DontDestroyOnLoad(this.gameObject);
            }

            // fadeImage가 인스펙터에서 할당되지 않았다면, 이 게임오브젝트에서 직접 찾아봅니다.
            if (fadeImage == null)
            {
                fadeImage = GetComponent<Image>();
            }
            if (fadeImage == null)
            {
                Debug.LogError("SceneTransitionView: FadeImage가 할당되지 않았거나, 이 컴포넌트가 Image 컴포넌트와 다른 GameObject에 있습니다!");
            }
        }
    }
}