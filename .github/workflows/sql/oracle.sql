WHENEVER SQLERROR EXIT SQL.SQLCODE

alter session set container = freepdb1;

create user k identified by k;

grant select_catalog_role to k;
grant create tablespace to k;
grant drop tablespace to k;
grant create user to k;
grant drop user to k;
grant create session to k with admin option;
grant resource to k with admin option;
grant connect to k with admin option;
grant unlimited tablespace to k with admin option;
grant select on v_$session to k with grant option;
grant select on sys.gv_$session to k with grant option
grant alter system to k;

exit;