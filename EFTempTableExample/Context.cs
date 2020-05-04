using EFTempTable;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;

namespace EFTempTableExample
{
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

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            TempTableExtensions.RegisterTempTables(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
    }
}
