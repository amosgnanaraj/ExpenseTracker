@echo off
set PGPASSWORD=EXPapp!@#
if "%~1"=="-f" (
    "C:\Program Files\PostgreSQL\18\bin\psql.exe" --host=localhost --port=5432 --username=expensetracker_user --dbname=ExpenseTrackerDB -f "%~2"
) else (
    "C:\Program Files\PostgreSQL\18\bin\psql.exe" --host=localhost --port=5432 --username=expensetracker_user --dbname=ExpenseTrackerDB -c "%~1"
)
