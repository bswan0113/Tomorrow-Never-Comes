// C:\Workspace\Tomorrow Never Comes\Core\LifetimeScope\Parent\GameInitializer.cs (REFACTORED)

using System;
using System.Threading;
using Core.Data; // *** ADDED: SchemaManager 사용을 위해 추가 ***
using Core.Data.Interface; // *** ADDED: IDatabaseAccess 사용을 위해 추가 ***
using Core.Logging;
using Core.Resource;
using UnityEngine;
using VContainer.Unity;
using Cysharp.Threading.Tasks;

namespace Core.LifetimeScope.Parent
{
    public class GameInitializer : IAsyncStartable
    {
        private readonly GameManager _gameManager;
        private readonly GameResourceManager _gameResourceManager;
        private readonly SchemaManager _schemaManager; // *** ADDED ***
        private readonly IDatabaseAccess _databaseAccess; // *** ADDED ***

        // *** CHANGED: SchemaManager와 IDatabaseAccess 의존성 주입 추가 ***
        public GameInitializer(
            GameManager gameManager,
            GameResourceManager gameResourceManager,
            SchemaManager schemaManager,
            IDatabaseAccess databaseAccess)
        {
            _gameManager = gameManager;
            _gameResourceManager = gameResourceManager;
            _schemaManager = schemaManager;
            _databaseAccess = databaseAccess;
            CoreLogger.LogInfo("[GameInitializer] 생성자 호출. 의존성 주입 완료.", null);
        }

        // IAsyncStartable의 비동기 Start 메서드
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            CoreLogger.LogInfo("[GameInitializer] IAsyncStartable.StartAsync() 호출. 비동기 초기화 시작.", null);

            try
            {
                // *** NEW STEP 1: 데이터베이스 스키마 초기화 ***
                // 게임의 다른 부분이 데이터베이스를 사용하기 전에 테이블이 준비되도록 합니다.
                CoreLogger.LogInfo("[GameInitializer] SchemaManager.InitializeTablesAsync() 호출 시도...", null);
                await _schemaManager.InitializeTablesAsync(_databaseAccess); // Task를 await하면 UniTask로 자동 변환됩니다.
                CoreLogger.LogInfo("[GameInitializer] SchemaManager.InitializeTablesAsync() 호출 완료. 데이터베이스 준비 완료.", null);

                // *** STEP 2: GameResourceManager 비동기 초기화 대기 ***
                CoreLogger.LogInfo("[GameInitializer] GameResourceManager.InitializeAsync() 호출 시도...", null);
                await _gameResourceManager.InitializeAsync();
                CoreLogger.LogInfo("[GameInitializer] GameResourceManager.InitializeAsync() 호출 완료.", null);

                // *** STEP 3: 모든 초기화가 성공했으므로 이제 GameManager 시작 ***
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