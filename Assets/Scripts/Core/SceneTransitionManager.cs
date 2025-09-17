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
using Cysharp.Threading.Tasks; // UniTask 사용
using VContainer.Unity;

namespace Core
{
    public class SceneTransitionManager : ISceneTransitionService, IInitializable
    {
        private readonly SceneTransitionView _view;
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
        }

        public void Initialize()
        {
            if (_view.FadeImage != null)
            {
                _view.FadeImage.color = new Color(0, 0, 0, 1);
                FadeAsync(0f).Forget();
            }
        }

        /// <summary>
        /// 페이드 효과와 함께 씬을 비동기적으로 로드합니다. 이 작업이 완료될 때까지 기다릴 수 있습니다.
        /// </summary>
        /// <param name="sceneName">로드할 씬의 이름.</param>
        /// <returns>씬 전환 작업이 완료되면 끝나는 UniTask.</returns>
        public async UniTask FadeAndLoadScene(string sceneName) // void -> async UniTask로 변경
        {
            if (_isTransitioning)
            {
                CoreLogger.LogWarning($"Scene transition already in progress. Ignoring request to load '{sceneName}'.");
                return;
            }
            // .Forget() 대신 await를 사용하여 내부 비동기 작업이 끝날 때까지 기다립니다.
            await FadeAndLoadSceneAsync(sceneName);
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
                cancellationToken.ThrowIfCancellationRequested();

                time += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / _fadeDuration);
                fadeImage.color = new Color(0, 0, 0, alpha);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            fadeImage.color = new Color(0, 0, 0, targetAlpha);
        }
    }
}