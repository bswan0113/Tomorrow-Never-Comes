// Scripts/Core/Interface/IPlayerService.cs
using System;
using Core.Interface.Core.Interface; // Action 이벤트를 위해 필요

namespace Core.Interface // DataManager 인터페이스와 동일한 네임스페이스에 있다면
{
    public interface IPlayerService
    {

        void Initialize(IDataService dataService);
        void Initialize();
        void SavePlayerData();
        PlayerStatus Status { get; } // 플레이어 데이터 접근을 위한 프로퍼티
        event Action OnPlayerStatusChanged; // 스탯 변경 이벤트
        // 기타 AddIntellect 등 플레이어 스탯 변경 메서드도 필요하다면 여기에 추가
        void AddIntellect(int amount);
        void AddCharm(int amount);
    }
}