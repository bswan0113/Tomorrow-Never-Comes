using System;
using Core.Data.Interface;
using UnityEngine;

namespace Core.LifetimeScope.Parent
{
    public class DatabaseCleanup : IDisposable
    {
        private readonly IDatabaseAccess _databaseAccess;

        public DatabaseCleanup(IDatabaseAccess databaseAccess)
        {
            _databaseAccess = databaseAccess;

            // 어플리케이션 종료 시 DB 연결 닫기
            Application.quitting += OnApplicationQuit;
        }

        private void OnApplicationQuit()
        {
            _databaseAccess.CloseConnection();
            Debug.Log("Database connection closed");
        }

        public void Dispose()
        {
            Application.quitting -= OnApplicationQuit;
        }
    }
}