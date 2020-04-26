# EFTempTables
Entity Framework Temporary Tables

<p>The purpose of this library is to create a small easy to use way to include temp tables into your EF project.</p>
<p>Below is a simple example of how you can use this library.</p>

```c#
using (var db = new Context())
{
  //Select data from a Databsae Table into a temp table,
  //Then pull the data from the temp table into memory
  //----QUERY 1----
  var st = db.Students.Select(a => new TempStudentTableBase
  {
    FullName = a.FirstMidName + " " + a.LastName,
    ID = a.ID
  })
  .ToTempTable<TempStudentTable, TempStudentTableBase>().ToList();

  //create a temp table but do not pull the data back into memory. It will be used later
  //----QUERY 2----
  var temptable = db.Students.Select(a => new TempStudentTableBase
  {
    FullName = a.FirstMidName + " " + a.LastName,
    ID = a.ID
  })
  .ToTempTable<TempStudentTable, TempStudentTableBase>();

  //Use the temp table to join on the Enrollments table, but also join data from the temp table as well
  var enrolldfg = (from enrol in db.Enrollments
    join tempb in temptable on enrol.StudentID equals tempb.ID
    select new
    {
      enrol,
      tempb
    }).ToList();
  }     
```
<p>The above code will produce this SQL output</p>

```sql
----QUERY 1----
IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
CREATE TABLE #TempStudentTable ([ID] INT NOT NULL PRIMARY KEY CLUSTERED,[FullName] NVARCHAR(MAX) NULL)

INSERT INTO #TempStudentTable([ID],[FullName]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
    
SELECT 
    [Extent1].[ID] AS [ID], 
    [Extent1].[FullName] AS [FullName]
    FROM [dbo].[#TempStudentTable] AS [Extent1]    
    

----QUERY 2----
IF OBJECT_ID('tempdb..#TempStudentTable') IS NOT NULL BEGIN DROP TABLE #TempStudentTable END
CREATE TABLE #TempStudentTable ([ID] INT NOT NULL PRIMARY KEY CLUSTERED,[FullName] NVARCHAR(MAX) NULL)

INSERT INTO #TempStudentTable([ID],[FullName]) (SELECT * FROM (SELECT 
    [Extent1].[ID] AS [ID], 
    CASE WHEN ([Extent1].[FirstMidName] IS NULL) THEN N'' ELSE [Extent1].[FirstMidName] END + N' ' + CASE WHEN ([Extent1].[LastName] IS NULL) THEN N'' ELSE [Extent1].[LastName] END AS [C1]
    FROM [dbo].[Students] AS [Extent1]) AS [TemporarySnapshotQueryable])
    
SELECT 
    [Extent1].[EnrollmentID] AS [EnrollmentID], 
    [Extent1].[CourseID] AS [CourseID], 
    [Extent1].[StudentID] AS [StudentID], 
    [Extent1].[Grade] AS [Grade], 
    [Extent2].[ID] AS [ID], 
    [Extent2].[FullName] AS [FullName]
    FROM  [dbo].[Enrollments] AS [Extent1]
    INNER JOIN [dbo].[#TempStudentTable] AS [Extent2] ON [Extent1].[StudentID] = [Extent2].[ID]
    
```
