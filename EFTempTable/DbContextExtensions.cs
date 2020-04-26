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

            if (temporarySnapshotColumn.PrimitiveType.PrimitiveTypeKind == PrimitiveTypeKind.Decimal)
            {
                typeNameUpperCase += $"({temporarySnapshotColumn.Precision},{temporarySnapshotColumn.Scale})";
            }

            temporarySnapshotColumnCreateSqlSuffix += ",";

            switch (typeNameUpperCase)
            {
                case "NUMERIC":
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

        private static IEnumerable<ScalarPropertyMapping> GetEntityPropertyMappings<TEntity>(this ObjectQuery dbContext)
        {
            // Get the metadata
            var metadata = dbContext.Context.MetadataWorkspace;

            // Get the space within the metadata which contains information about CLR types
            var clrSpace = (ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace);

            // Get the entity type from the metadata that maps to the CLR type
            var entityEntityType = metadata.GetItems<EntityType>(DataSpace.OSpace).Single(e => clrSpace.GetClrType(e) == typeof(TEntity));

            // Get the entity set that uses this entity type
            var entityEntitySet = metadata.GetItems<EntityContainer>(DataSpace.CSpace).Single().EntitySets.Single(s => s.ElementType.Name == entityEntityType.Name);

            // Get the mapping between conceptual and storage model for this entity set
            var entityEntitySetMapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace).Single().EntitySetMappings.Single(m => m.EntitySet == entityEntitySet);

            // Get the entity columns
            return entityEntitySetMapping.EntityTypeMappings.Single().Fragments.Single().PropertyMappings.OfType<ScalarPropertyMapping>();
        }

        public static IQueryable<TTemporaryEntity> ToTempTable<TTemporaryEntity, INTYPE>(this IQueryable<INTYPE> query) where TTemporaryEntity : TempTableBase where INTYPE : TempTableBase
        {
            var temporarySnapshotObjectQuery = query.GetObjectQuery();
            var temporarySnapshotColumns = temporarySnapshotObjectQuery.GetEntityPropertyMappings<TTemporaryEntity>().ToDictionary(p => p.Property.Name, p => p.Column);

            if ((temporarySnapshotObjectQuery != null) && temporarySnapshotColumns.Any())
            {
                var temporarySnapshotTableName = "#" + typeof(TTemporaryEntity).Name;
                var temporarySnapshotQuerySql = temporarySnapshotObjectQuery.ToTraceString();
                var temporarySnapshotObjectQueryColumnsPositions = temporarySnapshotObjectQuery.GetQueryPropertyPositions().OrderBy(cp => cp.Value);

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

                string temporarySnapshotCreateSqlCommand = string.Format("IF OBJECT_ID('tempdb..{0}') IS NOT NULL BEGIN DROP TABLE {0} END{1}CREATE TABLE {0} ({2})", temporarySnapshotTableName, Environment.NewLine, temporarySnapshotCreateColumnsListBuilder);
                string temporarySnapshotFillSqlCommand = string.Format("INSERT INTO {0}({1}) (SELECT * FROM ({2}) AS [TemporarySnapshotQueryable])", temporarySnapshotTableName, temporarySnapshotFillColumnsListBuilder, temporarySnapshotQuerySql);
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

                // We are opening connection manually here because since Entity Framework 6 it will not be automatically closed until context disposal - this way the temporary table will be visible for other queries.

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
