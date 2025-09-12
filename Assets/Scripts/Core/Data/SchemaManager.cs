// --- START OF FILE SchemaManager.cs ---

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Logging;
using Newtonsoft.Json;
using UnityEngine;

namespace Core.Data // 새로운 네임스페이스 (혹은 기존 Core.Interface에 포함해도 됨)
{
    // 스키마 정보를 담을 내부 클래스
    public class ColumnSchema
    {
        public string Name { get; set; }
        public string Type { get; set; } // 예: "INTEGER", "TEXT", "REAL"
        public bool IsPrimaryKey { get; set; }
        public bool IsNullable { get; set; }
        public string DefaultValue { get; set; } // 예: "0", "NULL", "'default_text'"
    }

    public class TableSchema
    {
        public string Name { get; set; }
        public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();
        public string CreateQuery { get; set; } // 테이블 생성 쿼리 원본
    }

    /// <summary>
    /// 데이터베이스 스키마 정보를 로드하고 관리하며,
    /// SQL 식별자(테이블, 컬럼 이름)의 유효성을 검사하는 책임만을 가집니다.
    /// </summary>
    public class SchemaManager
    {
        private Dictionary<string, TableSchema> m_TableSchemas;
        private readonly Regex TableNameRegex = new Regex(@"CREATE TABLE (IF NOT EXISTS )?(?<TableName>\w+)", RegexOptions.IgnoreCase);
        public SchemaManager()
        {
            m_TableSchemas = new Dictionary<string, TableSchema>();
            LoadSchemasInternal();
        }

        /// <summary>
        /// SQLSchemas.json 파일에서 스키마 정보를 로드하고 파싱합니다.
        /// 이 메서드는 생성자에서 호출됩니다.
        /// </summary>
                private void LoadSchemasInternal()
        {
            TextAsset sqlJson = Resources.Load<TextAsset>("SQLSchemas");
            if (sqlJson == null)
            {
                CoreLogger.LogError("[SchemaManager] Resources/SQLSchemas.json file not found! Unable to load database schemas.");
                throw new FileNotFoundException("SQLSchemas.json file not found in Resources.", "SQLSchemas");
            }

            var rawQueries = JsonConvert.DeserializeObject<Dictionary<string, string>>(sqlJson.text);
            if (rawQueries == null)
            {
                CoreLogger.LogError("[SchemaManager] Failed to deserialize SQLSchemas.json. File content might be invalid.");
                return;
            }

            foreach (var entry in rawQueries)
            {
                string createQuery = entry.Value; // JSON 값(CREATE TABLE 쿼리)을 사용

                // 정규식을 사용하여 쿼리에서 실제 테이블 이름 추출
                Match match = TableNameRegex.Match(createQuery);
                if (!match.Success)
                {
                    CoreLogger.LogWarning($"[SchemaManager] Could not extract table name from query: '{createQuery}'. Skipping.");
                    continue;
                }
                string actualTableName = match.Groups["TableName"].Value;

                if (string.IsNullOrWhiteSpace(actualTableName))
                {
                    CoreLogger.LogWarning($"[SchemaManager] Extracted empty table name from query: '{createQuery}'. Skipping.");
                    continue;
                }

                if (m_TableSchemas.ContainsKey(actualTableName)) // 실제 테이블 이름으로 검사
                {
                    CoreLogger.LogWarning($"[SchemaManager] Duplicate table name '{actualTableName}' found in schema. Overwriting.");
                }

                m_TableSchemas[actualTableName] = new TableSchema // 실제 테이블 이름으로 저장
                {
                    Name = actualTableName,
                    CreateQuery = createQuery,
                    Columns = new List<ColumnSchema>() // TODO: 실제 파싱 로직 구현
                };
                CoreLogger.Log($"[SchemaManager] Loaded schema for table: {actualTableName} (from JSON key '{entry.Key}')");
            }
            CoreLogger.Log($"[SchemaManager] Loaded {m_TableSchemas.Count} table schemas.");
        }
        /// <summary>
        /// SQLSchemas.json 파일에서 스키마 정보를 로드하고 파싱합니다.
        /// </summary>
        public void LoadSchemas()
        {
            TextAsset sqlJson = Resources.Load<TextAsset>("SQLSchemas");
            if (sqlJson == null)
            {
                CoreLogger.LogError("[SchemaManager] Resources/SQLSchemas.json file not found! Unable to load database schemas.");
                throw new FileNotFoundException("SQLSchemas.json file not found in Resources.", "SQLSchemas");
            }

            // SQLSchemas.json은 { "TableName": "CREATE TABLE ..." } 형태이므로,
            // 이 쿼리를 파싱하여 ColumnSchema 목록을 만들어야 합니다.
            // 단순화를 위해 여기서는 각 쿼리를 TableSchema.CreateQuery에 저장하고,
            // 테이블 이름만 추출하여 허용된 테이블로 등록합니다.
            // 컬럼 정보 파싱은 더 복잡하므로, 초기 단계에서는 테이블 이름 유효성 검사에 집중합니다.
            // TODO: 실제 CREATE TABLE 쿼리를 파싱하여 Columns 정보를 채우는 로직 구현
            var rawQueries = JsonConvert.DeserializeObject<Dictionary<string, string>>(sqlJson.text);
            if (rawQueries == null)
            {
                CoreLogger.LogError("[SchemaManager] Failed to deserialize SQLSchemas.json. File content might be invalid.");
                return;
            }

            foreach (var entry in rawQueries)
            {
                string tableName = entry.Key; // 보통 Dictionary의 키가 테이블 이름으로 사용됨
                // CREATE TABLE 문의 실제 테이블 이름을 추출 (더 견고한 파싱 필요)
                // 예: "CREATE TABLE PlayerStats (id INTEGER PRIMARY KEY, ...)"
                // 단순화를 위해 일단 키를 테이블 이름으로 간주합니다.

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    CoreLogger.LogWarning($"[SchemaManager] Skipping entry with empty table name in SQLSchemas.json: {entry.Value}");
                    continue;
                }

                if (m_TableSchemas.ContainsKey(tableName))
                {
                    CoreLogger.LogWarning($"[SchemaManager] Duplicate table name '{tableName}' found in SQLSchemas.json. Overwriting.");
                }

                // TODO: 여기에서 CREATE TABLE 쿼리를 파싱하여 ColumnSchema 목록을 채워야 합니다.
                // 현재는 빈 목록으로 초기화합니다.
                m_TableSchemas[tableName] = new TableSchema
                {
                    Name = tableName,
                    CreateQuery = entry.Value,
                    Columns = new List<ColumnSchema>() // 실제 파싱 로직 구현 필요
                };
                CoreLogger.Log($"[SchemaManager] Loaded schema for table: {tableName}");
            }
            CoreLogger.Log($"[SchemaManager] Loaded {m_TableSchemas.Count} table schemas.");
        }

        /// <summary>
        /// 주어진 테이블 이름이 허용된 스키마에 존재하는지 확인합니다.
        /// </summary>
        /// <param name="tableName">확인할 테이블 이름.</param>
        /// <returns>테이블 이름이 유효하면 true, 아니면 false.</returns>
        public bool IsTableNameValid(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return false;
            return m_TableSchemas.ContainsKey(tableName);
        }

        /// <summary>
        /// 주어진 테이블에 특정 컬럼 이름이 유효한지 확인합니다.
        /// (현재는 컬럼 스키마 파싱이 구현되지 않았으므로, 최소한의 안전성 검사만 수행)
        /// </summary>
        /// <param name="tableName">테이블 이름.</param>
        /// <param name="columnName">확인할 컬럼 이름.</param>
        /// <returns>컬럼 이름이 유효하면 true, 아니면 false.</returns>
        public bool IsColumnNameValid(string tableName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return false;
            if (!IsTableNameValid(tableName)) return false; // 테이블이 유효하지 않으면 컬럼도 유효할 수 없음

            // P17: SQL 식별자 문자열 삽입 취약 - 최소한의 문자열 검사
            // 컬럼 이름에는 공백, 세미콜론, 따옴표 등을 포함해서는 안 됩니다.
            if (columnName.Any(char.IsWhiteSpace) || columnName.Contains(";") || columnName.Contains("'") || columnName.Contains("\"") || columnName.Contains("--"))
            {
                CoreLogger.LogWarning($"[SchemaManager] Column name '{columnName}' for table '{tableName}' contains invalid characters.");
                return false;
            }

            // TODO: m_TableSchemas[tableName].Columns를 참조하여 실제 컬럼이 존재하는지 확인하는 로직 추가
            // 현재는 컬럼 스키마를 파싱하지 않으므로, 이 부분은 더미 검사만 합니다.
            // 이상적으로는 m_TableSchemas[tableName].Columns.Any(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)) 와 같이 되어야 합니다.
            return true; // 임시적으로 모든 "안전해 보이는" 컬럼 이름을 허용
        }

        /// <summary>
        /// 주어진 테이블의 모든 CREATE TABLE 쿼리 문자열을 반환합니다.
        /// </summary>
        public IEnumerable<string> GetAllTableCreateQueries()
        {
            return m_TableSchemas.Values.Select(ts => ts.CreateQuery);
        }

        /// <summary>
        /// 특정 테이블의 스키마 정보를 반환합니다.
        /// </summary>
        public TableSchema GetTableSchema(string tableName)
        {
            if (m_TableSchemas.TryGetValue(tableName, out var schema))
            {
                return schema;
            }
            return null;
        }
    }
}
