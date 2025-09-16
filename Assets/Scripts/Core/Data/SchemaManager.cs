// C:\Workspace\Tomorrow Never Comes\Core\Data\SchemaManager.cs (REFACTORED)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks; // Task를 사용하기 위해 추가
using Core.Logging;
using Newtonsoft.Json;
using UnityEngine;
using Core.Data.Interface;

namespace Core.Data
{
    // 스키마 정보를 담을 내부 클래스들 (변경 없음)
    public class ColumnSchema
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsNullable { get; set; }
        public string DefaultValue { get; set; }
    }

    public class TableSchema
    {
        public string Name { get; set; }
        public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();
        public string CreateQuery { get; set; }
    }

    /// <summary>
    /// 데이터베이스 스키마 정보를 로드하고 관리하며,
    /// SQL 식별자(테이블, 컬럼 이름)의 유효성을 검사하는 책임만을 가집니다.
    /// 또한, IDatabaseAccess를 사용하여 실제 테이블을 초기화합니다.
    /// </summary>
    public class SchemaManager
    {
        private Dictionary<string, TableSchema> m_TableSchemas;

        // 정규식들 (변경 없음)
        private readonly Regex TableNameRegex = new Regex(@"CREATE TABLE (IF NOT EXISTS )?(?<TableName>\w+)", RegexOptions.IgnoreCase);
        private readonly Regex ColumnsContentRegex = new Regex(@"CREATE TABLE(?: IF NOT EXISTS)? \w+\s*\((?<ColumnsContent>.*?)\)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private readonly Regex ColumnDefinitionRegex = new Regex(
            @"^\s*(?<Name>\w+)\s+(?<Type>\w+)" +
            @"(?<Constraints>(?:\s+(?:PRIMARY KEY|NOT NULL|UNIQUE|CHECK\s*\(.+?\)|DEFAULT\s+(?:'[^']+'|\d+|NULL)))*?)" +
            @"(?:,\s*|$)",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
        );

        public SchemaManager()
        {
            m_TableSchemas = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
            LoadSchemasInternal();
        }

        // LoadSchemasInternal (변경 없음)
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

                Match columnsMatch = ColumnsContentRegex.Match(createQuery);
                if (columnsMatch.Success)
                {
                    string columnsContent = columnsMatch.Groups["ColumnsContent"].Value.Trim();
                    MatchCollection columnDefMatches = ColumnDefinitionRegex.Matches(columnsContent);
                    foreach (Match colDefMatch in columnDefMatches)
                    {
                        if (!colDefMatch.Success) continue;
                        string colName = colDefMatch.Groups["Name"].Value;
                        string colType = colDefMatch.Groups["Type"].Value;
                        string constraints = colDefMatch.Groups["Constraints"].Value;
                        bool isPrimaryKey = constraints.IndexOf("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isNullable = constraints.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) < 0;
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
        /// 로드된 스키마 정보를 기반으로 데이터베이스에 실제 테이블을 생성합니다. (비동기 버전)
        /// 이 메서드는 IDatabaseAccess를 통해 DB 작업을 위임받습니다.
        /// </summary>
        /// <param name="dbAccess">데이터베이스 접근 인터페이스.</param>
        public async Task InitializeTablesAsync(IDatabaseAccess dbAccess) // *** CHANGED: async Task, 이름 변경 ***
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
                    // *** CHANGED: 비동기 메서드 호출 및 await 사용 ***
                    await dbAccess.ExecuteNonQueryAsync(tableSchema.CreateQuery);
                    CoreLogger.Log($"[SchemaManager] Successfully created/ensured table: {tableSchema.Name}");
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError($"[SchemaManager] Failed to create table {tableSchema.Name} with query: {tableSchema.CreateQuery}. Error: {ex.Message}");
                    throw new InvalidOperationException($"Failed to initialize database table '{tableSchema.Name}'. See previous errors for details.", ex);
                }
            }
            CoreLogger.Log("[SchemaManager] Database table initialization complete.");
        }


        // IsTableNameValid, IsColumnNameValid, GetAllTableCreateQueries, GetTableSchema (변경 없음)
        public bool IsTableNameValid(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return false;
            return m_TableSchemas.ContainsKey(tableName);
        }

        public bool IsColumnNameValid(string tableName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return false;
            if (!m_TableSchemas.TryGetValue(tableName, out var tableSchema)) return false;

            if (columnName.Any(char.IsWhiteSpace) || columnName.Contains(";") || columnName.Contains("'") || columnName.Contains("\"") || columnName.Contains("--"))
            {
                CoreLogger.LogWarning($"[SchemaManager] Column name '{columnName}' for table '{tableName}' contains invalid characters (pre-check).");
                return false;
            }

            return tableSchema.Columns.Any(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        }



        public IEnumerable<string> GetAllTableCreateQueries()
        {
            return m_TableSchemas.Values.Select(ts => ts.CreateQuery);
        }

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