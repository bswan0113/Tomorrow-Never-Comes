// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionManager.cs

using System;
using System.Collections;
using Core.Interface;
using Core.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement; // SceneManager 사용을 위해 추가
using UnityEngine.UI;

namespace Core
{
    public class SceneTransitionManager : MonoBehaviour, ISceneTransitionService
    {
        [SerializeField] private Image fadeImage;
        [SerializeField] private float fadeDuration = 1f;

        private bool _isTransitioning = false;
        public bool IsTransitioning => _isTransitioning;

        // P26: CurrentSceneName 인터페이스 구현
        /// <summary>
        /// 현재 활성화된 씬의 이름을 반환합니다.
        /// </summary>
        public string CurrentSceneName => SceneManager.GetActiveScene().name; // SceneManager를 통해 현재 씬 이름 가져오기

        public event Action<bool> OnTransitionStateChanged;
        public event Action<float> OnLoadingProgress;

        private void Start()
        {
            InitializeFadeImage();
            if (fadeImage != null)
            {
                fadeImage.color = new Color(0, 0, 0, 1);
                StartCoroutine(Fade(0f));
            }
        }

        private void OnDestroy()
        {
            // 이 매니저는 DontDestroyOnLoad 될 가능성이 높으므로, OnDestroy는 게임 종료 시점에 가깝습니다.
            // 만약 동적으로 생성/파괴된다면, 이벤트 구독자를 여기서 초기화하는 것을 고려할 수 있습니다.
            // OnTransitionStateChanged = null;
            // OnLoadingProgress = null;
        }

        private void InitializeFadeImage()
        {
            if (fadeImage == null)
            {
                CoreLogger.LogWarning("SceneTransitionManager: fadeImage가 할당되지 않았습니다. GameObject에서 Image 컴포넌트를 찾아봅니다.", this);
                if (TryGetComponent(out Image img))
                {
                    fadeImage = img;
                    CoreLogger.LogInfo("SceneTransitionManager: Image 컴포넌트를 찾아 fadeImage로 할당했습니다.", this);
                }
                else
                {
                    CoreLogger.LogError("SceneTransitionManager: GameObject에 Image 컴포넌트가 없으며, fadeImage가 할당되지 않았습니다. 페이드 효과가 작동하지 않습니다.", this);
                }
            }
        }

        public void FadeAndLoadScene(string sceneName)
        {
            if (_isTransitioning)
            {
                CoreLogger.LogWarning($"Scene transition already in progress. Ignoring request to load '{sceneName}'.", this);
                return;
            }

            StartCoroutine(FadeAndLoadCoroutine(sceneName));
        }

        private IEnumerator FadeAndLoadCoroutine(string sceneName)
        {
            _isTransitioning = true;
            OnTransitionStateChanged?.Invoke(true);
            CoreLogger.LogInfo($"[SceneTransitionManager] Scene transition started for '{sceneName}'. Current Scene (before load): {CurrentSceneName}", this);

            // 전환 실패 여부를 추적하는 플래그
            bool transitionFailed = false;

            // --- Fade Out ---
            yield return StartCoroutine(Fade(1f));
            CoreLogger.LogDebug("[SceneTransitionManager] Fade Out complete.", this);

            // --- Addressables를 이용한 Scene Load ---
            // activateOnLoad: true로 변경하여 로드 완료 시 씬이 즉시 활성화되도록 합니다.
            AsyncOperationHandle<SceneInstance> loadSceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single, true);

            while (!loadSceneHandle.IsDone)
            {
                OnLoadingProgress?.Invoke(loadSceneHandle.PercentComplete);
                yield return null;
            }

            if (loadSceneHandle.Status == AsyncOperationStatus.Failed)
            {
                CoreLogger.LogError($"[SceneTransitionManager] Failed to load scene '{sceneName}' using Addressables. Exception: {loadSceneHandle.OperationException?.Message}", this);
                transitionFailed = true;
            }
            else
            {
                CoreLogger.LogInfo($"[SceneTransitionManager] Scene '{sceneName}' loaded and activated successfully. New active scene: {CurrentSceneName}", this);
            }

            // --- Fade In (로드 성공 시에만) ---
            if (!transitionFailed && fadeImage != null)
            {
                yield return StartCoroutine(Fade(0f));
                CoreLogger.LogDebug("[SceneTransitionManager] Fade In complete.", this);
            }

            // --- 클린업 ---
            _isTransitioning = false;
            OnTransitionStateChanged?.Invoke(false);
            CoreLogger.LogInfo($"[SceneTransitionManager] Scene transition for '{sceneName}' finished (status: {(transitionFailed ? "Failed" : "Completed")}). Final active scene: {CurrentSceneName}", this);

            // Addressables 핸들 해제에 대한 원래 주석은 그대로 유지합니다.
            // Addressables.Release(loadSceneHandle); // 필요한 경우 핸들 해제
        }

        private IEnumerator Fade(float targetAlpha)
        {
            if (fadeImage == null)
            {
                CoreLogger.LogError("FadeImage is null. Cannot perform fade.", this);
                yield break;
            }

            float startAlpha = fadeImage.color.a;
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            fadeImage.color = new Color(0, 0, 0, targetAlpha);
        }
    }
}