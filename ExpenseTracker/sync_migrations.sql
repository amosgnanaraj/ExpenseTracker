INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
SELECT '20260314143407_AddCategoryToMutualFund', '8.0.2'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314143407_AddCategoryToMutualFund'
);
