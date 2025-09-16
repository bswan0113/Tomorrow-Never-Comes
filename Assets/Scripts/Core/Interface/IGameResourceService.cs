using ScriptableObjects.Abstract;
using UnityEngine;

namespace Core.Interface
{
    public interface IGameResourceService
    {
        T[] GetAllDataOfType<T>() where T : GameData;
        T GetDataByID<T>(string id) where T : GameData;

    }
}