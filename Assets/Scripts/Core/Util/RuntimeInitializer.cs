using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Util
{
    public static class RuntimeInitializer
    {
        // 이 함수는 플레이 모드가 시작되면 Awake보다 먼저 딱 한 번 호출됩니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeBeforeSceneLoad()
        {
#if UNITY_EDITOR
            string bootstrapScenePath = "Assets/Scenes/Initialization.unity"; // ★ 경로 확인
            const string PREVIOUS_SCENE_KEY = "notTommorow.PreviousScenePath";

            // 현재 씬이 부트스트랩 씬이 아니라면,
            if (SceneManager.GetActiveScene().path != bootstrapScenePath)
            {
                string previousScene = EditorPrefs.GetString(PREVIOUS_SCENE_KEY);
                if (!string.IsNullOrEmpty(previousScene))
                {
                    SceneLoader.SceneToLoadOverride = previousScene;
                    EditorPrefs.DeleteKey(PREVIOUS_SCENE_KEY);
                }

                // 그리고 즉시 부트스트랩 씬을 로드합니다.
                SceneManager.LoadScene(bootstrapScenePath);
            }
#endif
        }
    }
}