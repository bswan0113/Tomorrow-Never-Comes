// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionManager.cs

using System;
using System.Collections;
using Core.Interface;
using Core.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core
{
    public class SceneTransitionManager : MonoBehaviour, ISceneTransitionService
    {
        [SerializeField] private Image fadeImage;
        [SerializeField] private float fadeDuration = 1f;

        private bool _isTransitioning = false;
        public bool IsTransitioning => _isTransitioning;

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
            CoreLogger.LogInfo($"[SceneTransitionManager] Scene transition started for '{sceneName}'.", this);

            // 전환 실패 여부를 추적하는 플래그
            bool transitionFailed = false;

            // --- Fade Out ---
            // !!! 경고: 만약 StartCoroutine(Fade(1f)) 호출에서 동기적 예외가 발생하면,
            // 이 코루틴은 여기서 즉시 중단되고 아래의 클린업 코드는 실행되지 않습니다.
            // _isTransitioning 상태가 'true'로 고정될 수 있습니다.
            yield return StartCoroutine(Fade(1f));
            CoreLogger.LogDebug("[SceneTransitionManager] Fade Out complete.", this);

            // --- Addressables를 이용한 Scene Load ---
            AsyncOperationHandle<SceneInstance> loadSceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single, false);

            while (!loadSceneHandle.IsDone)
            {
                OnLoadingProgress?.Invoke(loadSceneHandle.PercentComplete);
                yield return null; // 이 지점에서 발생할 수 있는 동기적 예외(예: OnLoadingProgress 콜백 내부)에도 대비해야 합니다.
            }

            if (loadSceneHandle.Status == AsyncOperationStatus.Failed)
            {
                CoreLogger.LogError($"[SceneTransitionManager] Failed to load scene '{sceneName}' using Addressables. Exception: {loadSceneHandle.OperationException?.Message}", this);
                transitionFailed = true;
                // 로드 실패 시, 더 이상의 진행 없이 코루틴을 종료하려면 여기에 'yield break;'를 추가할 수 있습니다.
                // 'yield break;'를 사용하더라도 아래의 클린업 코드는 정상적으로 실행됩니다.
                // yield break;
            }
            else
            {
                CoreLogger.LogInfo($"[SceneTransitionManager] Scene '{sceneName}' loaded successfully.", this);
            }

            // --- Fade In (로드 성공 시에만) ---
            if (!transitionFailed && fadeImage != null)
            {
                // !!! 경고: StartCoroutine(Fade(0f)) 호출에서 동기적 예외가 발생하면,
                // 이 코루틴은 여기서 즉시 중단되고 아래의 클린업 코드는 실행되지 않습니다.
                // _isTransitioning 상태가 'true'로 고정될 수 있습니다.
                yield return StartCoroutine(Fade(0f));
                CoreLogger.LogDebug("[SceneTransitionManager] Fade In complete.", this);
            }

            // --- 클린업 (코루틴이 끝까지 실행되거나 'yield break'로 정상 종료될 경우 항상 실행됩니다.) ---
            // 단, 위에서 언급한 것처럼 중간에 *처리되지 않은 동기적 예외*로 인해 코루틴이 강제 종료되면 이 코드는 실행되지 않습니다.
            _isTransitioning = false;
            OnTransitionStateChanged?.Invoke(false);
            CoreLogger.LogInfo($"[SceneTransitionManager] Scene transition for '{sceneName}' finished (status: {(transitionFailed ? "Failed" : "Completed")}).", this);

            // Addressables 핸들 해제에 대한 원래 주석은 그대로 유지합니다.
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