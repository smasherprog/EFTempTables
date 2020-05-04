using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace EFTempTable
{
    public interface ITempTable<T> : IDisposable where T : class
    {
        IQueryable<T> ToEFTable();
        void AppendData(IQueryable query);
    }

    public class TempTable<T> : ITempTable<T> where T : class
    {
        private static Dictionary<Type, MappingFragment> TableNameMapping = new Dictionary<Type, MappingFragment>();
        private bool Disposed = false;
        private ObjectContext ObjectContext = null;
        private string TableName = string.Empty;

        private bool TableCreated => !string.IsNullOrWhiteSpace(TableName);

        public void AppendData(IQueryable query)
        {
            var temporarySnapshotObjectQuery = query.GetObjectQuery(); 
            this.ObjectContext = temporarySnapshotObjectQuery.Context;
            var desc = GetEntityDescription<T>(temporarySnapshotObjectQuery);
            if (desc == null)
            {
                throw new ArgumentOutOfRangeException($"The type {typeof(T)} is not a part of the dbcontext. Add the code to your context \n  public DbSet<{typeof(T).Name}> {typeof(T).Name}s {{ get; set; }}\n");
            }

            var temporarySnapshotColumns = desc.PropertyMappings.OfType<ScalarPropertyMapping>().ToDictionary(p => p.Property.Name, p => p.Column);
            if ((temporarySnapshotObjectQuery != null) && temporarySnapshotColumns.Any())
            {
                var temporarySnapshotQuerySql = temporarySnapshotObjectQuery.ToTraceString();
                var temporarySnapshotObjectQueryColumnsPositions = temporarySnapshotObjectQuery.GetQueryPropertyPositions().OrderBy(cp => cp.Value);
                foreach (var item in temporarySnapshotColumns)
                {
                    if (temporarySnapshotObjectQueryColumnsPositions.All(a => a.Key != item.Key))
                    {
                        throw new ArgumentOutOfRangeException($"You must set a value for the property: {item.Key}. If not value is present, then set it to null. Temp tables require all values to be present");
                    }
                }
                var temporarySnapshotCreateColumnsListBuilder = new StringBuilder();
                var temporarySnapshotFillColumnsListBuilder = new StringBuilder();
                foreach (KeyValuePair<string, int> temporarySnapshotObjectQueryColumnPosition in temporarySnapshotObjectQueryColumnsPositions)
                {
                    EdmProperty temporarySnapshotColumn = temporarySnapshotColumns[temporarySnapshotObjectQueryColumnPosition.Key];
                    if (!TableCreated)
                    {
                        temporarySnapshotCreateColumnsListBuilder.Append(GetTemporarySnapshotColumnCreateSql(temporarySnapshotColumn));
                    }
                    temporarySnapshotFillColumnsListBuilder.AppendFormat("[{0}],", temporarySnapshotColumn.Name);
                }
                if (!TableCreated)
                {
                    temporarySnapshotCreateColumnsListBuilder.Length -= 1;
                }
                temporarySnapshotFillColumnsListBuilder.Length -= 1;

                // We need to handle "1 AS [C1]" column here
                if (temporarySnapshotObjectQueryColumnsPositions.First().Value == 1)
                {
                    if (!TableCreated)
                    {
                        temporarySnapshotCreateColumnsListBuilder.Insert(0, "[RESERVED_EF_INTERNAL] INT,");
                    }
                    temporarySnapshotFillColumnsListBuilder.Insert(0, "[RESERVED_EF_INTERNAL],");
                }

                string temporarySnapshotFillSqlCommand = string.Format("INSERT INTO {0}({1}) (SELECT * FROM ({2}) AS [TemporarySnapshotQueryable])", desc.StoreEntitySet.Table, temporarySnapshotFillColumnsListBuilder, temporarySnapshotQuerySql);
                var temporarySnapshotFillSqlCommandParameters = new List<SqlParameter>();
                foreach (var item in temporarySnapshotObjectQuery.Parameters)
                {
                    var t = new SqlParameter(item.Name, item.Value);
                    t.IsNullable = Nullable.GetUnderlyingType(item.ParameterType) != null;
                    if (t.IsNullable && item.Value == null)
                    {
                        t.Value = DBNull.Value;
                    }

                    t.SqlDbType = SqlHelper.GetDbType(item.ParameterType);
                    temporarySnapshotFillSqlCommandParameters.Add(t);
                }

                if (ObjectContext.Connection.State != ConnectionState.Open)
                {
                    ObjectContext.Connection.Open();
                }
                if (!TableCreated)
                {
                    string temporarySnapshotCreateSqlCommand = string.Format("IF OBJECT_ID('tempdb..{0}') IS NOT NULL BEGIN DROP TABLE {0} END{1}CREATE TABLE {0} ({2})", desc.StoreEntitySet.Table, Environment.NewLine, temporarySnapshotCreateColumnsListBuilder);
                    ObjectContext.ExecuteStoreCommand(temporarySnapshotCreateSqlCommand);
                    TableName = desc.StoreEntitySet.Table;
                }

                ObjectContext.ExecuteStoreCommand(temporarySnapshotFillSqlCommand, temporarySnapshotFillSqlCommandParameters.ToArray());
            }
            else
            {
                throw new ArgumentOutOfRangeException($"The type {typeof(T)} is not a part of the dbcontext. Add the code to your context \n  public DbSet<{typeof(T).Name}> {typeof(T).Name}s {{ get; set; }}\n");
            }
        }

        public IQueryable<T> ToEFTable() 
        {
            return this.ObjectContext.CreateObjectSet<T>().AsNoTracking();
        }

        private string GetTemporarySnapshotColumnCreateSql(EdmProperty temporarySnapshotColumn)
        {
            string typeNameUpperCase = temporarySnapshotColumn.TypeName.ToUpperInvariant();
            string temporarySnapshotColumnCreateSqlSuffix = string.Empty;
            if (temporarySnapshotColumn.Nullable)
            {
                temporarySnapshotColumnCreateSqlSuffix += " NULL,";
            }
            else
            {
                temporarySnapshotColumnCreateSqlSuffix += " NOT NULL,";
            }

            switch (typeNameUpperCase)
            {
                case "NUMERIC":
                case "DECIMAL":
                    return string.Format("[{0}] NUMERIC({1},{2}){3}", temporarySnapshotColumn.Name, temporarySnapshotColumn.Precision, temporarySnapshotColumn.Scale, temporarySnapshotColumnCreateSqlSuffix);
                case "NVARCHAR":
                case "VARCHAR":
                    return string.Format("[{0}] {1}({2}){3}", temporarySnapshotColumn.Name, typeNameUpperCase, temporarySnapshotColumn.MaxLength, temporarySnapshotColumnCreateSqlSuffix);
                default:
                    return string.Format("[{0}] {1}{2}", temporarySnapshotColumn.Name, typeNameUpperCase, temporarySnapshotColumnCreateSqlSuffix);
            }
        }

        private static class SqlHelper
        {
            private static Dictionary<Type, SqlDbType> typeMap;

            // Create and populate the dictionary in the static constructor
            static SqlHelper()
            {
                typeMap = new Dictionary<Type, SqlDbType>();
                typeMap[typeof(string)] = SqlDbType.NVarChar;
                typeMap[typeof(char[])] = SqlDbType.NVarChar;
                typeMap[typeof(byte)] = SqlDbType.TinyInt;
                typeMap[typeof(short)] = SqlDbType.SmallInt;
                typeMap[typeof(int)] = SqlDbType.Int;
                typeMap[typeof(long)] = SqlDbType.BigInt;
                typeMap[typeof(byte[])] = SqlDbType.Image;
                typeMap[typeof(bool)] = SqlDbType.Bit;
                typeMap[typeof(DateTime)] = SqlDbType.DateTime2;
                typeMap[typeof(DateTimeOffset)] = SqlDbType.DateTimeOffset;
                typeMap[typeof(decimal)] = SqlDbType.Money;
                typeMap[typeof(float)] = SqlDbType.Real;
                typeMap[typeof(double)] = SqlDbType.Float;
                typeMap[typeof(TimeSpan)] = SqlDbType.Time;
            }

            public static SqlDbType GetDbType(Type giveType)
            {
                // Allow nullable types to be handled
                giveType = Nullable.GetUnderlyingType(giveType) ?? giveType;
                if (giveType.IsEnum)
                {
                    giveType = giveType.GetEnumUnderlyingType();
                }

                if (typeMap.ContainsKey(giveType))
                {
                    return typeMap[giveType];
                }

                throw new ArgumentException($"{giveType.FullName} is not a supported .NET class");
            }

            // Generic version
            public static SqlDbType GetDbType<T>()
            {
                return GetDbType(typeof(T));
            }
        }

        private static MappingFragment GetEntityDescription<TEntity>(ObjectQuery objectContext)
        {
            var clrEntityType = typeof(TEntity);
            MappingFragment tablename = null;
            lock (TableNameMapping)
            {
                if (TableNameMapping.TryGetValue(clrEntityType, out tablename))
                {
                    return tablename;
                }
            }

            var metadata = objectContext.Context.MetadataWorkspace;

            // Get the part of the model that contains info about the actual CLR types
            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            // Get the entity type from the model that maps to the CLR type
            var entityType = metadata
                    .GetItems<EntityType>(DataSpace.OSpace)
                          .FirstOrDefault(e => objectItemCollection.GetClrType(e) == clrEntityType);
            if (entityType == null)
            {
                return null;
            }

            // Get the entity set that uses this entity type
            var entitySet = metadata
                .GetItems<EntityContainer>(DataSpace.CSpace)
                      .Single()
                      .EntitySets
                      .Single(s => s.ElementType.Name == entityType.Name);

            // Find the mapping between conceptual and storage model for this entity set
            var mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                .Single()
                .EntitySetMappings
                .Single(s => s.EntitySet == entitySet);

            // Find the storage entity set (table) that the entity is mapped
            var m = mapping
                .EntityTypeMappings.Single()
                .Fragments.Single();

            lock (TableNameMapping)
            {
                if (TableNameMapping.TryGetValue(clrEntityType, out tablename))
                {
                    return tablename;
                }

                tablename = m;
                TableNameMapping.Add(clrEntityType, tablename);
            }

            return tablename;
        }

        public void Dispose()
        {
            if (!this.Disposed && this.TableCreated)
            {
                string temporarySnapshotCreateSqlCommand = $"IF OBJECT_ID('tempdb..{this.TableName}') IS NOT NULL BEGIN DROP TABLE {this.TableName} END";
                ObjectContext.ExecuteStoreCommand(temporarySnapshotCreateSqlCommand);
                this.Disposed = true;
            }
        }
    }
}
