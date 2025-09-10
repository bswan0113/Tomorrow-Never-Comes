// --- START OF FILE IDataSerializer.cs ---

using System.Collections.Generic;

namespace Core.Data.Interface // Core.Interface 네임스페이스에 포함
{
    /// <summary>
    /// 게임 데이터 객체를 Dictionary 형태로 직렬화하고, Dictionary에서 객체로 역직렬화하는 기능을 정의합니다.
    /// DataManager가 특정 게임 데이터를 직접 다루는 대신, 이 인터페이스를 통해 데이터를 변환합니다.
    /// </summary>
    public interface IDataSerializer<T> : IBaseDataSerializer where T : class // IBaseDataSerializer 상속 추가
    {
        /// <summary>
        /// 게임 데이터 객체를 데이터베이스 저장을 위한 Dictionary<string, object> 형태로 직렬화합니다.
        /// </summary>
        /// <param name="data">직렬화할 게임 데이터 객체.</param>
        /// <returns>데이터베이스 저장을 위한 Dictionary 형태의 데이터.</returns>
        Dictionary<string, object> Serialize(T data);

        /// <summary>
        /// 데이터베이스에서 로드된 Dictionary<string, object> 형태의 데이터를
        /// 게임 데이터 객체로 역직렬화합니다.
        /// </summary>
        /// <param name="dataMap">데이터베이스에서 로드된 Dictionary 형태의 데이터.</param>
        /// <returns>역직렬화된 게임 데이터 객체. 역직렬화 실패 또는 데이터가 없는 경우 null을 반환할 수 있습니다.</returns>
        T Deserialize(Dictionary<string, object> dataMap);

        // GetTableName, GetPrimaryKeyColumnName, GetPrimaryKeyDefaultValue는 IBaseDataSerializer로 이동했으므로
        // 이 인터페이스에서는 제거할 수 있습니다. (하지만 구현 클래스에서는 여전히 구현해야 함)
        // 명시적으로 여기에 다시 선언하지 않아도 됩니다.
    }
}
// --- END OF FILE IDataSerializer.cs ---