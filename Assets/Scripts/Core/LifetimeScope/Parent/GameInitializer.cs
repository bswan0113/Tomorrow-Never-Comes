using System;
using Core.Logging;
using Core.Resource;
using UnityEngine;

namespace Core.LifetimeScope.Parent
{
    using System.Threading;
using VContainer.Unity;
using Cysharp.Threading.Tasks; // UniTask 사용을 위해 추가 (패키지 설치 필수!)

namespace Core.LifetimeScope.Parent
{
    public class GameInitializer : IAsyncStartable
    {
        private readonly GameManager _gameManager;
        private readonly GameResourceManager _gameResourceManager; // GameResourceManager 주입

        public GameInitializer(GameManager gameManager, GameResourceManager gameResourceManager)
        {
            _gameManager = gameManager;
            _gameResourceManager = gameResourceManager;
            CoreLogger.LogInfo("[GameInitializer] 생성자 호출. 의존성 주입 완료.", null);
        }

        // IAsyncStartable의 비동기 Start 메서드
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            CoreLogger.LogInfo("[GameInitializer] IAsyncStartable.StartAsync() 호출. 비동기 초기화 시작.", null);

            try
            {
                // GameResourceManager 비동기 초기화 대기
                CoreLogger.LogInfo("[GameInitializer] GameResourceManager.InitializeAsync() 호출 시도...", null);
                await _gameResourceManager.InitializeAsync();
                CoreLogger.LogInfo("[GameInitializer] GameResourceManager.InitializeAsync() 호출 완료.", null);

                // ResourceManager 초기화가 성공했으므로 이제 GameManager 시작
                if (_gameManager != null)
                {
                    CoreLogger.LogInfo("[GameInitializer] GameManager.StartGame() 호출 시도...", null);
                    _gameManager.StartGame();
                    CoreLogger.LogInfo("[GameInitializer] GameManager.StartGame() 호출 완료. 부팅 성공.", null);
                }
                else
                {
                    CoreLogger.LogCritical("[GameInitializer] CRITICAL: StartAsync() 시점에 GameManager가 null입니다. 부팅 시퀀스 실패.", null);
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogCritical($"[GameInitializer] CRITICAL: 게임 초기화 중 치명적인 예외 발생! 부팅 실패. \nException: {ex.Message}\n{ex.StackTrace}", null);
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Break();
                #endif
            }

            CoreLogger.LogInfo("[GameInitializer] 비동기 초기화 종료.", null);
        }
    }
}
}