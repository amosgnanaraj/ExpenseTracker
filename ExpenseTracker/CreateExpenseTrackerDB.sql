-- Create the ExpenseTrackerDB database
-- You should connect to the default 'postgres' database to run this section, or just run it via command line
-- CREATE DATABASE "ExpenseTrackerDB";
-- \c "ExpenseTrackerDB"

-- Create AccountSources table
CREATE TABLE "AccountSources" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "Type" VARCHAR(50) NOT NULL CHECK ("Type" IN ('Bank', 'CreditCard', 'Loan')),
    "Balance" DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    "InterestRate" DECIMAL(5, 2) NULL
);

-- Create Categories table
CREATE TABLE "Categories" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "ParentCategoryId" INTEGER NULL,
    CONSTRAINT "FK_Categories_Categories_ParentCategoryId" FOREIGN KEY ("ParentCategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
);

-- Create Transactions table
CREATE TABLE "Transactions" (
    "Id" SERIAL PRIMARY KEY,
    "Date" TIMESTAMP WITH TIME ZONE NOT NULL,
    "Amount" DECIMAL(18, 2) NOT NULL,
    "Description" TEXT NULL,
    "CategoryId" INTEGER NOT NULL,
    "AccountSourceId" INTEGER NOT NULL,
    CONSTRAINT "FK_Transactions_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Transactions_AccountSources_AccountSourceId" FOREIGN KEY ("AccountSourceId") REFERENCES "AccountSources" ("Id") ON DELETE CASCADE
);
