-- Create specialized tables
CREATE TABLE IF NOT EXISTS "Stocks" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Symbol" VARCHAR(50) NOT NULL,
    "Quantity" DECIMAL(18,4) NOT NULL,
    "BuyPrice" DECIMAL(18,2) NOT NULL,
    "CurrentPrice" DECIMAL(18,2) NOT NULL,
    "PurchaseDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UserId" VARCHAR(450)
);

CREATE TABLE IF NOT EXISTS "MutualFunds" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Symbol" VARCHAR(50),
    "FolioNumber" VARCHAR(100),
    "Units" DECIMAL(18,4) NOT NULL,
    "AvgNAV" DECIMAL(18,4) NOT NULL,
    "CurrentNAV" DECIMAL(18,4) NOT NULL,
    "PurchaseDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UserId" VARCHAR(450)
);

CREATE TABLE IF NOT EXISTS "FixedDeposits" (
    "Id" SERIAL PRIMARY KEY,
    "BankName" VARCHAR(200) NOT NULL,
    "CertificateNumber" VARCHAR(100),
    "PrincipalAmount" DECIMAL(18,2) NOT NULL,
    "InterestRate" DECIMAL(18,2) NOT NULL,
    "MaturityDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "PurchaseDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UserId" VARCHAR(450)
);

-- Migrate data from Investments
-- Assuming IDs 1=Stocks, 2=Mutual Funds, 3=Fixed Deposits based on previous seeding
INSERT INTO "Stocks" ("Name", "Symbol", "Quantity", "BuyPrice", "CurrentPrice", "PurchaseDate", "UserId")
SELECT "Name", "Symbol", "Quantity", "BuyPrice", "CurrentPrice", "PurchaseDate", "UserId"
FROM "Investments"
WHERE "InvestmentTypeId" = (SELECT "Id" FROM "InvestmentTypes" WHERE "Name" = 'Stocks');

INSERT INTO "MutualFunds" ("Name", "Symbol", "Units", "AvgNAV", "CurrentNAV", "PurchaseDate", "UserId")
SELECT "Name", "Symbol", "Quantity", "BuyPrice", "CurrentPrice", "PurchaseDate", "UserId"
FROM "Investments"
WHERE "InvestmentTypeId" = (SELECT "Id" FROM "InvestmentTypes" WHERE "Name" = 'Mutual Funds');

INSERT INTO "FixedDeposits" ("BankName", "PrincipalAmount", "InterestRate", "MaturityDate", "PurchaseDate", "UserId")
SELECT "Name", "BuyPrice", COALESCE("InterestRate", 0), COALESCE("MaturityDate", "PurchaseDate"), "PurchaseDate", "UserId"
FROM "Investments"
WHERE "InvestmentTypeId" = (SELECT "Id" FROM "InvestmentTypes" WHERE "Name" = 'Fixed Deposits');
