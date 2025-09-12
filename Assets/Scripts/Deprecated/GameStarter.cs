// // C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\LifetimeScope\Parent\GameStarter.cs
//
// using Core.Logging; // CoreLogger 사용을 위해 추가
// using UnityEngine;
// using VContainer.Unity; // IStartable 사용을 위해 추가
//
// namespace Core.LifetimeScope.Parent
// {
//     /// <summary>
//     /// VContainer의 모든 의존성 주입이 완료된 후 게임을 시작하는 엔트리 포인트입니다.
//     /// IStartable 인터페이스를 구현하여 VContainer가 자동으로 Start()를 호출하도록 합니다.
//     /// </summary>
//     public class GameStarter : IStartable // MonoBehaviour 대신 IStartable을 구현 (만약 GameObject가 필요하다면 MonoBehaviour 유지)
//     {
//         private readonly GameManager _gameManager;
//
//         // 생성자 주입은 VContainer에 의해 자동으로 처리됨
//         public GameStarter(GameManager gameManager)
//         {
//             _gameManager = gameManager;
//             CoreLogger.LogInfo("[GameStarter] 생성자 호출. GameManager 주입 시도...");
//
//             // (P3 부팅 실패 조기 탐지)
//             if (_gameManager == null)
//             {
//                 // 이 로그는 VContainer가 주입에 실패하면 생성자 호출 자체가 실패하므로 실제로는 거의 발생하지 않지만,
//                 // 만약 VContainer 설정이 Optional 주입 등으로 변경될 경우를 대비한 방어 코드입니다.
//                 CoreLogger.LogCritical("[GameStarter] CRITICAL: GameManager 주입 실패! 게임을 시작할 수 없습니다.");
//             }
//         }
//
//         // IStartable 인터페이스의 Start 메서드. 모든 주입이 완료된 후 VContainer가 호출합니다.
//         public void Start()
//         {
//             CoreLogger.LogInfo("[GameStarter] IStartable.Start() 호출. 모든 의존성 주입 완료.", null);
//
//             try
//             {
//                 if (_gameManager != null)
//                 {
//                     CoreLogger.LogInfo("[GameStarter] GameManager.StartGame() 호출 시도...", null);
//                     _gameManager.StartGame();
//                     CoreLogger.LogInfo("[GameStarter] GameManager.StartGame() 호출 완료. 부팅 성공.", null);
//                 }
//                 else
//                 {
//                     // 이 경우는 생성자에서 이미 크리티컬 로그를 남겼어야 합니다.
//                     CoreLogger.LogCritical("[GameStarter] CRITICAL: Start() 시점에 GameManager가 null입니다. 부팅 시퀀스 실패.", null);
//                 }
//             }
//             catch (System.Exception ex)
//             {
//                 // (P3 부팅 실패 조기 탐지)
//                 // StartGame() 실행 중 발생한 모든 예외를 포착하여 크리티컬 로그로 남깁니다.
//                 CoreLogger.LogCritical($"[GameStarter] CRITICAL: GameManager.StartGame() 실행 중 치명적인 예외 발생! 부팅 실패. \nException: {ex.Message}\n{ex.StackTrace}", null);
//
//                 // 개발 빌드나 에디터에서는 게임을 멈추는 것이 좋습니다.
//                 #if UNITY_EDITOR || DEVELOPMENT_BUILD
//                 Debug.Break(); // 에디터에서 실행을 일시 중지시킴
//                 #endif
//             }
//         }
//     }
// }