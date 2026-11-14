"""Consultas de leitura ao PostgreSQL: tamanho, partições, conexões e arquivos em processamento."""

import pandas as pd
import psycopg

# A sessão do console é só de leitura e desiste de consultas que passem de 10 segundos.
_SESSION_OPTIONS = "-c default_transaction_read_only=on -c statement_timeout=10000"

# Tabela comum conta só ela mesma; tabela particionada soma todas as partições (pg_partition_tree).
TABLE_SIZES = """
    with tables as (
        select t.oid, t.relname, t.relkind
          from pg_class t
          join pg_namespace n on n.oid = t.relnamespace
         where n.nspname = 'public' and t.relkind in ('r', 'p') and not t.relispartition
    ),
    members as (
        select tables.oid as root, p.relid as member, p.isleaf, p.level
          from tables cross join lateral pg_partition_tree(tables.oid) p
         where tables.relkind = 'p'
        union all
        select oid, oid, true, 0 from tables where relkind = 'r'
    )
    select t.relname as table_name,
           count(*) filter (where m.level > 0) as partitions,
           coalesce(sum(greatest(c.reltuples, 0)) filter (where m.isleaf), 0) as estimated_rows,
           coalesce(sum(pg_total_relation_size(m.member)), 0) as total_bytes,
           coalesce(sum(pg_indexes_size(m.member)), 0) as index_bytes
      from tables t
      join members m on m.root = t.oid
      join pg_class c on c.oid = m.member
     group by t.relname
     order by total_bytes desc
"""

PARTITIONS = """
    select c.relname as partition_name,
           greatest(c.reltuples, 0) as estimated_rows,
           pg_total_relation_size(c.oid) as total_bytes,
           pg_indexes_size(c.oid) as index_bytes
      from pg_inherits i
      join pg_class c on c.oid = i.inhrelid
     where i.inhparent = 'positions'::regclass
     order by c.relname
"""

CONNECTIONS = """
    select coalesce(nullif(application_name, ''), '(sem nome)') as application,
           usename as user_name,
           coalesce(state, '(sem estado)') as state,
           count(*) as connections,
           max(now() - state_change) as longest_in_state
      from pg_stat_activity
     where datname = current_database() and backend_type = 'client backend'
     group by 1, 2, 3
     order by connections desc
"""

DATABASE_STATS = """
    select pg_database_size(current_database()) as database_bytes,
           (select setting::int from pg_settings where name = 'max_connections') as max_connections,
           numbackends, xact_commit, xact_rollback, blks_read, blks_hit,
           temp_files, temp_bytes, deadlocks, stats_reset, version() as server_version
      from pg_stat_database
     where datname = current_database()
"""

PROCESSING_FILES = """
    select id, source_key, attempts, processing_started_at, lease_until
      from dataset_files
     where status = 'Processing'
     order by processing_started_at
"""

FILE_DURATIONS = """
    select id, source_key, kind, status, rows_read, size,
           extract(epoch from processed_at - processing_started_at) as seconds,
           processing_started_at, processed_at
      from dataset_files
     where status <> 'Processing' and processed_at >= processing_started_at
     order by processed_at desc
     limit %(limit)s
"""

STORED_FILES = """
    select status, count(*) as files, coalesce(sum(size), 0) as bytes
      from dataset_files
     group by status
     order by files desc
"""

REJECTION_REASONS = """
    select reason, count(*) as rows
      from rejected_rows
     group by reason
     order by rows desc
"""


class Database:
    def __init__(self, dsn: str):
        self._dsn = dsn

    def stats(self) -> dict:
        return self._rows(DATABASE_STATS).iloc[0].to_dict()

    def table_sizes(self) -> pd.DataFrame:
        return self._rows(TABLE_SIZES)

    def partitions(self) -> pd.DataFrame:
        return self._rows(PARTITIONS)

    def connections(self) -> pd.DataFrame:
        return self._rows(CONNECTIONS)

    def processing_files(self) -> pd.DataFrame:
        return self._rows(PROCESSING_FILES)

    def file_durations(self, limit: int = 60) -> pd.DataFrame:
        return self._rows(FILE_DURATIONS, {"limit": limit})

    def stored_files(self) -> pd.DataFrame:
        return self._rows(STORED_FILES)

    def rejection_reasons(self) -> pd.DataFrame:
        return self._rows(REJECTION_REASONS)

    def _rows(self, sql: str, params: dict | None = None) -> pd.DataFrame:
        with psycopg.connect(self._dsn, options=_SESSION_OPTIONS, application_name="nexo-console", connect_timeout=5) as connection:
            cursor = connection.execute(sql, params)
            columns = [column.name for column in cursor.description]
            return pd.DataFrame(cursor.fetchall(), columns=columns)


def partition_month(partition_name: str) -> str | None:
    """'positions_202610' vira '2026-10'; nomes fora do padrão devolvem None."""
    suffix = partition_name.rsplit("_", 1)[-1]
    if len(suffix) != 6 or not suffix.isdigit():
        return None
    return f"{suffix[:4]}-{suffix[4:]}"
