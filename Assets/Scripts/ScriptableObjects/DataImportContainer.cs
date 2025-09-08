// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Data\DataImportContainer.cs (경로는 예시입니다)

using UnityEngine;
using System.Collections.Generic;

// 이 클래스는 CSV에서 임포트된 SO들을 담는 역할을 합니다.
// 런타임에 직접 사용할 일은 거의 없으며, 에디터에서 데이터 관리를 편하게 하기 위한 용도입니다.
public class DataImportContainer : ScriptableObject
{
    [SerializeField]
    public List<ScriptableObject> importedObjects = new List<ScriptableObject>();
}

// 참고: 기존 CSVParser는 변경 없이 그대로 사용하면 됩니다.
// 만약 다른 곳에 없다면 GenericCsvImporter.cs 파일 안에 private static class로 넣거나, 별도 파일로 유지해도 좋습니다.