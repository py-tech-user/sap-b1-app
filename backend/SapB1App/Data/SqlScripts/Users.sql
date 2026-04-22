IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL,
        PasswordHash NVARCHAR(512) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        SapSalesPersonCode INT NOT NULL
    );

    CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username);
    CREATE UNIQUE INDEX IX_Users_SapSalesPersonCode ON dbo.Users (SapSalesPersonCode);
END;
GO

INSERT INTO dbo.Users (Username, PasswordHash, FullName, SapSalesPersonCode)
VALUES
('karim', 'REPLACE_WITH_HASH', 'EL BADAOUI Karim', 1),
('tarik', 'REPLACE_WITH_HASH', 'TARIK', 2),
('dacosta', 'REPLACE_WITH_HASH', 'Dacosta Mohamed', 3),
('hakim', 'REPLACE_WITH_HASH', 'EL BADAOUI Hakim', 4);
GO
