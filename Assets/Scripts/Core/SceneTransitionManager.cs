// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionManager.cs (REFACTORED)

using System;
using System.Threading;
using Core.Interface;
using Core.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Core
{
    // MonoBehaviour 제거, IInitializable 구현 (초기 페이드인을 위해)
    public class SceneTransitionManager : ISceneTransitionService, IInitializable
    {
        private readonly SceneTransitionView _view; // 씬의 View를 주입받음
        private readonly float _fadeDuration = 1f;

        private bool _isTransitioning = false;
        public bool IsTransitioning => _isTransitioning;

        public string CurrentSceneName => SceneManager.GetActiveScene().name;

        public event Action<bool> OnTransitionStateChanged;
        public event Action<float> OnLoadingProgress;

        [Inject]
        public SceneTransitionManager(SceneTransitionView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            // 필요하다면 설정값(fadeDuration)도 DI를 통해 주입받을 수 있습니다.
        }

        // VContainer가 모든 주입 후 호출. 게임 시작 시 초기 페이드인을 담당합니다.
        public void Initialize()
        {
            if (_view.FadeImage != null)
            {
                _view.FadeImage.color = new Color(0, 0, 0, 1);
                // Initialize는 동기 메서드이므로, 비동기 Fade는 Forget()으로 처리합니다.
                // 이는 게임 로직의 일부이므로 IAsyncStartable 대신 IInitializable에서 시작해도 좋습니다.
                FadeAsync(0f).Forget();
            }
        }

        public void FadeAndLoadScene(string sceneName)
        {
            if (_isTransitioning)
            {
                CoreLogger.LogWarning($"Scene transition already in progress. Ignoring request to load '{sceneName}'.");
                return;
            }
            // 비동기 작업을 시작하고 호출자는 기다리지 않음 (Fire-and-forget)
            FadeAndLoadSceneAsync(sceneName).Forget();
        }

        private async UniTask FadeAndLoadSceneAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            _isTransitioning = true;
            OnTransitionStateChanged?.Invoke(true);
            CoreLogger.LogInfo($"[SceneTransitionManager] Scene transition started for '{sceneName}'.");

            bool transitionFailed = false;

            try
            {
                // --- Fade Out ---
                await FadeAsync(1f, cancellationToken);
                CoreLogger.LogDebug("[SceneTransitionManager] Fade Out complete.");

                // --- Addressables를 이용한 Scene Load ---
                var loadSceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single, true);

                // Progress를 업데이트하면서 비동기 로딩을 기다림
                await loadSceneHandle.ToUniTask(
                    Progress.Create<float>(p => OnLoadingProgress?.Invoke(p)),
                    cancellationToken: cancellationToken
                );

                CoreLogger.LogInfo($"[SceneTransitionManager] Scene '{sceneName}' loaded and activated successfully. New active scene: {CurrentSceneName}");

                // --- Fade In ---
                await FadeAsync(0f, cancellationToken);
                CoreLogger.LogDebug("[SceneTransitionManager] Fade In complete.");
            }
            catch (OperationCanceledException)
            {
                CoreLogger.LogWarning($"[SceneTransitionManager] Scene transition for '{sceneName}' was cancelled.");
                transitionFailed = true;
            }
            catch (Exception ex)
            {
                CoreLogger.LogError($"[SceneTransitionManager] Failed to load scene '{sceneName}'. Exception: {ex.Message}");
                transitionFailed = true;
            }
            finally
            {
                _isTransitioning = false;
                OnTransitionStateChanged?.Invoke(false);
                CoreLogger.LogInfo($"[SceneTransitionManager] Scene transition for '{sceneName}' finished (status: {(transitionFailed ? "Failed" : "Completed")}).");
                // 핸들 해제는 Addressables 정책에 따라 결정 (보통 자동으로 관리됨)
            }
        }

        private async UniTask FadeAsync(float targetAlpha, CancellationToken cancellationToken = default)
        {
            if (_view.FadeImage == null)
            {
                CoreLogger.LogError("FadeImage is null. Cannot perform fade.");
                return;
            }

            var fadeImage = _view.FadeImage;
            float startAlpha = fadeImage.color.a;
            float time = 0f;

            while (time < _fadeDuration)
            {
                // 매 프레임마다 취소 요청을 확인
                cancellationToken.ThrowIfCancellationRequested();

                time += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / _fadeDuration);
                fadeImage.color = new Color(0, 0, 0, alpha);

                // 다음 프레임까지 기다림
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            fadeImage.color = new Color(0, 0, 0, targetAlpha);
        }
    }
}