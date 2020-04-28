using System;
using System.Collections.Generic;
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
    public static class DbContextExtensions
    {
        private static string GetTemporarySnapshotColumnCreateSql(EdmProperty temporarySnapshotColumn)
        {
            string typeNameUpperCase = temporarySnapshotColumn.TypeName.ToUpperInvariant();
            string temporarySnapshotColumnCreateSqlSuffix = string.Empty;
            if (temporarySnapshotColumn.Nullable)
            {
                temporarySnapshotColumnCreateSqlSuffix += " NULL";
            }
            else
            {
                temporarySnapshotColumnCreateSqlSuffix += " NOT NULL";
            }

            if (temporarySnapshotColumn.StoreGeneratedPattern == StoreGeneratedPattern.Identity)
            {
                temporarySnapshotColumnCreateSqlSuffix += " PRIMARY KEY CLUSTERED";
            }

            temporarySnapshotColumnCreateSqlSuffix += ",";

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

        private static Dictionary<Type, MappingFragment> TableNameMapping = new Dictionary<Type, MappingFragment>();

        private static MappingFragment GetEntityDescription<TEntity>(ObjectQuery objectContext)
        {
            var clrEntityType = typeof(TEntity);
            MappingFragment tablename = null;
            if (TableNameMapping.TryGetValue(clrEntityType, out tablename))
            {
                return tablename;
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

        public static IQueryable<TTemporaryEntity> ToTempTable<TTemporaryEntity, INTYPE>(this IQueryable<INTYPE> query) where TTemporaryEntity : class, INTYPE
        {
            var temporarySnapshotObjectQuery = query.GetObjectQuery(); 
            var desc = GetEntityDescription<TTemporaryEntity>(temporarySnapshotObjectQuery);
            if (desc == null)
            {
                throw new ArgumentOutOfRangeException($"The type {typeof(TTemporaryEntity)} is not a part of the dbcontext. Add the code to your context \n  public DbSet<{typeof(TTemporaryEntity).Name}> {typeof(TTemporaryEntity).Name}s {{ get; set; }}\n");
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
                        throw new ArgumentOutOfRangeException($"You must set a value for the property: {item.Key} in the class {typeof(INTYPE)}. If not value is present, then set it to null. Temp tables require all values to be present");
                    }
                }
                var temporarySnapshotCreateColumnsListBuilder = new StringBuilder();
                var temporarySnapshotFillColumnsListBuilder = new StringBuilder();
                foreach (KeyValuePair<string, int> temporarySnapshotObjectQueryColumnPosition in temporarySnapshotObjectQueryColumnsPositions)
                {
                    EdmProperty temporarySnapshotColumn = temporarySnapshotColumns[temporarySnapshotObjectQueryColumnPosition.Key];

                    temporarySnapshotCreateColumnsListBuilder.Append(GetTemporarySnapshotColumnCreateSql(temporarySnapshotColumn));
                    temporarySnapshotFillColumnsListBuilder.AppendFormat("[{0}],", temporarySnapshotColumn.Name);
                }

                temporarySnapshotCreateColumnsListBuilder.Length -= 1;
                temporarySnapshotFillColumnsListBuilder.Length -= 1;

                // We need to handle "1 AS [C1]" column here
                if (temporarySnapshotObjectQueryColumnsPositions.First().Value == 1)
                {
                    temporarySnapshotCreateColumnsListBuilder.Insert(0, "[RESERVED_EF_INTERNAL] INT,");
                    temporarySnapshotFillColumnsListBuilder.Insert(0, "[RESERVED_EF_INTERNAL],");
                }

                string temporarySnapshotCreateSqlCommand = string.Format("IF OBJECT_ID('tempdb..{0}') IS NOT NULL BEGIN DROP TABLE {0} END{1}CREATE TABLE {0} ({2})", desc.StoreEntitySet.Table, Environment.NewLine, temporarySnapshotCreateColumnsListBuilder);
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
                 
                if (temporarySnapshotObjectQuery.Context.Connection.State != ConnectionState.Open)
                {
                    temporarySnapshotObjectQuery.Context.Connection.Open();
                }
                temporarySnapshotObjectQuery.Context.ExecuteStoreCommand(temporarySnapshotCreateSqlCommand);
                temporarySnapshotObjectQuery.Context.ExecuteStoreCommand(temporarySnapshotFillSqlCommand, temporarySnapshotFillSqlCommandParameters.ToArray());
                return temporarySnapshotObjectQuery.Context.CreateObjectSet<TTemporaryEntity>().AsNoTracking().AsQueryable();
            }

            return new List<TTemporaryEntity>().AsQueryable();
        }
    }
}
