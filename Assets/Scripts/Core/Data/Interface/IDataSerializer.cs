// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Interface\IDataSerializer.cs

using System.Collections.Generic;

namespace Core.Data.Interface
{
    /// <summary>
    /// 특정 데이터 타입 T를 Dictionary 형태로 직렬화하고 역직렬화하는 기능을 제공하는 인터페이스입니다.
    /// IBaseDataSerializer를 상속하여 테이블 및 주 키 정보를 공통으로 제공합니다.
    /// </summary>
    /// <typeparam name="T">직렬화/역직렬화할 데이터 타입 (클래스여야 함).</typeparam>
    public interface IDataSerializer<T> : IBaseDataSerializer where T : class
    {
        /// <summary>
        /// 데이터 객체를 데이터베이스 저장을 위한 Dictionary 형태로 직렬화합니다.
        /// </summary>
        /// <param name="data">직렬화할 데이터 객체.</param>
        /// <returns>컬럼 이름과 값의 쌍으로 이루어진 Dictionary.</returns>
        Dictionary<string, object> Serialize(T data);

        /// <summary>
        /// 데이터베이스에서 로드된 Dictionary를 데이터 객체로 역직렬화합니다.
        /// </summary>
        /// <param name="dataMap">데이터베이스에서 로드된 Dictionary.</param>
        /// <returns>역직렬화된 데이터 객체.</returns>
        T Deserialize(Dictionary<string, object> dataMap);

        // GetTableName(), GetPrimaryKeyColumnName(), GetPrimaryKeyDefaultValue()는
        // IBaseDataSerializer에서 이미 정의되었으므로 여기서는 제거합니다.
    }
}