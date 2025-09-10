// // C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\GameBootstrapper.cs
// using UnityEngine;
// using Core.Interface; // 우리가 정의한 인터페이스들을 사용하기 위해 필요
// using Manager;       // GameResourceManager 네임스페이스가 필요할 수 있습니다.
// using System;
// using System.IO;
// using Core.Interface.Core.Interface; // DatabaseAccess 경로 설정을 위해 필요
//
// public class GameBootstrapper : MonoBehaviour
// {
//     [HideInInspector] // 인스펙터에 노출하지 않으려면 사용
//     public IGameService GameService { get; private set; }
//
//     [Header("Core Components")]
//     [SerializeField] private SceneTransitionManager _sceneTransitionManager; // Hierarchy에 있는 컴포넌트 참조
//     [SerializeField] private GameResourceManager _gameResourceManager;         // Hierarchy에 있는 컴포넌트 참조
//     [SerializeField] private DataManager _dataManager;                       // Hierarchy에 있는 컴포넌트 참조
//     [SerializeField] private PlayerDataManager _playerDataManager;           // Hierarchy에 있는 컴포넌트 참조
//
//     [Header("UI Components")]
//     [SerializeField] private DialogueManager _dialogueManager;
//
//     [HideInInspector] // 인스펙터에 노출하지 않으려면
//     public IDialogueService DialogueService { get; private set; }
//     // GameManager는 MonoBehaviour가 아니므로 필드로 직접 인스턴스를 저장합니다.
//     private GameManager _gameManager;
//
//     void Awake()
//     {
//         DontDestroyOnLoad(gameObject);
//         InitializeCoreServices();
//         GameService = _gameManager;
//     }
//
//     private void InitializeCoreServices()
//     {
//         Debug.Log("<color=lime>[GameBootstrapper] 핵심 서비스 초기화 시작...</color>");
//
//         // 1. IDatabaseAccess 인스턴스 생성
//         // DatabaseAccess는 MonoBehaviour가 아니므로 직접 new로 생성합니다.
//         string dbPath = Path.Combine(Application.persistentDataPath, "PlayerSaveData.db");
//         IDatabaseAccess databaseAccess = new DatabaseAccess(dbPath);
//         databaseAccess.OpenConnection(); // DB 연결을 여기서 엽니다.
//
//         // 어플리케이션 종료 시 DB 연결을 닫기 위한 처리 (옵션)
//         // Unity는 MonoBehaviour의 OnApplicationQuit을 호출해주므로,
//         // GameBootstrapper에서 이 책임을 맡을 수 있습니다.
//         Application.quitting += () => databaseAccess.CloseConnection();
//
//
//         // 2. DataManager 초기화 (IDatabaseAccess 주입)
//         // _dataManager (Hierarchy에서 참조된 MonoBehaviour)에 초기화 메서드를 호출
//         if (_dataManager == null) { Debug.LogError("DataManager is not assigned in GameBootstrapper!"); return; }
//         _dataManager.Initialize(databaseAccess);
//         IDataService dataService = _dataManager; // 인터페이스로 업캐스팅하여 사용
//
//
//         // 3. GameResourceManager 초기화 (의존성 없음)
//         if (_gameResourceManager == null) { Debug.LogError("GameResourceManager is not assigned in GameBootstrapper!"); return; }
//         _gameResourceManager.Initialize();
//         IGameResourceService gameResourceService = _gameResourceManager; // 인터페이스로 업캐스팅하여 사용
//
//
//         // 4. SceneTransitionManager 초기화 (의존성 없음)
//         if (_sceneTransitionManager == null) { Debug.LogError("SceneTransitionManager is not assigned in GameBootstrapper!"); return; }
//         // SceneTransitionManager는 Initialize 메서드가 없었으므로 Awake/Start에서 자체 초기화될 것입니다.
//         // 만약 필요하다면 ISceneTransitionService에 Initialize()를 추가하고 호출해야 합니다.
//         ISceneTransitionService sceneTransitionService = _sceneTransitionManager; // 인터페이스로 업캐스팅하여 사용
//
//
//         // 5. PlayerDataManager 초기화 (IDataService 주입)
//         if (_playerDataManager == null) { Debug.LogError("PlayerDataManager is not assigned in GameBootstrapper!"); return; }
//         _playerDataManager.Initialize(dataService); // IDataService 주입
//         IPlayerService playerService = _playerDataManager; // 인터페이스로 업캐스팅하여 사용
//
//
//         // 6. GameManager 인스턴스 생성 및 의존성 주입
//         // GameManager는 이제 MonoBehaviour가 아니므로 new로 생성합니다.
//         _gameManager = new GameManager(playerService, sceneTransitionService, gameResourceService, dataService);
//         // 아직 dialogueService가 없으므로, 초기화는 일단 이 컴포넌트를 참조할 수 있도록만 합니다.
//         // DialogueManager가 초기화된 후, 그 IDialogueService 인스턴스를 DialogueUIHandler에 주입해야 합니다.
//         // 이 부분은 순서를 조정하여 나중에 주입합니다.
//
//         // 8. DialogueManager 초기화 (IGameResourceService, IDialogueUIHandler 주입)
//         if (_dialogueManager == null) { Debug.LogError("DialogueManager is not assigned in GameBootstrapper!"); return; }
//         _dialogueManager.Initialize(gameResourceService,_gameManager);
//         IDialogueService dialogueService = _dialogueManager; // 인터페이스로 업캐스팅
//
//         // GameManager의 게임 시작 로직 호출
//         _gameManager = new GameManager(playerService, sceneTransitionService, gameResourceService, dataService);
//         _gameManager.StartGame();
//         GameService = _gameManager;
//         DialogueService = dialogueService;
//
//         Debug.Log("<color=lime>[GameBootstrapper] 핵심 서비스 초기화 완료!</color>");
//     }
// }