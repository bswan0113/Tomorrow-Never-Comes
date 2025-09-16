// C:\Workspace\Tomorrow Never Comes\Core\LifetimeScope\Parent\GameLifetimeScope.cs

using System.IO;
using Core.Data;
using Core.Data.Impl;
using Core.Data.Interface;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.LifetimeScope.Parent.Core.LifetimeScope.Parent;
using Core.Logging;
using Core.Resource;
using Features.Data;
using Features.Player;
using Features.UI.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Util;

namespace Core.LifetimeScope.Parent
{
    public class GameLifetimeScope : VContainer.Unity.LifetimeScope
    {
        [Header("Core Components")]
        [SerializeField] private SceneTransitionManager sceneTransitionManager;
        [SerializeField] private GameResourceManager gameResourceManager;
        // [SerializeField] private DataManager dataManager;
        [SerializeField] private PlayerDataManager playerDataManager;

        [Header("UI Components")]
        [SerializeField] private DialogueManager dialogueManager;

        protected override void Configure(IContainerBuilder builder)
        {
            DontDestroyOnLoad(gameObject);

            // --- 1. 핵심 관리자 및 서비스 등록 (VContainer가 관리할 GameObject 컴포넌트)
            builder.RegisterComponent(gameResourceManager).As<IGameResourceService>().AsSelf();
            builder.RegisterComponent(sceneTransitionManager).As<ISceneTransitionService>();
            builder.RegisterComponent(dialogueManager).As<IDialogueService>();

            // --- 2. 스키마 관리자 등록 (C# 클래스)
            builder.Register<SchemaManager>(Lifetime.Singleton);

            // --- 3. 데이터베이스 접근 계층 등록 (C# 클래스)
            string dbPath = Path.Combine(Application.persistentDataPath, "PlayerSaveData.db");

            builder.Register<DatabaseAccess>(Lifetime.Singleton)
                .As<IDatabaseAccess>() // 명시적으로 IDatabaseAccess로 등록 (이것은 유지)
                .AsSelf() // DatabaseAccess 타입으로 직접 Resolve할 필요가 있다면 추가
                .As<IInitializable>() // DatabaseAccess가 IInitializable을 구현하므로 명시적으로 등록
                .WithParameter("dbPath", dbPath); // 경로 매개변수


            // --- 4. Serializer 및 Repository 등록
            builder.Register<IDataSerializer<GameProgressData>, GameProgressSerializer>(Lifetime.Singleton);
            builder.Register<IGameProgressRepository, GameProgressRepository>(Lifetime.Singleton);

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