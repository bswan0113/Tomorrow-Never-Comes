using System;
using System.Data;
using System.Threading.Tasks;

namespace Core.Data.Interface
{
    /// <summary>
    /// 데이터베이스 트랜잭션을 추상화하는 인터페이스입니다.
    /// 트랜잭션 관련 작업을 커밋, 롤백, 리소스 해제할 수 있도록 합니다.
    /// </summary>
    public interface ITransaction : IDisposable
    {
        /// <summary>
        /// 현재 트랜잭션이 사용 중인 데이터베이스 연결 객체를 가져옵니다.
        /// </summary>
        IDbConnection Connection { get; }

        /// <summary>
        /// 현재 활성화된 데이터베이스 트랜잭션 객체를 가져옵니다.
        /// </summary>
        IDbTransaction DbTransaction { get; }

        /// <summary>
        /// 트랜잭션 내에서 수행된 모든 작업을 비동기적으로 커밋합니다.
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// 트랜잭션 내에서 수행된 모든 작업을 비동기적으로 롤백합니다.
        /// </summary>
        Task RollbackAsync();
    }
}