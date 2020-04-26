using EFTempTable;
using System.Data.Entity;
using System.Linq;

namespace EFTempTableExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Database.SetInitializer(new DBSeed());

            using (var db = new Context())
            {
                var st = db.Students.Select(a => new TempStudentTableBase
                {
                    FullName = a.FirstMidName + " " + a.LastName,
                    ID = a.ID
                })
                .ToScopedTempTable<TempStudentTable, TempStudentTableBase>().ToList();

                int k = 6;
            }
        }
    }
}
