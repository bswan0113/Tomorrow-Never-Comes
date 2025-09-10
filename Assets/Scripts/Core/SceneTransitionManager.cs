// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\SceneTransitionManager.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Interface;


public class SceneTransitionManager : MonoBehaviour, ISceneTransitionService // 인터페이스 구현 추가
{

    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;


    // 게임 시작 시 Fade In (Start 메서드는 MonoBehaviour의 라이프사이클 메서드이므로 그대로 유지)
    private void Start()
    {
        if (fadeImage != null)
        {
            StartCoroutine(Fade(0f)); // 투명하게 만들기
        }
        else
        {
            Debug.LogWarning("SceneTransitionManager: fadeImage가 할당되지 않았습니다. 페이드 효과가 작동하지 않습니다.");
        }
    }

    // ISceneTransitionService 인터페이스 메서드 구현
    public void FadeAndLoadScene(string sceneName)
    {
        // MonoBehaviour의 StartCoroutine을 사용하기 위해 여전히 MonoBehaviour 인스턴스가 필요합니다.
        // 따라서 이 클래스는 MonoBehaviour를 유지하고, Composition Root에서 GameObject에 컴포넌트로 추가될 것입니다.
        StartCoroutine(FadeAndLoadCoroutine(sceneName));
    }

    private IEnumerator FadeAndLoadCoroutine(string sceneName)
    {
        // Fade Out
        yield return StartCoroutine(Fade(1f));

        // Scene Load
        SceneManager.LoadScene(sceneName);

        // Fade In (새 씬이 로드된 후)
        // fadeImage가 null일 경우 대비
        if (fadeImage != null)
        {
            yield return StartCoroutine(Fade(0f));
        }
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeImage == null)
        {
            Debug.LogError("FadeImage is null. Cannot perform fade.");
            yield break;
        }

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