-- ============================================================================
-- DataGuard Synthetic Seed Generator
-- Script: db/seed/001_seed_databases.sql
-- Description: Deterministically generates 100k+ rows for LegacyDB & MigratedDB
--              with precise, documented injected discrepancies.
-- ============================================================================

-- Create LegacyDB
IF DB_ID('LegacyDB') IS NULL CREATE DATABASE LegacyDB;
GO

-- Create MigratedDB
IF DB_ID('MigratedDB') IS NULL CREATE DATABASE MigratedDB;
GO

-- ----------------------------------------------------------------------------
-- 1. SETUP LEGACY DATABASE
-- ----------------------------------------------------------------------------
USE LegacyDB;
GO

IF OBJECT_ID('dbo.Invoices', 'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DROP TABLE dbo.Customers;
GO

CREATE TABLE dbo.Customers (
    CustomerId INT NOT NULL PRIMARY KEY CLUSTERED,
    CustomerCode VARCHAR(20) NOT NULL UNIQUE,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    Phone VARCHAR(25) NOT NULL,
    Status VARCHAR(20) NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL
);

CREATE TABLE dbo.Orders (
    OrderId INT NOT NULL PRIMARY KEY CLUSTERED,
    CustomerId INT NOT NULL,
    OrderDate DATETIME2(3) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status VARCHAR(20) NOT NULL,
    ShippingAddress NVARCHAR(200) NOT NULL
);

CREATE TABLE dbo.OrderItems (
    OrderItemId INT NOT NULL PRIMARY KEY CLUSTERED,
    OrderId INT NOT NULL,
    ProductCode VARCHAR(50) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    LineTotal DECIMAL(18,2) NOT NULL
);

CREATE TABLE dbo.Invoices (
    InvoiceId INT NOT NULL PRIMARY KEY CLUSTERED,
    OrderId INT NOT NULL,
    InvoiceDate DATETIME2(3) NOT NULL,
    AmountDue DECIMAL(18,2) NOT NULL,
    IsPaid BIT NOT NULL
);
GO

-- Fast High-Volume Deterministic Tally Generator
PRINT 'Generating 100,000 Customers for LegacyDB...';
SET NOCOUNT ON;

;WITH Tally AS (
    SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
    FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c
)
INSERT INTO dbo.Customers (CustomerId, CustomerCode, FirstName, LastName, Email, Phone, Status, CreatedAt)
SELECT 
    N AS CustomerId,
    'CUST-' + RIGHT('0000000' + CAST(N AS VARCHAR(10)), 7) AS CustomerCode,
    'First_' + CAST((N % 500) AS NVARCHAR(20)),
    'Last_' + CAST((N % 1000) AS NVARCHAR(20)),
    'user_' + CAST(N AS NVARCHAR(20)) + '@example.com',
    '+1-555-' + RIGHT('0000' + CAST((N % 10000) AS VARCHAR(10)), 4),
    CASE (N % 4) WHEN 0 THEN 'Active' WHEN 1 THEN 'Active' WHEN 2 THEN 'Inactive' ELSE 'Pending' END,
    DATEADD(MINUTE, N % 500000, '2020-01-01T00:00:00')
FROM Tally;
GO

PRINT 'Generating 100,000 Orders for LegacyDB...';
;WITH Tally AS (
    SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
    FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c
)
INSERT INTO dbo.Orders (OrderId, CustomerId, OrderDate, TotalAmount, Status, ShippingAddress)
SELECT 
    N AS OrderId,
    ((N - 1) % 100000) + 1 AS CustomerId,
    DATEADD(MINUTE, N % 400000, '2021-01-01T00:00:00'),
    CAST(50.00 + ((N * 17) % 950) AS DECIMAL(18,2)),
    CASE (N % 5) 
        WHEN 0 THEN 'Pending' 
        WHEN 1 THEN 'Processing' 
        WHEN 2 THEN 'Shipped' 
        WHEN 3 THEN 'Delivered' 
        ELSE 'Cancelled' 
    END,
    CAST(N AS NVARCHAR(20)) + ' Industrial Parkway, Suite ' + CAST((N % 100) AS NVARCHAR(10))
FROM Tally;
GO

PRINT 'Generating 200,000 OrderItems for LegacyDB...';
;WITH Tally AS (
    SELECT TOP (200000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
    FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c
)
INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductCode, Quantity, UnitPrice, LineTotal)
SELECT 
    N AS OrderItemId,
    ((N - 1) / 2) + 1 AS OrderId,
    'PRD-' + RIGHT('0000' + CAST((N % 500) AS VARCHAR(10)), 4),
    (N % 5) + 1,
    CAST(10.00 + ((N * 7) % 100) AS DECIMAL(18,2)),
    CAST(((N % 5) + 1) * (10.00 + ((N * 7) % 100)) AS DECIMAL(18,2))
FROM Tally;
GO

PRINT 'Generating 100,000 Invoices for LegacyDB...';
;WITH Tally AS (
    SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
    FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c
)
INSERT INTO dbo.Invoices (InvoiceId, OrderId, InvoiceDate, AmountDue, IsPaid)
SELECT 
    N AS InvoiceId,
    N AS OrderId,
    DATEADD(DAY, 1, DATEADD(MINUTE, N % 400000, '2021-01-01T00:00:00')),
    CAST(50.00 + ((N * 17) % 950) AS DECIMAL(18,2)),
    CASE WHEN (N % 3) = 0 THEN 0 ELSE 1 END
FROM Tally;
GO

-- ----------------------------------------------------------------------------
-- 2. SETUP MIGRATED DATABASE WITH INJECTED ISSUES
-- ----------------------------------------------------------------------------
USE MigratedDB;
GO

IF OBJECT_ID('dbo.Invoices', 'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DROP TABLE dbo.Customers;
GO

-- Notice SchemaDrift: Phone is VARCHAR(20) instead of VARCHAR(25)
CREATE TABLE dbo.Customers (
    CustomerId INT NOT NULL,
    CustomerCode VARCHAR(20) NOT NULL,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NULL, -- allows null for injection check
    Phone VARCHAR(20) NOT NULL, -- DRIFT: length 20 vs 25 in legacy
    Status VARCHAR(20) NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL
);

CREATE TABLE dbo.Orders (
    OrderId INT NOT NULL,
    CustomerId INT NOT NULL,
    OrderDate DATETIME2(3) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status VARCHAR(20) NOT NULL,
    ShippingAddress NVARCHAR(200) NOT NULL
);

CREATE TABLE dbo.OrderItems (
    OrderItemId INT NOT NULL,
    OrderId INT NOT NULL,
    ProductCode VARCHAR(50) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    LineTotal DECIMAL(18,2) NOT NULL
);

CREATE TABLE dbo.Invoices (
    InvoiceId INT NOT NULL,
    OrderId INT NOT NULL,
    InvoiceDate DATETIME2(3) NOT NULL,
    AmountDue DECIMAL(18,2) NOT NULL,
    IsPaid BIT NOT NULL
);
GO

-- Copy Base Data from LegacyDB
PRINT 'Copying base data into MigratedDB...';
INSERT INTO dbo.Customers (CustomerId, CustomerCode, FirstName, LastName, Email, Phone, Status, CreatedAt)
SELECT CustomerId, CustomerCode, FirstName, LastName, Email, LEFT(Phone, 20), Status, CreatedAt
FROM LegacyDB.dbo.Customers;

INSERT INTO dbo.Orders (OrderId, CustomerId, OrderDate, TotalAmount, Status, ShippingAddress)
SELECT OrderId, CustomerId, OrderDate, TotalAmount, Status, ShippingAddress
FROM LegacyDB.dbo.Orders;

INSERT INTO dbo.OrderItems (OrderItemId, OrderId, ProductCode, Quantity, UnitPrice, LineTotal)
SELECT OrderItemId, OrderId, ProductCode, Quantity, UnitPrice, LineTotal
FROM LegacyDB.dbo.OrderItems;

INSERT INTO dbo.Invoices (InvoiceId, OrderId, InvoiceDate, AmountDue, IsPaid)
SELECT InvoiceId, OrderId, InvoiceDate, AmountDue, IsPaid
FROM LegacyDB.dbo.Invoices;
GO

-- ----------------------------------------------------------------------------
-- INJECT PRECISE GROUND-TRUTH ANOMALIES
-- ----------------------------------------------------------------------------
PRINT 'Injecting ground-truth anomalies into MigratedDB...';

-- Anomaly 1: RowCount Discrepancy (15 missing rows in Orders: 99986..100000)
DELETE FROM MigratedDB.dbo.Orders WHERE OrderId BETWEEN 99986 AND 100000;

-- Anomaly 2: NotNull / Blank Discrepancy (25 customers with NULL or whitespace email: 101..125)
UPDATE MigratedDB.dbo.Customers
SET Email = CASE WHEN CustomerId <= 115 THEN NULL ELSE '   ' END
WHERE CustomerId BETWEEN 101 AND 125;

-- Anomaly 3: DuplicateKey (10 duplicate CustomerCode values: assign CUST-0000001 to customers 201..210)
UPDATE MigratedDB.dbo.Customers
SET CustomerCode = 'CUST-0000001'
WHERE CustomerId BETWEEN 201 AND 210;

-- Anomaly 4: OrphanKey (20 child orders referencing non-existent parent CustomerId 999999: Orders 301..320)
UPDATE MigratedDB.dbo.Orders
SET CustomerId = 999999
WHERE OrderId BETWEEN 301 AND 320;

-- Anomaly 5: ColumnAggregate Discrepancy (TotalAmount altered on 11 orders by +$100.00: Orders 500..510)
UPDATE MigratedDB.dbo.Orders
SET TotalAmount = TotalAmount + 100.00
WHERE OrderId BETWEEN 500 AND 510;

-- Anomaly 6: ValueCheck / Invalid Status (12 orders given invalid status 'INVALID_STATUS': Orders 701..712)
UPDATE MigratedDB.dbo.Orders
SET Status = 'INVALID_STATUS'
WHERE OrderId BETWEEN 701 AND 712;

-- Anomaly 7: Checksum / In-row data corruption (18 order items have UnitPrice modified without updating LineTotal: OrderItems 1001..1018)
UPDATE MigratedDB.dbo.OrderItems
SET UnitPrice = UnitPrice + 5.50
WHERE OrderItemId BETWEEN 1001 AND 1018;

PRINT 'Seed data generation and anomaly injection complete!';
GO
