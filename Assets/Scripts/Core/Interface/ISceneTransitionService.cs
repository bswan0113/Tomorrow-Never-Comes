// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Interface\ISceneTransitionService.cs

using System; // Action 델리게이트를 사용하기 위해 추가

namespace Core.Interface
{
    public interface ISceneTransitionService
    {
        /// <summary>
        /// 씬 전환이 현재 진행 중인지 여부를 나타냅니다.
        /// </summary>
        bool IsTransitioning { get; }

        /// <summary>
        /// 씬 전환 상태가 변경될 때 발생합니다.
        /// (true: 전환 시작, false: 전환 완료)
        /// </summary>
        event Action<bool> OnTransitionStateChanged;

        /// <summary>
        /// 지정된 씬으로 페이드 효과와 함께 전환을 시작합니다.
        /// </summary>
        /// <param name="sceneName">로드할 씬의 이름입니다.</param>
        void FadeAndLoadScene(string sceneName);
    }
}