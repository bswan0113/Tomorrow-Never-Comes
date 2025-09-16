// C:\Workspace\Tomorrow Never Comes\Core\LifetimeScope\Parent\DatabaseCleanup.cs (수정)

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
        private readonly IDatabaseAccess _databaseAccess;

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
            // IDatabaseAccess를 IDisposable로 캐스팅하여 Dispose 호출 (모든 스레드 연결 해제)
            if (_databaseAccess is IDisposable disposableDbAccess)
            {
                disposableDbAccess.Dispose();
            }
            else
            {
                CoreLogger.LogWarning("[DatabaseCleanup] IDatabaseAccess does not implement IDisposable. Connection might not be fully closed.");
            }
        }

        // IDisposable 구현: 컨테이너 종료 시점에 이벤트 구독 해제
        public void Dispose()
        {
            CoreLogger.Log("[DatabaseCleanup] Disposing DatabaseCleanup: Unsubscribing from Application.quitting.");
            Application.quitting -= OnApplicationQuit;
            // OnApplicationQuit에서 이미 _databaseAccess.Dispose()를 호출했으므로 여기서는 중복 호출하지 않습니다.
        }
    }
}