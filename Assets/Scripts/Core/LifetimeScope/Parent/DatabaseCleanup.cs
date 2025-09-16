// C:\Workspace\Tomorrow Never Comes\Core\LifetimeScope\Parent\DatabaseCleanup.cs (REFACTORED)

using System;
using Core.Data.Interface;
using Core.Logging;
using UnityEngine;
using VContainer.Unity; // IInitializable, IDisposable

namespace Core.LifetimeScope.Parent
{
    // DatabaseCleanup은 IInitializable을 구현하여 Application.quitting 구독
    // IDisposable을 구현하여 구독 해제 및 DatabaseAccess.Dispose 호출
    public class DatabaseCleanup : IInitializable, IDisposable
    {
        private readonly IDatabaseAccess _databaseAccess; // IDatabaseAccess가 이제 IDisposable을 상속합니다.

        public DatabaseCleanup(IDatabaseAccess databaseAccess)
        {
            _databaseAccess = databaseAccess;
            // 생성자에서는 할당만 하고, 초기화는 Initialize에서 수행
        }

        // IInitializable 구현: 초기화 시점에 이벤트 구독
        public void Initialize()
        {
            CoreLogger.Log("[DatabaseCleanup] Initializing: Subscribing to Application.quitting.");
            Application.quitting += OnApplicationQuit;
        }

        private void OnApplicationQuit()
        {
            CoreLogger.Log("[DatabaseCleanup] Application is quitting. Disposing DatabaseAccess.");
            // IDatabaseAccess는 이제 IDisposable을 직접 상속하므로, 캐스팅 없이 Dispose 호출 가능.
            // DatabaseAccess의 Dispose 구현은 주로 풀링된 연결을 정리하거나,
            // 기타 장기 실행 리소스를 해제하는 역할을 할 수 있습니다.
            _databaseAccess.Dispose();
        }

        // IDisposable 구현: 컨테이너 종료 시점에 이벤트 구독 해제
        public void Dispose()
        {
            CoreLogger.Log("[DatabaseCleanup] Disposing DatabaseCleanup: Unsubscribing from Application.quitting.");
            Application.quitting -= OnApplicationQuit;
            // Unity 생명주기상 Application.quitting 이벤트는 VContainer가 Dispose를 호출하기 전에 발생합니다.
            // 따라서 OnApplicationQuit에서 이미 _databaseAccess.Dispose()를 호출했으므로 여기서는 중복 호출을 피합니다.
            // 만약 VContainer의 Dispose가 Application.ququitting보다 먼저 호출되는 예외적인 상황을 고려한다면,
            // 여기서도 _databaseAccess.Dispose()를 호출할 수 있습니다.
        }
    }
}