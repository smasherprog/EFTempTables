using EFTempTable;
using System.Collections.Generic;
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
                //Select data from a Databsae Table into a temp table,
                //Then pull the data from the temp table into memory
                //----QUERY 1----
                var st = db.Students.Select(a => new
                {
                    FullName = a.FirstMidName + " " + a.LastName,
                    a.ID,
                    FirstLetterLastName = a.LastName.Substring(0, 1),
                    Numbers = a.EnrollmentDate.Month
                })
                .ToTempTable<TempStudentTable>();

                //create a temp table but do not pull the data back into memory. It will be used later
                var temptable = db.Students.Select(a => new
                {
                    FullName = a.FirstMidName + " " + a.LastName,
                    a.ID,
                    FirstLetterLastName = a.LastName.Substring(0, 1),
                    Numbers = a.EnrollmentDate.Month
                })
               .ToTempTable<TempStudentTable>().ToEFTable();

                //Use the temp table to join on the Enrollments table, but also join data from the temp table as well
                var enrollment2 = (from enrol in db.Enrollments
                                   join tempb in temptable on enrol.StudentID equals tempb.ID
                                   select new
                                   {
                                       enrol,
                                       tempb
                                   }).ToList();

                //when dispose is called, the temp table will be dropped. Otherwise the temp table will exist until the underlying context is disposed
                using (var disposeteblewhendone = db.Students.Select(a => new
                {
                    FullName = a.FirstMidName + " " + a.LastName,
                    ID = a.ID,
                    FirstLetterLastName = a.LastName.Substring(0, 1),
                    Numbers = a.EnrollmentDate.Month
                }).ToTempTable<TempStudentTable>())
                {
                    var tablelist = disposeteblewhendone.ToEFTable().ToList();
                }

                //when dispose is called, the temp table will be dropped. Otherwise the temp table will exist until the underlying context is disposed
                using (var disposeteblewhendone = db.Students.Select(a => new TempStudentTableBase
                {
                    FullName = a.FirstMidName + " " + a.LastName,
                    ID = a.ID,
                    FirstLetterLastName = a.LastName.Substring(0, 1),
                    Numbers = a.EnrollmentDate.Month
                }).ToTempTable<TempStudentTable>())
                {
                    //add more data from some other iqueryable
                    disposeteblewhendone.AppendData(db.Students.Select(a => new
                    {
                        FullName = a.FirstMidName + " " + a.LastName,
                        ID = a.ID,
                        FirstLetterLastName = a.LastName.Substring(0, 1),
                        Numbers = a.EnrollmentDate.Month
                    }));
                    var tablelist = disposeteblewhendone.ToEFTable().ToList();
                    int k = 6;
                }
            }
        }
    }
}
