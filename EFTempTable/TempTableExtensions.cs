using System;
using System.Data.Entity;
using System.Linq;

namespace EFTempTable
{
    public static class TempTableExtensions
    {
        /// <summary>
        /// Use this function to automagically register all temp tables for the context. This can be used or you can manually add the classes to the dbset on the context.
        /// </summary>
        /// <param name="dbModelBuilder"></param>
        public static void RegisterTempTables(DbModelBuilder dbModelBuilder)
        {
            var entityMethod = typeof(DbModelBuilder).GetMethod("Entity");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    var attribs = type.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), false);
                    if (attribs != null && attribs.Length > 0)
                    {
                        var tname = attribs[0] as System.ComponentModel.DataAnnotations.Schema.TableAttribute;
                        if (tname.Name.StartsWith("#"))
                        {
                            entityMethod.MakeGenericMethod(type).Invoke(dbModelBuilder, new object[] { });
                        }
                    }
                }
            }

        }

        public static ITempTable<INTYPE> ToTempTable<INTYPE>(this IQueryable query) where INTYPE : class
        {
            var t = new TempTable<INTYPE>();
            t.AppendData(query);
            return t;
        }
    }
}
