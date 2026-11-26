/* =====================================================================
   Contact form submissions (Mofam)
   Used by: ContactSubmissionService (save) and the "Form Submissions"
   dashboard in the Umbraco backoffice (CSV export).
   Safe to run again: nothing is dropped or duplicated.
   ===================================================================== */

/* 1. Table ---------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ContactSubmissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContactSubmissions
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ContactSubmissions PRIMARY KEY,
        Subject       NVARCHAR(255)  NOT NULL,
        FirstName     NVARCHAR(255)  NOT NULL,
        LastName      NVARCHAR(255)  NOT NULL,
        Email         NVARCHAR(255)  NOT NULL,
        ContactNumber NVARCHAR(255)  NOT NULL,
        Message       NVARCHAR(3000) NOT NULL,
        Culture       NVARCHAR(10)   NULL,      -- en or ar
        CreatedAt     DATETIME2      NOT NULL CONSTRAINT DF_ContactSubmissions_CreatedAt DEFAULT SYSUTCDATETIME(),  -- UTC
        EmailSent     BIT            NOT NULL CONSTRAINT DF_ContactSubmissions_EmailSent DEFAULT 0
    );
END
GO

/* 2. Save an entry. Returns the new Id. ------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_ContactSubmission_Insert
    @Subject       NVARCHAR(255),
    @FirstName     NVARCHAR(255),
    @LastName      NVARCHAR(255),
    @Email         NVARCHAR(255),
    @ContactNumber NVARCHAR(255),
    @Message       NVARCHAR(3000),
    @Culture       NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ContactSubmissions (Subject, FirstName, LastName, Email, ContactNumber, Message, Culture)
    VALUES (@Subject, @FirstName, @LastName, @Email, @ContactNumber, @Message, @Culture);

    SELECT CAST(SCOPE_IDENTITY() AS INT);
END
GO

/* 3. Mark that the email of an entry was sent. ----------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ContactSubmission_MarkEmailSent
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ContactSubmissions SET EmailSent = 1 WHERE Id = @Id;
END
GO

/* 4. Entries for the CSV export, newest first. ----------------------- */
/*    @Culture = NULL gives all; 'en' or 'ar' gives only that language. */
/*    CreatedAt is returned in UAE time (stored in UTC).                */
CREATE OR ALTER PROCEDURE dbo.usp_ContactSubmission_GetAll
    @Culture NVARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Subject, FirstName, LastName, Email, ContactNumber, Message, Culture,
           CAST(CreatedAt AT TIME ZONE 'UTC' AT TIME ZONE 'Arabian Standard Time' AS DATETIME2(0)) AS CreatedAt,
           EmailSent
    FROM dbo.ContactSubmissions
    WHERE @Culture IS NULL OR Culture = @Culture
    ORDER BY Id DESC;
END
GO

/* Test data cleanup (run only when needed):
   DELETE FROM dbo.ContactSubmissions WHERE Subject LIKE 'LOADTEST-%' OR Subject LIKE '%TEST-CSV%';
*/
