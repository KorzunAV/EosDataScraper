1 Install PostgresSql
2 Run init.sql from  \wwwroot\sql\init.sql (UpdateDb(...) will automaticaly run other *.sql)
3 Go to appsettings.json and check DefaultConnection string
5 Build solution

----
Use name pattern \d{4}-\d{2}-\d{2}-\d{2}.sql for new *.sql (files will be ordered by name)