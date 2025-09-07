// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerDataManager.cs

using System; // Action 이벤트를 사용하기 위함
using System.Collections.Generic;
using UnityEngine;
// using Manager; // GameResourceManager는 이 스크립트에서 직접적으로 필요하지 않으므로 주석 처리하거나 삭제해도 됩니다.

public class PlayerDataManager : MonoBehaviour
{
    // --- 싱글턴 설정 ---
    public static PlayerDataManager Instance { get; private set; }

    // ▼▼▼ 추가 ▼▼▼: 실제 플레이어 데이터
    public PlayerStatus Status { get; private set; }

    // ▼▼▼ 추가 ▼▼▼: 스탯이 변경되었음을 알리는 이벤트
    public static event Action OnPlayerStatusChanged;


    void Awake()
    {
        // --- 싱글턴 인턴스 관리 ---
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // ▼▼▼ 추가 ▼▼▼: 게임 시작 직후 DataManager가 준비된 후 호출될 초기화 함수
    // GameManager나 다른 초기화 매니저에서 호출해주는 것이 이상적입니다.
    public void Initialize()
    {
        // 이 매니저가 깨어날 때, 스스로 DB에서 데이터를 로드하여 자신을 채웁니다.
        LoadPlayerData();
        Debug.Log($"<color=lightblue>[PlayerDataManager] 초기화 완료. 현재 지능: {Status.Intellect}</color>");
    }


    /// <summary>
    /// DataManager를 통해 데이터베이스에서 모든 플레이어 정보를 로드합니다.
    /// </summary>
    public void LoadPlayerData()
    {
        if (DataManager.Instance.HasSaveData)
        {
            // --- 저장된 데이터가 있을 경우 ---
            var data = DataManager.Instance.LoadData("PlayerStats", "SaveSlotID", 1);
            if (data != null && data.Count > 0)
            {
                var row = data[0]; // 첫 번째 행의 데이터를 가져옵니다.
                Status = new PlayerStatus
                {
                    // DB에서 읽어온 값은 object 타입이므로, 실제 타입으로 변환(casting)해줘야 합니다.
                    // SQLite의 INTEGER는 C#에서 long으로 취급되는 경우가 많으므로 (long)으로 변환 후 (int)로 다시 변환합니다.
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
                // HasSaveData는 true인데 실제 데이터 로드에 실패한 예외적인 경우
                Debug.LogError("세이브 데이터 플래그는 있으나, PlayerStats 테이블에서 데이터를 가져오지 못했습니다. 새 데이터로 시작합니다.");
                InitializeNewPlayerData();
            }
        }
        else
        {
            // --- 저장된 데이터가 없을 경우 (새 게임) ---
            Debug.Log("세이브 데이터가 없습니다. 새로운 플레이어 데이터를 생성합니다.");
            InitializeNewPlayerData();
        }
    }

    /// <summary>
    /// 새 게임을 위한 기본 스탯으로 PlayerData를 초기화하고 DB에 저장합니다.
    /// </summary>
    private void InitializeNewPlayerData()
    {
        Status = new PlayerStatus(); // 기본값으로 생성
        // DataManager를 통해 DB에 이 새 데이터를 INSERT 합니다.
        DataManager.Instance.SaveData(
            "PlayerStats",
            new string[] { "SaveSlotID", "Intellect", "Charm", "Endurance", "Money", "HeroineALiked", "HeroineBLiked", "HeroineCLiked" },
            new object[] { 1, Status.Intellect, Status.Charm, Status.Endurance, Status.Money, Status.HeroineALiked, Status.HeroineBLiked, Status.HeroineCLiked }
        );
    }

    /// <summary>
    /// 현재 스탯을 DB에 저장(UPDATE)합니다.
    /// </summary>
    public void SavePlayerData()
    {
        if (Status == null) return;

        DataManager.Instance.UpdateData(
            "PlayerStats",
            new string[] { "Intellect", "Charm", "Endurance", "Money", "HeroineALiked", "HeroineBLiked", "HeroineCLiked" },
            new object[] { Status.Intellect, Status.Charm, Status.Endurance, Status.Money, Status.HeroineALiked, Status.HeroineBLiked, Status.HeroineCLiked },
            "SaveSlotID",
            1
        );
        Debug.Log("플레이어 데이터를 DB에 저장(업데이트)했습니다.");
    }

    // --- 외부에서 스탯을 안전하게 변경하기 위한 메서드들 ---

    public void AddIntellect(int amount)
    {
        Status.Intellect += amount;
        NotifyStatusChanged();
        Debug.Log($"지능 스탯 {amount} 증가! 현재 지능: {Status.Intellect}");
    }

    public void AddCharm(int amount)
    {
        Status.Charm += amount;
        NotifyStatusChanged();
    }

    // ... (다른 스탯 변경 메서드들도 필요에 따라 추가) ...

    /// <summary>
    /// 스탯 변경이 있음을 게임 전체에 알립니다. (UI 업데이트 등)
    /// </summary>
    private void NotifyStatusChanged()
    {
        OnPlayerStatusChanged?.Invoke();
    }
}