// C:\Workspace\Tomorrow Never Comes\Core\LifetimeScope\Parent\GameLifetimeScope.cs (REFACTORED)

using System.IO;
using Core.Data;
using Core.Data.Impl;
using Core.Data.Interface;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Logging;
using Core.Resource;
using Core.Util;
using Features.Data;
using Features.Player;
using Features.UI.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;
// using Core.Util; // GameProgressSerializer 등의 클래스 실제 네임스페이스를 확인하고 필요에 따라 유지 또는 제거

namespace Core.LifetimeScope.Parent
{
    public class GameLifetimeScope : VContainer.Unity.LifetimeScope
    {
        [Header("Core Components")]
        [SerializeField] private SceneTransitionManager sceneTransitionManager;
        [SerializeField] private GameResourceManager gameResourceManager;
        // [SerializeField] private DataManager dataManager; // DataManager는 VContainer를 통해 Resolve됩니다.
        [SerializeField] private PlayerDataManager playerDataManager;

        [Header("UI Components")]
        [SerializeField] private DialogueManager dialogueManager;

        protected override void Configure(IContainerBuilder builder)
        {
            DontDestroyOnLoad(gameObject);

            // --- 1. 핵심 관리자 및 서비스 등록 (VContainer가 관리할 GameObject 컴포넌트)
            builder.RegisterComponent(gameResourceManager).As<IGameResourceService>();
            builder.RegisterComponent(sceneTransitionManager).As<ISceneTransitionService>();
            builder.RegisterComponent(dialogueManager).As<IDialogueService>();

            // --- 2. 스키마 관리자 등록 (C# 클래스)
            // SchemaManager는 데이터베이스 스키마 정의를 관리하며, DatabaseAccess에 주입됩니다.
            // DatabaseAccess의 생성자에서 SchemaManager.InitializeSchema()를 호출하도록 변경되었습니다.
            builder.Register<SchemaManager>(Lifetime.Singleton);

            // --- 3. 데이터베이스 접근 계층 등록 (C# 클래스)
            string dbPath = Path.Combine(Application.persistentDataPath, "PlayerSaveData.db");

            builder.Register<DatabaseAccess>(Lifetime.Singleton)
                .As<IDatabaseAccess>() // IDatabaseAccess 인터페이스로 등록 (유지)
                .AsSelf()              // DatabaseAccess 타입으로 직접 Resolve할 필요가 있다면 추가 (유지)
                // .As<IInitializable>() // DatabaseAccess는 이제 시작 시점에 연결을 열 필요가 없으므로 IInitializable 등록 제거
                .WithParameter("dbPath", dbPath); // 경로 매개변수 (유지)

            // --- 4. Serializer 및 Repository 등록
            // IDataSerializer<GameProgressData>는 구체적인 GameProgressSerializer를 사용합니다.
            // IGameProgressRepository는 GameProgressRepository를 구현합니다.
            builder.Register<IDataSerializer<GameProgressData>, GameProgressSerializer>(Lifetime.Singleton);
            builder.Register<IGameProgressRepository, GameProgressRepository>(Lifetime.Singleton);

            // IDataSerializer<PlayerStatsData>는 구체적인 PlayerStatsSerializer를 사용합니다.
            // IPlayerStatsRepository는 PlayerStatsRepository를 구현합니다.
            builder.Register<IDataSerializer<PlayerStatsData>, PlayerStatsSerializer>(Lifetime.Singleton);
            builder.Register<IPlayerStatsRepository, PlayerStatsRepository>(Lifetime.Singleton);

            // --- 5. DataManager 등록 (MonoBehaviour 컴포넌트)
            builder.Register<DataManager>(Lifetime.Singleton)
                   .AsImplementedInterfaces() // IDataService를 구현하는 DataManager
                   .AsSelf();

            // --- 6. PlayerDataManager 등록 (MonoBehaviour 컴포넌트)
            builder.RegisterComponent(playerDataManager)
                .As<IPlayerService>();

            // --- 7. 이전의 RegisterBuildCallback을 통한 수동 초기화 제거 (잘 하셨습니다)

            // --- 8. GameManager 등록 (C# 클래스)
            builder.Register<GameManager>(Lifetime.Singleton)
                   .AsImplementedInterfaces()
                   .AsSelf();

            // --- 9. 엔트리 포인트 등록
            builder.RegisterEntryPoint<DatabaseCleanup>();
            builder.RegisterEntryPoint<GameInitializer>();

            CoreLogger.Log("[GameLifetimeScope] VContainer configuration complete. Manual initialization callbacks removed.");
        }
    }
}