// C:\Workspace\Tomorrow Never Comes\Core\LifetimeScope\Parent\GameInitializer.cs (REFACTORED)

using System;
using System.Threading;
using Core.Data; // *** ADDED: SchemaManager 사용을 위해 추가 ***
using Core.Data.Interface;
using Core.Interface; // *** ADDED: IDatabaseAccess 사용을 위해 추가 ***
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
        private readonly IPlayerService _playerService;
        private readonly IDataService _dataService;

        // *** CHANGED: SchemaManager와 IDatabaseAccess 의존성 주입 추가 ***
        public GameInitializer(
            GameManager gameManager,
            GameResourceManager gameResourceManager,
            SchemaManager schemaManager,
            IDatabaseAccess databaseAccess,
            IPlayerService playerService,
            IDataService dataService)
        {
            _gameManager = gameManager;
            _gameResourceManager = gameResourceManager;
            _schemaManager = schemaManager;
            _databaseAccess = databaseAccess;
            _playerService = playerService;
            _dataService = dataService;
            CoreLogger.LogInfo("[GameInitializer] 생성자 호출. 의존성 주입 완료.", null);
        }

        // IAsyncStartable의 비동기 Start 메서드
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            CoreLogger.LogInfo("[GameInitializer] 최종 초기화 시퀀스 시작.", null);

            try
            {
                // 1단계: 데이터베이스 스키마 초기화 (테이블 생성)
                CoreLogger.LogInfo("[GameInitializer] 1/5: 데이터베이스 스키마 초기화...", null);
                await _schemaManager.InitializeTablesAsync(_databaseAccess);

                // 2단계 (신규): 저장 데이터 존재 여부 확인
                // 테이블이 방금 생성되었으므로 이제 이 호출은 안전합니다.
                CoreLogger.LogInfo("[GameInitializer] 2/5: 저장 데이터 확인...", null);
                await _dataService.CheckSaveDataAsync();

                // 3단계: GameResourceManager 초기화 (ScriptableObject 로드)
                CoreLogger.LogInfo("[GameInitializer] 3/5: 게임 리소스 초기화...", null);
                await _gameResourceManager.InitializeAsync();

                // 4단계: PlayerDataManager 데이터 로드
                CoreLogger.LogInfo("[GameInitializer] 4/5: 플레이어 데이터 로드...", null);
                await _playerService.LoadPlayerDataAsync();

                // 5단계: GameManager 게임 로직 시작
                if (_gameManager != null)
                {
                    CoreLogger.LogInfo("[GameInitializer] 5/5: 게임 매니저 시작...", null);
                    await _gameManager.StartGame();
                    CoreLogger.LogInfo("===== 게임 부팅 성공 =====", null);
                }
                else
                {
                    throw new InvalidOperationException("GameManager가 null입니다.");
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogCritical($"[GameInitializer] CRITICAL: 게임 초기화 중 치명적인 예외 발생! 부팅 실패. \nException: {ex.Message}\n{ex.StackTrace}", null);
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Break();
                #endif
            }
        }
    }
}