// --- START OF FILE SchemaManager.cs ---

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Logging;
using Newtonsoft.Json;
using UnityEngine;
using Core.Data.Interface; // IDatabaseAccess 인터페이스를 사용하기 위해 추가

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
    /// 또한, IDatabaseAccess를 사용하여 실제 테이블을 초기화합니다.
    /// </summary>
    public class SchemaManager
    {
        private Dictionary<string, TableSchema> m_TableSchemas;

        // CREATE TABLE 쿼리에서 테이블 이름을 추출하기 위한 정규식
        private readonly Regex TableNameRegex = new Regex(@"CREATE TABLE (IF NOT EXISTS )?(?<TableName>\w+)", RegexOptions.IgnoreCase);

        // CREATE TABLE 쿼리에서 컬럼 정의 부분을 추출하기 위한 정규식 (괄호 안 내용)
        private readonly Regex ColumnsContentRegex = new Regex(@"CREATE TABLE(?: IF NOT EXISTS)? \w+\s*\((?<ColumnsContent>.*?)\)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // 컬럼 정의 문자열에서 이름, 타입, 제약조건을 파싱하기 위한 정규식 (부분적으로만 강력함)
        // 그룹: 1=컬럼이름, 2=타입, 3=제약조건 (PRIMARY KEY, NOT NULL, DEFAULT 등)
        private readonly Regex ColumnDefinitionRegex = new Regex(
            @"^\s*(?<Name>\w+)\s+(?<Type>\w+)" + // Column Name and Type
            @"(?<Constraints>(?:\s+(?:PRIMARY KEY|NOT NULL|UNIQUE|CHECK\s*\(.+?\)|DEFAULT\s+(?:'[^']+'|\d+|NULL)))*?)" + // Optional constraints
            @"(?:,\s*|$)", // Ends with comma or end of string
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
        );


        public SchemaManager()
        {
            m_TableSchemas = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase); // 테이블 이름 대소문자 무시
            LoadSchemasInternal();
        }

        /// <summary>
        /// SQLSchemas.json 파일에서 스키마 정보를 로드하고 파싱합니다.
        /// 이 메서드는 생성자에서 호출되며, 컬럼 정보까지 파싱합니다.
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
                string createQuery = entry.Value;

                // 정규식을 사용하여 쿼리에서 실제 테이블 이름 추출 (JSON 키 대신 쿼리 내부 이름 사용)
                Match tableNameMatch = TableNameRegex.Match(createQuery);
                if (!tableNameMatch.Success)
                {
                    CoreLogger.LogWarning($"[SchemaManager] Could not extract table name from query: '{createQuery}'. Skipping.");
                    continue;
                }
                string actualTableName = tableNameMatch.Groups["TableName"].Value;

                if (string.IsNullOrWhiteSpace(actualTableName))
                {
                    CoreLogger.LogWarning($"[SchemaManager] Extracted empty table name from query: '{createQuery}'. Skipping.");
                    continue;
                }

                if (m_TableSchemas.ContainsKey(actualTableName))
                {
                    CoreLogger.LogWarning($"[SchemaManager] Duplicate table name '{actualTableName}' found in schema. Overwriting.");
                }

                TableSchema tableSchema = new TableSchema
                {
                    Name = actualTableName,
                    CreateQuery = createQuery,
                    Columns = new List<ColumnSchema>()
                };

                // 컬럼 정보 파싱
                Match columnsMatch = ColumnsContentRegex.Match(createQuery);
                if (columnsMatch.Success)
                {
                    string columnsContent = columnsMatch.Groups["ColumnsContent"].Value.Trim();

                    // 각 컬럼 정의를 파싱
                    // NOTE: 이 파싱 로직은 매우 단순하므로, 복잡한 DDL (예: FOREIGN KEY 제약 조건)에는 취약할 수 있습니다.
                    // 실제 애플리케이션에서는 더 강력한 SQL 파서를 고려할 수 있습니다.
                    MatchCollection columnDefMatches = ColumnDefinitionRegex.Matches(columnsContent);
                    foreach (Match colDefMatch in columnDefMatches)
                    {
                        if (!colDefMatch.Success) continue;

                        string colName = colDefMatch.Groups["Name"].Value;
                        string colType = colDefMatch.Groups["Type"].Value;
                        string constraints = colDefMatch.Groups["Constraints"].Value;

                        bool isPrimaryKey = constraints.IndexOf("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isNullable = constraints.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) < 0; // NOT NULL이 없으면 NULL 허용

                        string defaultValue = null;
                        Match defaultMatch = Regex.Match(constraints, @"DEFAULT\s+(?<DefaultValue>(?:'[^']+'|\d+|NULL))", RegexOptions.IgnoreCase);
                        if (defaultMatch.Success)
                        {
                            defaultValue = defaultMatch.Groups["DefaultValue"].Value;
                        }

                        tableSchema.Columns.Add(new ColumnSchema
                        {
                            Name = colName,
                            Type = colType,
                            IsPrimaryKey = isPrimaryKey,
                            IsNullable = isNullable,
                            DefaultValue = defaultValue
                        });
                    }
                }

                m_TableSchemas[actualTableName] = tableSchema;
                CoreLogger.Log($"[SchemaManager] Loaded schema for table: {actualTableName} (from JSON key '{entry.Key}', parsed {tableSchema.Columns.Count} columns)");
            }
            CoreLogger.Log($"[SchemaManager] Loaded {m_TableSchemas.Count} table schemas.");
        }

        /// <summary>
        /// 로드된 스키마 정보를 기반으로 데이터베이스에 실제 테이블을 생성합니다.
        /// 이 메서드는 IDatabaseAccess를 통해 DB 작업을 위임받습니다.
        /// </summary>
        /// <param name="dbAccess">데이터베이스 접근 인터페이스.</param>
        public void InitializeTables(IDatabaseAccess dbAccess)
        {
            if (dbAccess == null)
            {
                throw new ArgumentNullException(nameof(dbAccess), "[SchemaManager] IDatabaseAccess cannot be null for schema initialization.");
            }

            CoreLogger.Log("[SchemaManager] Initializing database tables using IDatabaseAccess...");
            foreach (var tableSchema in m_TableSchemas.Values)
            {
                try
                {
                    // "CREATE TABLE IF NOT EXISTS" 쿼리이므로, 이미 존재하면 오류 없이 건너뜁니다.
                    dbAccess.ExecuteNonQuery(tableSchema.CreateQuery);
                    CoreLogger.Log($"[SchemaManager] Successfully created/ensured table: {tableSchema.Name}");
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError($"[SchemaManager] Failed to create table {tableSchema.Name} with query: {tableSchema.CreateQuery}. Error: {ex.Message}");
                    // 스키마 초기화는 매우 중요한 단계이므로, 실패 시 애플리케이션을 계속 진행하기 어려울 수 있습니다.
                    // 따라서 예외를 다시 던져서 상위 호출자가 이 문제를 처리하도록 합니다.
                    throw new InvalidOperationException($"Failed to initialize database table '{tableSchema.Name}'. See previous errors for details.", ex);
                }
            }
            CoreLogger.Log("[SchemaManager] Database table initialization complete.");
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
        /// </summary>
        /// <param name="tableName">테이블 이름.</param>
        /// <param name="columnName">확인할 컬럼 이름.</param>
        /// <returns>컬럼 이름이 유효하면 true, 아니면 false.</returns>
        public bool IsColumnNameValid(string tableName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return false;
            if (!m_TableSchemas.TryGetValue(tableName, out var tableSchema)) return false; // 테이블이 유효하지 않으면 컬럼도 유효할 수 없음

            // P17: SQL 식별자 문자열 삽입 취약 - 최소한의 문자열 검사 (여전히 중요)
            if (columnName.Any(char.IsWhiteSpace) || columnName.Contains(";") || columnName.Contains("'") || columnName.Contains("\"") || columnName.Contains("--"))
            {
                CoreLogger.LogWarning($"[SchemaManager] Column name '{columnName}' for table '{tableName}' contains invalid characters (pre-check).");
                return false;
            }

            // 파싱된 컬럼 목록을 참조하여 실제 컬럼이 존재하는지 확인
            return tableSchema.Columns.Any(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 모든 테이블의 CREATE TABLE 쿼리 문자열을 반환합니다.
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
// --- END OF FILE SchemaManager.cs ---