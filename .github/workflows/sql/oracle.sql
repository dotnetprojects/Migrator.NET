WHENEVER SQLERROR EXIT SQL.SQLCODE

alter session set container = freepdb1;

create user k identified by k;

grant
   create user
to k;

grant
   drop user
to k;

grant
   create session
to k with admin option;

grant resource to k with admin option;

grant connect to k with admin option;

grant
   unlimited tablespace
to k with admin option;

grant select on v_$session to k with grant option

grant
   alter system
to k

exit;