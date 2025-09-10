// --- START OF FILE IBaseDataSerializer.cs ---

using System.Collections.Generic;

namespace Core.Data.Interface
{
    /// <summary>
    /// 모든 IDataSerializer<T>가 공통적으로 구현해야 하는 비-제네릭 멤버들을 정의하는
    /// 기본 인터페이스입니다. 이를 통해 IDataSerializer<T> 타입들을 묶어서 관리할 수 있습니다.
    /// </summary>
    public interface IBaseDataSerializer
    {
        /// <summary>
        /// 해당 데이터 타입이 사용하는 테이블 이름을 반환합니다.
        /// </summary>
        string GetTableName();

        /// <summary>
        /// 데이터가 저장될 때 고유하게 식별할 키 컬럼의 이름을 반환합니다.
        /// </summary>
        string GetPrimaryKeyColumnName();

        /// <summary>
        /// 데이터가 저장될 때 고유하게 식별할 키 컬럼의 기본 값을 반환합니다.
        /// </summary>
        object GetPrimaryKeyDefaultValue();
    }
}
