/* =====================================================================
   Newsletter subscribers (Mofam)
   Used by: NewsletterSubscriptionService (save) and the "Form Submissions"
   dashboard in the Umbraco backoffice (CSV export).
   One row for each email: a second subscription with the same email
   (any capital letters) does not add a row.
   Safe to run again: nothing is dropped or duplicated.
   ===================================================================== */

/* 1. Table ---------------------------------------------------------- */
IF OBJECT_ID(N'dbo.NewsletterSubscribers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NewsletterSubscribers
    (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewsletterSubscribers PRIMARY KEY,
        Email     NVARCHAR(255) NOT NULL CONSTRAINT UQ_NewsletterSubscribers_Email UNIQUE,   -- one row for each email
        Culture   NVARCHAR(10)  NULL,      -- en or ar (the language of the first subscription)
        CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_NewsletterSubscribers_CreatedAt DEFAULT SYSUTCDATETIME(),  -- UTC
        EmailSent BIT           NOT NULL CONSTRAINT DF_NewsletterSubscribers_EmailSent DEFAULT 0
    );
END
GO

/* 2. Save a subscriber. ---------------------------------------------- */
/*    Returns one row: Id, and IsNew (1 = added now, 0 = the email was   */
/*    already there, so nothing was added and no email should be sent).  */
CREATE OR ALTER PROCEDURE dbo.usp_NewsletterSubscriber_Insert
    @Email   NVARCHAR(255),
    @Culture NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        INSERT INTO dbo.NewsletterSubscribers (Email, Culture)
        VALUES (@Email, @Culture);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id, CAST(1 AS BIT) AS IsNew;
    END TRY
    BEGIN CATCH
        -- 2627 / 2601: the email is already in the table (also when two requests come at the same time).
        IF ERROR_NUMBER() IN (2627, 2601)
            SELECT Id, CAST(0 AS BIT) AS IsNew FROM dbo.NewsletterSubscribers WHERE Email = @Email;
        ELSE
            THROW;
    END CATCH
END
GO

/* 3. Mark that the emails of a subscriber were sent. ----------------- */
CREATE OR ALTER PROCEDURE dbo.usp_NewsletterSubscriber_MarkEmailSent
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.NewsletterSubscribers SET EmailSent = 1 WHERE Id = @Id;
END
GO

/* 4. Subscribers for the CSV export, newest first. ------------------- */
/*    @Culture = NULL gives all; 'en' or 'ar' gives only that language. */
/*    CreatedAt is returned in UAE time (stored in UTC).                */
CREATE OR ALTER PROCEDURE dbo.usp_NewsletterSubscriber_GetAll
    @Culture NVARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Email, Culture,
           CAST(CreatedAt AT TIME ZONE 'UTC' AT TIME ZONE 'Arabian Standard Time' AS DATETIME2(0)) AS CreatedAt,
           EmailSent
    FROM dbo.NewsletterSubscribers
    WHERE @Culture IS NULL OR Culture = @Culture
    ORDER BY Id DESC;
END
GO

/* Test data cleanup (run only when needed):
   DELETE FROM dbo.NewsletterSubscribers WHERE Email LIKE '%@example.com';
*/
