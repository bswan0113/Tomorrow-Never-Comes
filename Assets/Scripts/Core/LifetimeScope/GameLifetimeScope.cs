using Core.Interface;
using Core.Interface.Core.Interface;
using Manager;

namespace Core
{
    using VContainer;
    using VContainer.Unity;
    using UnityEngine;
    using System.IO;

public class GameLifetimeScope : LifetimeScope
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

        // 1. DatabaseAccess 등록
        string dbPath = Path.Combine(Application.persistentDataPath, "PlayerSaveData.db");
        builder.Register<IDatabaseAccess>(container => {
            var dbAccess = new DatabaseAccess(dbPath);
            dbAccess.OpenConnection();
            return dbAccess;
        }, Lifetime.Singleton);

        // 어플리케이션 종료 시 DB 연결 닫기
        builder.RegisterEntryPoint<DatabaseCleanup>();

        // 2. DataManager 등록
        builder.RegisterComponent(dataManager).As<IDataService>();
        builder.RegisterBuildCallback(container => {
            var dbAccess = container.Resolve<IDatabaseAccess>();
            dataManager.Initialize(dbAccess);
        });

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

        // 8. EntryPoint 등록 (게임 시작)
        builder.RegisterEntryPoint<GameStarter>();
    }
}
}