// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerDataManager.cs

using System;
using Core.Data.Interface;
using Core.Interface;
using UnityEngine;

namespace Features.Player
{
    public class PlayerDataManager : MonoBehaviour,IPlayerService // IPlayerService 인터페이스 구현 추가
    {
        // public static PlayerDataManager Instance { get; private set; } // <- 제거

        // 의존성 주입을 위한 필드
        private IDataService _dataService;

        // IPlayerService 인터페이스 구현을 위한 프로퍼티와 이벤트
        public PlayerStatsData StatsData { get; private set; } // IPlayerService에 추가 필요
        public event Action OnPlayerStatusChanged; // IPlayerService에 추가 필요 (static 제거)



        // 컴포지션 루트에서 호출될 초기화 함수 (IDataService를 주입받음)
        public void Initialize(IDataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            LoadPlayerData(); // DataManager가 준비된 후 호출됩니다.
            Debug.Log($"<color=lightblue>[PlayerDataManager] 초기화 완료. 현재 지능: {StatsData.Intellect}</color>");
        }

        public void Initialize()
        {
            LoadPlayerData(); // DataManager가 준비된 후 호출됩니다.
        }


        /// <summary>
        /// DataManager를 통해 데이터베이스에서 모든 플레이어 정보를 로드합니다.
        /// </summary>
        public void LoadPlayerData()
        {
            if (_dataService == null)
            {
                Debug.LogError("PlayerDataManager: IDataService가 초기화되지 않았습니다. Initialize()를 먼저 호출해주세요.");
                return;
            }

            if (_dataService.HasSaveData) // 주입받은 _dataService 사용
            {
                var data = _dataService.LoadData("PlayerStats", "SaveSlotID", 1); // 주입받은 _dataService 사용
                if (data != null && data.Count > 0)
                {
                    var row = data[0];
                    StatsData = new PlayerStatsData
                    {
                        Intellect = Convert.ToInt32(row["Intellect"]),
                        Charm = Convert.ToInt32(row["Charm"]),
                        Endurance = Convert.ToInt32(row["Endurance"]),
                        Money = (long)row["Money"],
                        HeroineALiked = Convert.ToInt32(row["HeroineALiked"]),
                        HeroineBLiked = Convert.ToInt32(row["HeroineBLiked"]),
                        HeroineCLiked = Convert.ToInt32(row["HeroineCLiked"]),
                    };
                    Debug.Log("플레이어 데이터를 DB에서 로드했습니다.");
                }
                else
                {
                    Debug.LogError("세이브 데이터 플래그는 있으나, PlayerStats 테이블에서 데이터를 가져오지 못했습니다. 새 데이터로 시작합니다.");
                    InitializeNewPlayerData();
                }
            }
            else
            {
                Debug.Log("세이브 데이터가 없습니다. 새로운 플레이어 데이터를 생성합니다.");
                InitializeNewPlayerData();
            }
        }

        /// <summary>
        /// 새 게임을 위한 기본 스탯으로 PlayerData를 초기화하고 DB에 저장합니다.
        /// </summary>
        private void InitializeNewPlayerData()
        {
            if (_dataService == null)
            {
                Debug.LogError("PlayerDataManager: IDataService가 초기화되지 않았습니다. Initialize()를 먼저 호출해주세요.");
                return;
            }
            StatsData = new PlayerStatsData();
            _dataService.InsertData( // <- SaveData 대신 InsertData로 변경
                "PlayerStats",
                new string[] { "SaveSlotID", "Intellect", "Charm", "Endurance", "Money", "HeroineALiked", "HeroineBLiked", "HeroineCLiked" },
                new object[] { 1, StatsData.Intellect, StatsData.Charm, StatsData.Endurance, StatsData.Money, StatsData.HeroineALiked, StatsData.HeroineBLiked, StatsData.HeroineCLiked }
            );
        }

        /// <summary>
        /// 현재 스탯을 DB에 저장(UPDATE)합니다. (IPlayerService 인터페이스 메서드 구현)
        /// </summary>
        public void SavePlayerData() // IPlayerService에 이 메서드가 정의되어 있어야 함
        {
            if (_dataService == null)
            {
                Debug.LogError("PlayerDataManager: IDataService가 초기화되지 않았습니다. Initialize()를 먼저 호출해주세요.");
                return;
            }
            if (StatsData == null) return;

            _dataService.UpdateData( // 주입받은 _dataService 사용
                "PlayerStats",
                new string[] { "Intellect", "Charm", "Endurance", "Money", "HeroineALiked", "HeroineBLiked", "HeroineCLiked" },
                new object[] { StatsData.Intellect, StatsData.Charm, StatsData.Endurance, StatsData.Money, StatsData.HeroineALiked, StatsData.HeroineBLiked, StatsData.HeroineCLiked },
                "SaveSlotID",
                1
            );
            Debug.Log("플레이어 데이터를 DB에 저장(업데이트)했습니다.");
        }

        // --- 외부에서 스탯을 안전하게 변경하기 위한 메서드들 ---

        public void AddIntellect(int amount)
        {
            StatsData.Intellect += amount;
            NotifyStatusChanged();
            Debug.Log($"지능 스탯 {amount} 증가! 현재 지능: {StatsData.Intellect}");
        }

        public void AddCharm(int amount)
        {
            StatsData.Charm += amount;
            NotifyStatusChanged();
        }

        // ... (다른 스탯 변경 메서드들도 필요에 따라 추가) ...

        /// <summary>
        /// 스탯 변경이 있음을 게임 전체에 알립니다. (UI 업데이트 등)
        /// </summary>
        private void NotifyStatusChanged()
        {
            // OnPlayerStatusChanged?.Invoke(); // 이제 static이 아니므로 직접 호출
            OnPlayerStatusChanged?.Invoke(); // 수정: static 이벤트가 아니므로, this.OnPlayerStatusChanged?.Invoke() 가 더 명확하지만, 그냥 OnPlayerStatusChanged?.Invoke()도 동일하게 동작.
        }
    }
}