using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "PlayerRoom";

    // ▼▼▼▼▼ [핵심] 이 "우편함" 변수가 반드시 필요합니다! ▼▼▼▼▼
    public static string SceneToLoadOverride;

    void Start()
    {
        // RuntimeInitializer가 이 '우편함'에 넣어준 쪽지가 있는지 확인합니다.
        if (!string.IsNullOrEmpty(SceneToLoadOverride))
        {
            string sceneToLoad = SceneToLoadOverride;

            // 우편함은 확인 후 다음을 위해 반드시 비워줍니다.
            SceneToLoadOverride = null;

            SceneManager.LoadScene(sceneToLoad);
            return;
        }

        // 우편함에 쪽지가 없다면, 기본 씬(MainMenuScene)으로 갑니다.
        SceneManager.LoadScene(nextSceneName);
    }
}