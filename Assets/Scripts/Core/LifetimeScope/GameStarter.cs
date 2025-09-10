namespace Core
{
    using VContainer.Unity;
    using UnityEngine;

    public class GameStarter : IStartable
    {
        private readonly GameManager _gameManager;

        public GameStarter(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        void IStartable.Start()
        {
            Debug.Log("<color=lime>[GameStarter] 게임 시작...</color>");
            _gameManager.StartGame();
        }
    }
}