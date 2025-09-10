// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Features\World\IInteractable.cs

namespace Features.World
{
    /// <summary>
    /// 상호작용이 가능한 모든 오브젝트가 구현해야 하는 인터페이스입니다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 이 오브젝트와 상호작용했을 때 호출될 메서드입니다.
        /// </summary>
        void Interact();
    }
}