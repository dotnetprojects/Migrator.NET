#!/usr/bin/env bash
set -euo pipefail
database="$1"
# Registry timeouts are transient; retry downloads, never test failures.
pull() {
  for attempt in 1 2 3; do
    if docker pull "$1"; then return 0; fi
    sleep 5
  done
  return 1
}
case "$database" in
  Unit|SQLite) exit 0 ;;
  MySQL)
    docker run -d --name migrator-db -p 3306:3306 -e MYSQL_ROOT_PASSWORD=rootpass -e MYSQL_DATABASE=testdb -e MYSQL_USER=testuser -e MYSQL_PASSWORD=testpass mysql:8.0.44
    ready() { docker exec migrator-db mysql -uroot -prootpass -e 'SELECT 1' >/dev/null 2>&1; }
    ;;
  MariaDB)
    docker run -d --name migrator-db -p 3306:3306 -e MARIADB_ROOT_PASSWORD=rootpass -e MARIADB_DATABASE=testdb mariadb:11.4.10
    ready() { docker exec migrator-db mariadb -uroot -prootpass -e 'SELECT 1' >/dev/null 2>&1; }
    ;;
  PostgreSQL)
    docker run -d --name migrator-db -p 5432:5432 -e POSTGRES_USER=testuser -e POSTGRES_PASSWORD=testpass postgres:13.23
    ready() { docker exec migrator-db pg_isready -U testuser >/dev/null 2>&1; }
    ;;
  SQLServer)
    docker run -d --name migrator-db -p 1433:1433 -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=YourStrong@Passw0rd mcr.microsoft.com/mssql/server:2019-CU32-ubuntu-20.04
    ready() { docker exec migrator-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'YourStrong@Passw0rd' -Q 'SELECT 1' >/dev/null 2>&1; }
    ;;
  Oracle)
    docker run -d --name migrator-db -p 1521:1521 -e ORACLE_PASSWORD=adfkweflajdfglkj gvenzl/oracle-free:23.9-slim-faststart
    ready() { docker exec migrator-db healthcheck.sh >/dev/null 2>&1; }
    ;;
  Firebird)
    docker run -d --name migrator-db -p 3050:3050 -e FIREBIRD_ROOT_PASSWORD=masterkey -e FIREBIRD_DATABASE=test.fdb firebirdsql/firebird:5.0.3
    ready() { echo 'select 1 from rdb$database;' | docker exec -i migrator-db isql -b -u SYSDBA -p masterkey localhost:/var/lib/firebird/data/test.fdb >/dev/null 2>&1; }
    ;;
  Db2)
    pull icr.io/db2_community/db2:11.5.9.0
    docker run -d --name migrator-db --privileged -p 50000:50000 -e LICENSE=accept -e DB2INST1_PASSWORD=testpass -e DBNAME=testdb -e ARCHIVE_LOGS=false -e AUTOCONFIG=false icr.io/db2_community/db2:11.5.9.0
    ready() { docker logs migrator-db 2>&1 | grep -q 'Setup has completed'; }
    ;;
  Informix)
    pull icr.io/informix/informix-developer-database:15.0.1.0.3
    docker run -dt --name migrator-db --hostname ifx --privileged -p 9088:9088 -e LICENSE=accept icr.io/informix/informix-developer-database:15.0.1.0.3
    ready() { docker exec migrator-db bash -lc 'onstat -' 2>/dev/null | grep -q 'On-Line'; }
    ;;
  *) echo "Unknown database: $database" >&2; exit 1 ;;
esac
for attempt in $(seq 1 120); do
  if ready; then break; fi
  if [ "$attempt" -eq 120 ]; then docker logs migrator-db; exit 1; fi
  sleep 5
done
case "$database" in
  SQLServer) docker exec migrator-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'YourStrong@Passw0rd' -b -Q 'CREATE DATABASE [Whatever];' ;;
  Oracle) docker exec -i migrator-db sqlplus -s / as sysdba < .github/workflows/sql/oracle.sql ;;
  Informix)
    echo 'create database testdb with log;' | docker exec -i migrator-db bash -lc 'dbaccess sysmaster -'
    ;;
esac
