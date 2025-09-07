using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class BootstrapSceneLoader
{
    private const string PREVIOUS_SCENE_KEY = "notTommorow.PreviousScenePath";

    static BootstrapSceneLoader()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // '플레이 시작 전'에는 이제 돌아올 경로를 저장만 합니다. (가장 안전한 작업)
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            EditorPrefs.SetString(PREVIOUS_SCENE_KEY, EditorSceneManager.GetActiveScene().path);
        }

        // '플레이 종료 후'에는 이전과 동일하게 돌아오는 역할을 수행합니다.
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string previousScenePath = EditorPrefs.GetString(PREVIOUS_SCENE_KEY);
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath);
                EditorPrefs.DeleteKey(PREVIOUS_SCENE_KEY);
            }
        }
    }
}