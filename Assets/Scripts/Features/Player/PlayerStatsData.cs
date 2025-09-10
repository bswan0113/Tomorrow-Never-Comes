// --- START OF FILE PlayerStatsData.cs ---

// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerStatsData.cs



// Convert.ToInt32 등을 위해 필요할 수 있습니다.

namespace Features.Player
{
    /// <summary>
    /// 플레이어의 모든 스탯 정보를 담는 데이터 컨테이너 클래스입니다.
    /// 데이터베이스 저장을 위한 데이터 모델로 사용됩니다.
    /// </summary>
    public class PlayerStatsData
    {
        // --- 세이브 슬롯 식별자 (데이터베이스 저장/로드 시 필수) ---
        public int SaveSlotID { get; set; } = 1; // 기본값으로 1번 슬롯 가정

        // --- 기본 스탯 ---
        public int Intellect { get; set; }  // 지능
        public int Charm { get; set; }      // 매력
        public int Endurance { get; set; }  // 체력 (건강)

        // --- 재화 ---
        public long Money { get; set; }     // 돈

        // --- 히로인 호감도 ---
        public int HeroineALiked { get; set; } // A 히로인 호감도
        public int HeroineBLiked { get; set; } // B 히로인 호감도
        public int HeroineCLiked { get; set; } // C 히로인 호감도

        /// <summary>
        /// 새 게임 시작 시 기본값으로 객체를 생성하는 생성자입니다.
        /// </summary>
        public PlayerStatsData()
        {
            // 초기 스탯 값 설정 (기획에 따라 변경)
            SaveSlotID = 1; // 기본 세이브 슬롯 ID
            Intellect = 10;
            Charm = 10;
            Endurance = 50;
            Money = 30_000_000_000L; // long 리터럴을 명시적으로 L 접미사로 표시
            HeroineALiked = 0;
            HeroineBLiked = 0;
            HeroineCLiked = 0;
        }
    }
}
// --- END OF FILE PlayerStatsData.cs ---