-- Apply after 001. Existing stateless JWTs must be replaced by a new login.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.AuthenticationUsers', N'U') IS NULL
    CREATE TABLE dbo.AuthenticationUsers (
        UserId nvarchar(200) NOT NULL PRIMARY KEY,
        UserJson nvarchar(max) NOT NULL,
        Enabled bit NOT NULL);
IF OBJECT_ID(N'dbo.AuthenticationSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuthenticationSessions (
        SessionId varchar(32) NOT NULL PRIMARY KEY,
        UserId nvarchar(200) NOT NULL REFERENCES dbo.AuthenticationUsers(UserId),
        RefreshTokenId varchar(32) NOT NULL,
        ExpiresUtc datetime2 NOT NULL,
        Revoked bit NOT NULL DEFAULT 0);
    CREATE INDEX IX_AuthenticationSessions_Expiry ON dbo.AuthenticationSessions(ExpiresUtc);
END;
COMMIT;
