-- Insert primary categories
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (1, 'Food', NULL);
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (2, 'Transport', NULL);
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (3, 'Utilities', NULL);
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (4, 'Healthcare', NULL);

-- Insert subcategories
-- For Food (Id 1)
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (5, 'Dining', 1);
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (6, 'Groceries', 1);

-- For Transport (Id 2)
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (7, 'Fuel', 2);
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (8, 'Public Transit', 2);

-- For Utilities (Id 3)
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (9, 'Electricity', 3);
INSERT INTO "Categories" ("Id", "Name", "ParentCategoryId") VALUES (10, 'Internet', 3);

-- Reset sequence to continue from max Id + 1
SELECT setval(pg_get_serial_sequence('"Categories"', 'Id'), (SELECT MAX("Id") FROM "Categories"));
