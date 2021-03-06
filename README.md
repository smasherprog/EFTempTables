# EFTempTables
Entity Framework Temporary Tables
<p><a href="https://www.nuget.org/packages/EFTempTable/">Nuget Package</a></p>
<p>Supports EF >= 6.1 The purpose of this library is to create a small easy to use way to include temp tables into your EF project.</p>
<p>Below is a simple example of how you can use this library.</p>

```c#

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
                }
            } 
```
<p>The above code will produce this SQL output</p>

```sql
IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
CREATE TABLE #TempStudentTable ([ID] INT NOT NULL,[FullName] NVARCHAR(100) NULL,[FirstLetterLastName] NVARCHAR(1) NULL,[Numbers] NUMERIC(18,2) NOT NULL)

INSERT INTO #TempStudentTable([ID],[FullName],[FirstLetterLastName],[Numbers]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1], 
    SUBSTRING([Extent1].[LastName], 0 + 1, 1) AS [C2], 
    DATEPART (month, [Extent1].[EnrollmentDate]) AS [C3]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
 
 IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
CREATE TABLE #TempStudentTable ([ID] INT NOT NULL,[FullName] NVARCHAR(100) NULL,[FirstLetterLastName] NVARCHAR(1) NULL,[Numbers] NUMERIC(18,2) NOT NULL)
 
 INSERT INTO #TempStudentTable([ID],[FullName],[FirstLetterLastName],[Numbers]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1], 
    SUBSTRING([Extent1].[LastName], 0 + 1, 1) AS [C2], 
    DATEPART (month, [Extent1].[EnrollmentDate]) AS [C3]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
    
 SELECT 
    [Extent1].[EnrollmentID] AS [EnrollmentID], 
    [Extent1].[CourseID] AS [CourseID], 
    [Extent1].[StudentID] AS [StudentID], 
    [Extent1].[Grade] AS [Grade], 
    [Extent2].[ID] AS [ID], 
    [Extent2].[FirstLetterLastName] AS [FirstLetterLastName], 
    [Extent2].[FullName] AS [FullName], 
    [Extent2].[Numbers] AS [Numbers]
    FROM  [dbo].[Enrollments] AS [Extent1]
    INNER JOIN [dbo].[#TempStudentTable] AS [Extent2] ON [Extent1].[StudentID] = [Extent2].[ID]
    
IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
CREATE TABLE #TempStudentTable ([ID] INT NOT NULL,[FullName] NVARCHAR(100) NULL,[FirstLetterLastName] NVARCHAR(1) NULL,[Numbers] NUMERIC(18,2) NOT NULL)

INSERT INTO #TempStudentTable([ID],[FullName],[FirstLetterLastName],[Numbers]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1], 
    SUBSTRING([Extent1].[LastName], 0 + 1, 1) AS [C2], 
    DATEPART (month, [Extent1].[EnrollmentDate]) AS [C3]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
    
 SELECT 
    [Extent1].[ID] AS [ID], 
    [Extent1].[FirstLetterLastName] AS [FirstLetterLastName], 
    [Extent1].[FullName] AS [FullName], 
    [Extent1].[Numbers] AS [Numbers]
    FROM [dbo].[#TempStudentTable] AS [Extent1]
    
IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END

IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
CREATE TABLE #TempStudentTable ([ID] INT NOT NULL,[FullName] NVARCHAR(100) NULL,[FirstLetterLastName] NVARCHAR(1) NULL,[Numbers] NUMERIC(18,2) NOT NULL)

INSERT INTO #TempStudentTable([ID],[FullName],[FirstLetterLastName],[Numbers]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1], 
    SUBSTRING([Extent1].[LastName], 0 + 1, 1) AS [C2], 
     CAST( DATEPART (month, [Extent1].[EnrollmentDate]) AS decimal(19,0)) AS [C3]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
    
INSERT INTO #TempStudentTable([ID],[FullName],[FirstLetterLastName],[Numbers]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1], 
    SUBSTRING([Extent1].[LastName], 0 + 1, 1) AS [C2], 
    DATEPART (month, [Extent1].[EnrollmentDate]) AS [C3]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
    
 SELECT 
    [Extent1].[ID] AS [ID], 
    [Extent1].[FirstLetterLastName] AS [FirstLetterLastName], 
    [Extent1].[FullName] AS [FullName], 
    [Extent1].[Numbers] AS [Numbers]
    FROM [dbo].[#TempStudentTable] AS [Extent1]
    
IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
    
```

<h3>Setup</h3>
<p>See the example for the full setup</p>
<p>Your temporary table must be defined in c# and setup as if it were a real table in sql as far as EF is concerned as Below.</p>
<p>You must reate a regular class that holds your data properties, then create the class which EF will use that contains nothing.</p>
<p>After that, use the temp tables like the examples above. All normal LINQ operations will work on these temp tables!</p>

```c#
// ---------BEGIN -----------
    //
    //              REGULAR POCO CLASSES BEOW WHICH ARE YOUR NORMAL DATA OBJECT IN THE DATABASE
    //              THESE ARE HERE TO SHOW HOW TO USE TEMP TABLES WITH REGULAR TABLES
    public class Course
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CourseID { get; set; }
        public string Title { get; set; }
        public int Credits { get; set; }
        public virtual ICollection<Enrollment> Enrollments { get; set; }
    }

    public enum Grade
    {
        A, B, C, D, F
    }

    public class Enrollment
    {
        public int EnrollmentID { get; set; }
        public int CourseID { get; set; }
        public int StudentID { get; set; }
        public Grade? Grade { get; set; }
        public virtual Course Course { get; set; }
        public virtual Student Student { get; set; }
    }

    public class Student
    {
        public int ID { get; set; }
        public string LastName { get; set; }
        public string FirstMidName { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public virtual ICollection<Enrollment> Enrollments { get; set; }
    }
    // ---------END -----------



    //A Base class is needed for the projection as you cannot project into a table. EF will throw an error
    public class TempStudentTableBase
    {
        public int ID { get; set; }
        [MaxLength(1)]
        public string FirstLetterLastName { get; set; }
        [MaxLength(100)]
        public string FullName { get; set; }
        public decimal Numbers { get; set; }
    }

    // TEMP TABLES MUST BE DEFINED LIKE NORMAL EF CLASSES AND ADDED TO THE DbContext as if they were a real table! 
    //Make sure to name the table with the     HASH 
    [Table("#TempStudentTable")]
    public class TempStudentTable : TempStudentTableBase { }

    public class Context : DbContext
    {
        public Context() : base("Data Source=localhost;Initial Catalog=EFTempTableExampleDb;Integrated Security=SSPI;")
        {
        }
        public DbSet<Student> Students { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Course> Courses { get; set; }

        //USE THE BELOW OR YOU CAN USE the line   TempTableExtensions.RegisterTempTables(modelBuilder);
        ///public DbSet<TempStudentTable> TempStudentTables { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //use this call or, the above    public DbSet<TempStudentTable> TempStudentTables { get; set; }
            TempTableExtensions.RegisterTempTables(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
    }
    
```
