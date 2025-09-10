using System.IO;
using Core.Data;
using Core.Data.Impl;
using Core.Data.Interface;
using Core.Interface;
using Core.Interface.Core.Interface;
using Core.Resource;
using Features.Player;
using Features.UI.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.LifetimeScope.Parent
{
    public class GameLifetimeScope : VContainer.Unity.LifetimeScope
{
    [Header("Core Components")]
    [SerializeField] private SceneTransitionManager sceneTransitionManager;
    [SerializeField] private GameResourceManager gameResourceManager;
    [SerializeField] private DataManager dataManager;
    [SerializeField] private PlayerDataManager playerDataManager;

    [Header("UI Components")]
    [SerializeField] private DialogueManager dialogueManager;
    protected override void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        base.Awake();
    }
    protected override void Configure(IContainerBuilder builder)
    {

        builder.Register<SchemaManager>(Lifetime.Singleton);
        // 1. DatabaseAccess 등록
        string dbPath = Path.Combine(Application.persistentDataPath, "PlayerSaveData.db");
        builder.Register<IDatabaseAccess>(container => {
            // DatabaseAccess가 SchemaManager에 의존하므로, VContainer에서 SchemaManager 인스턴스를 가져와 전달합니다.
            var schemaManager = container.Resolve<SchemaManager>();
            var dbAccess = new DatabaseAccess(dbPath, schemaManager);
            return dbAccess;
        }, Lifetime.Singleton);



        builder.RegisterComponentInHierarchy<DataManager>()
            .AsImplementedInterfaces()
            .AsSelf()
            .WithParameter<IDatabaseAccess>(container => container.Resolve<IDatabaseAccess>());

        // 3. GameResourceManager 등록
        builder.RegisterComponent(gameResourceManager).As<IGameResourceService>();
        builder.RegisterBuildCallback(container => {
            gameResourceManager.Initialize();
        });

        // 4. SceneTransitionManager 등록
        builder.RegisterComponent(sceneTransitionManager).As<ISceneTransitionService>();

        // 5. PlayerDataManager 등록
        builder.RegisterComponent(playerDataManager).As<IPlayerService>();

        builder.RegisterBuildCallback(container => {
            var dataService = container.Resolve<IDataService>();
            playerDataManager.Initialize(dataService);
        });

        // 6. GameManager 등록
        builder.Register<GameManager>(Lifetime.Singleton)
               .AsImplementedInterfaces()
               .AsSelf();

        // 7. DialogueManager 등록
        builder.RegisterComponent(dialogueManager).As<IDialogueService>();
        builder.RegisterBuildCallback(container => {
            var gameResourceService = container.Resolve<IGameResourceService>();
            var gameManager = container.Resolve<GameManager>();
            dialogueManager.Initialize(gameResourceService, gameManager);
        });

        builder.RegisterEntryPoint<DatabaseCleanup>();
        // 8. EntryPoint 등록 (게임 시작)
        builder.RegisterEntryPoint<GameStarter>();
    }
}
}