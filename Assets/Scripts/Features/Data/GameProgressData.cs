namespace Features.Data
{
    // --- START OF FILE GameProgressData.cs ---

// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Game\Data\GameProgressData.cs

    using System;

    /// <summary>
    /// 게임의 전반적인 진행 상황을 담는 데이터 컨테이너 클래스입니다.
    /// 데이터베이스 저장을 위한 데이터 모델로 사용됩니다.
    /// </summary>
    public class GameProgressData
    {
        // --- 세이브 슬롯 식별자 (데이터베이스 저장/로드 시 필수) ---
        public int SaveSlotID { get; set; } = 1; // 기본값으로 1번 슬롯 가정

        // --- 게임 진행 상황 ---
        public int CurrentDay { get; set; }     // 현재 게임 내 날짜
        public string LastSceneName { get; set; } // 마지막으로 저장된 씬의 이름
        public DateTime SaveDateTime { get; set; } // 저장된 시간 (UTC)

        /// <summary>
        /// 새 게임 시작 시 기본값으로 객체를 생성하는 생성자입니다.
        /// </summary>
        public GameProgressData()
        {
            SaveSlotID = 1; // 기본 세이브 슬롯 ID
            CurrentDay = 1;
            LastSceneName = "StartScene"; // 시작 씬 이름 (기획에 따라 변경)
            SaveDateTime = DateTime.UtcNow; // 현재 UTC 시간으로 초기화
        }
    }
// --- END OF FILE GameProgressData.cs ---
}