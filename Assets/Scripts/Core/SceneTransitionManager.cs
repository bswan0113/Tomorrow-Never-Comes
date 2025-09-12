// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionManager.cs

using System; // Action 델리게이트를 사용하기 위해 추가
using System.Collections;
using Core.Interface;
using Core.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core
{
    public class SceneTransitionManager : MonoBehaviour, ISceneTransitionService
    {
        [SerializeField] private Image fadeImage;
        [SerializeField] private float fadeDuration = 1f;

        private bool _isTransitioning = false; // 씬 전환 중인지 여부를 나타내는 플래그
        public bool IsTransitioning => _isTransitioning; // ISceneTransitionService 구현
        public event Action<bool> OnTransitionStateChanged; // ISceneTransitionService 구현

        // 게임 시작 시 Fade In
        private void Start()
        {
            if (fadeImage == null)
            {
                CoreLogger.LogWarning("SceneTransitionManager: fadeImage가 할당되지 않았습니다. 페이드 효과가 작동하지 않습니다.");
                // 에디터에서 실수로 할당하지 않은 경우를 대비하여 기본 색상 설정
                if (fadeImage == null && TryGetComponent(out Image img))
                {
                    fadeImage = img;
                    fadeImage.color = new Color(0, 0, 0, 1); // 시작 시 검은색으로 가정
                }
            }

            if (fadeImage != null)
            {
                // 초기 상태를 검은색으로 설정하고, 페이드 인 시작
                fadeImage.color = new Color(0, 0, 0, 1);
                StartCoroutine(Fade(0f)); // 투명하게 만들기
            }
        }

        public void FadeAndLoadScene(string sceneName)
        {
            if (_isTransitioning)
            {
                CoreLogger.LogWarning($"Scene transition already in progress. Ignoring request to load '{sceneName}'.");
                return;
            }

            // 씬 유효성 검사
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                CoreLogger.LogError($"Scene '{sceneName}' cannot be loaded because it is not in the build settings or does not exist.");
                // 유효하지 않은 씬 요청에 대한 복구 경로 (여기서는 단순히 경고 후 중단)
                return;
            }

            StartCoroutine(FadeAndLoadCoroutine(sceneName));
        }

        private IEnumerator FadeAndLoadCoroutine(string sceneName)
        {
            _isTransitioning = true;
            OnTransitionStateChanged?.Invoke(true); // 씬 전환 시작 이벤트 발생

            // Fade Out
            yield return StartCoroutine(Fade(1f));

            // Scene Load (비동기 로드로 변경)
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

            // 로딩이 완료될 때까지 대기
            while (!asyncLoad.isDone)
            {
                // 로딩 진행률 표시 등의 추가 로직을 여기에 넣을 수 있습니다.
                yield return null;
            }

            // 로드 실패 처리 (예외 발생 시 asyncLoad.isDone은 true가 되므로, 추가적인 오류 검사가 필요할 수 있지만,
            // CanStreamedLevelBeLoaded로 대부분의 경우를 막았으므로 여기서는 단순화)
            // 만약 로드 중 치명적인 오류가 발생했다면 Unity 자체에서 에러를 발생시키므로,
            // 여기서는 asyncLoad.isDone이 true가 되지 않거나 예상치 못한 동작을 할 경우를 대비하는 코드가 필요할 수 있습니다.
            // 하지만 LoadSceneAsync는 보통 성공적으로 isDone을 반환합니다.

            // Fade In (새 씬이 로드된 후)
            if (fadeImage != null)
            {
                // 새 씬에서 Fade In이 시작되도록 설정 (새 씬 로드 후 이 Manager가 유지된다는 가정)
                // 만약 이 Manager가 새 씬에서 파괴되고 새로 생성된다면, 새 씬의 Manager가 Fade In을 담당해야 합니다.
                // 여기서는 DontDestroyOnLoad로 Manager가 유지된다고 가정합니다.
                yield return StartCoroutine(Fade(0f));
            }

            _isTransitioning = false;
            OnTransitionStateChanged?.Invoke(false); // 씬 전환 완료 이벤트 발생
        }

        private IEnumerator Fade(float targetAlpha)
        {
            if (fadeImage == null)
            {
                CoreLogger.LogError("FadeImage is null. Cannot perform fade.");
                yield break;
            }

            float startAlpha = fadeImage.color.a;
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime; // 씬 로드 중 시간 스케일이 0이 될 수 있으므로 unscaledDeltaTime 사용
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            fadeImage.color = new Color(0, 0, 0, targetAlpha);
        }
    }
}