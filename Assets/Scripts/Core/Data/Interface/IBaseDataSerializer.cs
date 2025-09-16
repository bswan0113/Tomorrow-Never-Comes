// C:\Workspace\Tomorrow Never Comes\Assets\Scripts\Core\Data\Interface\IBaseDataSerializer.cs

namespace Core.Data.Interface
{
    /// <summary>
    /// 모든 데이터 시리얼라이저가 공통적으로 가져야 할 기본 정보 접근 인터페이스입니다.
    /// VContainer 또는 내부적으로 시리얼라이저를 타입 불문하고 관리할 때 사용됩니다.
    /// </summary>
    public interface IBaseDataSerializer
    {
        /// <summary>
        /// 이 시리얼라이저가 다루는 데이터 타입이 저장될 테이블 이름을 반환합니다.
        /// </summary>
        string GetTableName();

        /// <summary>
        /// 테이블의 주 키 컬럼 이름을 반환합니다.
        /// </summary>
        string GetPrimaryKeyColumnName();

        /// <summary>
        /// 주 키의 기본값 (예: 단일 세이브 슬롯 게임의 경우 1)을 반환합니다.
        /// </summary>
        object GetPrimaryKeyDefaultValue();
    }
}