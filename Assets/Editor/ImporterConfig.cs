// C:\Workspace\Tomorrow Never Comes\Assets\Editor\ImporterConfig.cs

using UnityEngine;

// 여러 임포터 설정을 담아둘 컨테이너 SO
[CreateAssetMenu(fileName = "ImporterConfig", menuName = "Tools/Importer Config")]
public class ImporterConfig : ScriptableObject
{
    public ImporterProfile[] profiles;
}