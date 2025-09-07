// C:\Workspace\Tomorrow Never Comes\Assets\Editor\ImporterProfile.cs

using System;

// 개별 임포터 설정을 정의하는 클래스
// 이 파일은 MonoBehaviour나 ScriptableObject가 아니므로, 유니티 에디터의 Create 메뉴에 나타나지 않습니다.
[Serializable]
public class ImporterProfile
{
    public string profileName; // "Dialogue Importer", "Character Importer"
    public bool isEnabled = true;

    public string soTypeFullName; // 예: "DialogueData, Assembly-CSharp"
    public UnityEngine.TextAsset csvFile;     // CSV 파일 (TextAsset으로 연결)
    public string outputSOPath = "/Resources/ScriptableObject/";   // SO가 저장될 경로
}