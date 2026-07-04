-- Migrate Investments to use InvestmentType table

-- 1. Create InvestmentTypes table
CREATE TABLE IF NOT EXISTS "InvestmentTypes" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(50) NOT NULL,
    "Icon" VARCHAR(50)
);

-- 2. Seed InvestmentTypes
INSERT INTO "InvestmentTypes" ("Name", "Icon") VALUES 
('Stocks', 'bi-graph-up-arrow'),
('Mutual Funds', 'bi-pie-chart-fill'),
('Fixed Deposits', 'bi-safe2-fill')
ON CONFLICT DO NOTHING;

-- 3. Add InvestmentTypeId to Investments
ALTER TABLE "Investments" ADD COLUMN IF NOT EXISTS "InvestmentTypeId" INTEGER;

-- 4. Map existing 'Type' strings to InvestmentTypeId
UPDATE "Investments" SET "InvestmentTypeId" = (SELECT "Id" FROM "InvestmentTypes" WHERE "Name" = 'Stocks') WHERE "Type" = 'Stock';
UPDATE "Investments" SET "InvestmentTypeId" = (SELECT "Id" FROM "InvestmentTypes" WHERE "Name" = 'Mutual Funds') WHERE "Type" = 'MutualFund';
UPDATE "Investments" SET "InvestmentTypeId" = (SELECT "Id" FROM "InvestmentTypes" WHERE "Name" = 'Fixed Deposits') WHERE "Type" = 'FixedDeposit';

-- 5. Make InvestmentTypeId non-nullable and add FK
ALTER TABLE "Investments" ALTER COLUMN "InvestmentTypeId" SET NOT NULL;
ALTER TABLE "Investments" ADD CONSTRAINT "FK_Investments_InvestmentTypes" FOREIGN KEY ("InvestmentTypeId") REFERENCES "InvestmentTypes"("Id");

-- 6. Drop old Type column
ALTER TABLE "Investments" DROP COLUMN "Type";
