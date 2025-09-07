// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionManager.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 게임 시작 시 Fade In
    private void Start()
    {
        StartCoroutine(Fade(0f)); // 투명하게 만들기
    }

    public void FadeAndLoadScene(string sceneName)
    {
        StartCoroutine(FadeAndLoadCoroutine(sceneName));
    }

    private IEnumerator FadeAndLoadCoroutine(string sceneName)
    {
        // Fade Out
        yield return StartCoroutine(Fade(1f));

        // Scene Load
        SceneManager.LoadScene(sceneName);

        // Fade In (새 씬이 로드된 후)
        yield return StartCoroutine(Fade(0f));
    }

    private IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = fadeImage.color.a;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }

        fadeImage.color = new Color(0, 0, 0, targetAlpha);
    }
}