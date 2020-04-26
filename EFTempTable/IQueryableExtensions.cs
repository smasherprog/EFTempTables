using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; 
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Reflection; 

namespace EFTempTable
{
    public static class IQueryableExtensions
    {
        internal static ObjectQuery GetObjectQuery(this IQueryable query)
        {
            object internalQuery = GetField(query, "_internalQuery");

            ObjectQuery objectQuery = GetField(internalQuery, "_objectQuery") as ObjectQuery;

            return objectQuery;
        }

        internal static IReadOnlyDictionary<string, int> GetQueryPropertyPositions(this ObjectQuery objectQuery)
        {
            var propertyPositions = new Dictionary<string, int>();

            // Get the query state.
            object objectQueryState = GetProperty(objectQuery, "QueryState");

            // Get the cached query execution plan.
            object cachedPlan = GetField(objectQueryState, "_cachedPlan");

            // Get the command definition.
            object commandDefinition = GetField(cachedPlan, "CommandDefinition");

            // Get the column map generator.
            Array columnMapGenerators = GetField(commandDefinition, "_columnMapGenerators") as Array;
            object columnMapGenerator = ((columnMapGenerators != null) && (columnMapGenerators.Length == 1)) ? columnMapGenerators.GetValue(0) : null;

            // Get the column map.
            object columnMap = GetField(columnMapGenerator, "_columnMap");

            // get the record column map.
            object columnMapElement = GetProperty(columnMap, "Element");

            // Get column map properties.
            Array properties = GetProperty(columnMapElement, "Properties") as Array;
            if (properties != null)
            {
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    object property = properties.GetValue(propertyIndex);
                    propertyPositions.Add(GetProperty(property, "Name") as string, (int)GetProperty(property, "ColumnPos"));
                }
            }

            return new ReadOnlyDictionary<string, int>(propertyPositions);
        }

        private static object GetProperty(object objectInstance, string propertyName)
        {
            object propertyValue = null;

            if (objectInstance != null)
            {
                var property = objectInstance.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null)
                {
                    propertyValue = property.GetValue(objectInstance, new object[0]);
                }
            }

            return propertyValue;
        }

        private static object GetField(object objectInstance, string fieldName)
        {
            object fieldValue = null;

            if (objectInstance != null)
            {
                var field = objectInstance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null)
                {
                    fieldValue = field.GetValue(objectInstance);
                }
            }

            return fieldValue;
        }

    }
}
