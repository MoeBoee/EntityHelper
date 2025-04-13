using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityHelper
{
    /// <summary>
    /// DB_Migrate is an Tool to Udpdate an DB in DB-First Environments
    /// </summary>
    public class DB_Migrate
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbContext"></param>
        public DB_Migrate(DbContext dbContext)
        {

            DbContext = dbContext;
            init();
            
            //Up(5, 6, "ALTER TABLE [dbo].[tbl_serviceType]\r\nADD [serviceTypeTagId] int NULL;");
        }

        /// <summary>
        /// Name des DB-Contextes
        /// </summary>
        public String Name { get => DbContext.GetType().Name; }

        /// <summary>
        /// Typ es DB-Contextes
        /// </summary>
        public DbContext DbContext { get; private set; }

        /// <summary>
        /// Führt ein Datenbank-Upgrade von einer bestimmten Version auf eine neue Version durch,
        /// indem eine Reihe von SQL-Befehlen ausgeführt wird. Die Ausführung erfolgt innerhalb 
        /// einer Transaktion und wird bei einem Fehler vollständig zurückgesetzt.
        /// </summary>
        /// <param name="Version">Die aktuelle erwartete Datenbankversion.</param>
        /// <param name="NewVersion">Die Zielversion, auf die aktualisiert werden soll.</param>
        /// <param name="Statement">SQL-Befehle, getrennt durch "GO", die während des Upgrades ausgeführt werden.</param>
        /// <exception cref="Exception">Wird ausgelöst, wenn das Upgrade fehlschlägt. </exception>
        public void Up(int Version, int NewVersion, string Statement)
        {
            using (DbContext c = DbContext)
            {
                if (Version != GetDBVersion()) return;
                using (var dbContextTransaction = c.Database.BeginTransaction())
                {
                    try
                    {
                        var splits = Statement.Split(new string[] { "GO" }, StringSplitOptions.None);
                        foreach (var split in splits)
                        {
                            if (!string.IsNullOrEmpty(split)) c.Database.ExecuteSqlCommand(split);
                        }

                        //c.Database.ExecuteSqlCommand("DELETE FROM tbl_person WHERE Id = @p0", 5);
                        SetDBVersion(NewVersion);
                        dbContextTransaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            dbContextTransaction.Rollback();
                        }
                        catch { }

                        SetDBVersion(Version);
                        throw new Exception($"DBMigration to Version {NewVersion} failed", ex);
                    }
                }
            }
        }

       
        private void init()
        {
            using (DbContext c = DbContext)
            {
                using (var dbContextTransaction = c.Database.BeginTransaction())
                {
                    try
                    {

                        var exsists = c.Database.SqlQuery<bool>($"DECLARE @TableExists BIT;\r\n\r\nIF EXISTS (\r\n    SELECT 1\r\n    FROM INFORMATION_SCHEMA.TABLES\r\n    WHERE TABLE_SCHEMA = 'dbo'\r\n      AND TABLE_NAME = 'tbl_dbVersion'\r\n)\r\nBEGIN\r\n    SET @TableExists = 1;\r\nEND\r\nELSE\r\nBEGIN\r\n    SET @TableExists = 0;\r\nEND\r\n\r\nSELECT @TableExists AS TableExists;").First();
                        if (!exsists) c.Database.ExecuteSqlCommand("CREATE TABLE [dbo].[tbl_dbVersion](\r\n\t[contextName] [nvarchar](50) NOT NULL,\r\n\t[version] [int](50) NOT NULL,\r\n CONSTRAINT [PK_tbl_dbVersion] PRIMARY KEY CLUSTERED \r\n(\r\n\t[contextName] ASC\r\n)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]\r\n) ON [PRIMARY]");
                        //c.Database.ExecuteSqlCommand("DELETE FROM tbl_person WHERE Id = @p0", 5);

                        dbContextTransaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        dbContextTransaction.Rollback();
                        Console.WriteLine("Transaction failed: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the DB-Version als int
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public int? GetDBVersion()
        {
            using (DbContext c = DbContext)
            {

                try
                {
                    var result = c.Database.SqlQuery<int>($"SELECT version FROM tbl_dbVersion where contextName = @p0", Name).First();

                    return result;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to get Version from {Name}", ex);
                }
                
            }
        }

        private void SetDBVersion(int version)
        {
            using (DbContext c = DbContext)
            {

                try
                {
                    var result = c.Database.ExecuteSqlCommand($"IF EXISTS (SELECT 1 FROM [dbo].[tbl_dbVersion] WHERE [contextName] = @p0)\r\nBEGIN\r\n    UPDATE [dbo].[tbl_dbVersion]\r\n    SET [version] = @p1\r\n    WHERE [contextName] = @p0;\r\nEND\r\nELSE\r\nBEGIN\r\n    INSERT INTO [dbo].[tbl_dbVersion] ([contextName], [version])\r\n    VALUES (@p0, @p1);\r\nEND", Name, version);

                }
                catch (Exception ex)
                {

                    throw ex;
                }

            }
        }
    }
}
