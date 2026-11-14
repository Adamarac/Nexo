#!/bin/sh
# Cria o usuário da aplicação, sem privilégios de superusuário, dono do banco do Nexo.
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE ROLE "$NEXO_DB_USER" LOGIN PASSWORD '$NEXO_DB_PASSWORD';
    ALTER DATABASE "$POSTGRES_DB" OWNER TO "$NEXO_DB_USER";
    ALTER SCHEMA public OWNER TO "$NEXO_DB_USER";
EOSQL
