-- Migration: Add TransactionType and TransferToAccountSourceId to Transactions table
-- Date: 2026-03-11

-- Add Type column with default 'Debit' for existing rows
ALTER TABLE "Transactions"
ADD COLUMN IF NOT EXISTS "Type" text NOT NULL DEFAULT 'Debit';

-- Add TransferToAccountSourceId column (nullable FK)
ALTER TABLE "Transactions"
ADD COLUMN IF NOT EXISTS "TransferToAccountSourceId" integer NULL;

-- Add foreign key constraint for TransferToAccountSourceId
ALTER TABLE "Transactions"
ADD CONSTRAINT "FK_Transactions_AccountSources_TransferToAccountSourceId"
FOREIGN KEY ("TransferToAccountSourceId")
REFERENCES "AccountSources" ("Id")
ON DELETE RESTRICT;

-- Add index for TransferToAccountSourceId
CREATE INDEX IF NOT EXISTS "IX_Transactions_TransferToAccountSourceId"
ON "Transactions" ("TransferToAccountSourceId");
