// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\Player\PlayerStatus.cs

/// <summary>
/// 플레이어의 모든 스탯 정보를 담는 데이터 컨테이너 클래스입니다.
/// </summary>
public class PlayerStatus
{
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
    public PlayerStatus()
    {
        // 초기 스탯 값 설정 (기획에 따라 변경)
        Intellect = 10;
        Charm = 10;
        Endurance = 50;
        Money = 30_000_000_000; // 300억!
        HeroineALiked = 0;
        HeroineBLiked = 0;
        HeroineCLiked = 0;
    }
}