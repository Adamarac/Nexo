namespace Nexo.Infrastructure.Persistence.Loading;

/// <summary>SQL da carga de carteiras, na ordem em que roda. Cada passo é pequeno e tem um nome.</summary>
internal static class PortfolioLoadSql
{
    public const string CreateStageTables = """
        DROP TABLE IF EXISTS stage_positions;
        CREATE TEMP TABLE stage_positions (
            fund_cnpj char(14) NOT NULL,
            fund_name text NOT NULL,
            fund_type text,
            reference_date date NOT NULL,
            block smallint,
            is_confidential boolean NOT NULL,
            application_type text NOT NULL,
            asset_type text,
            trading_intent text,
            issuer_is_related boolean,
            quantity numeric,
            market_value numeric,
            cost_value numeric,
            bought_quantity numeric,
            bought_value numeric,
            sold_quantity numeric,
            sold_value numeric,
            confidential_until date,
            details jsonb,
            row_hash char(64) NOT NULL,
            asset_identity_key text,
            asset_block smallint,
            asset_application_type text,
            asset_asset_type text,
            asset_isin text,
            asset_ticker text,
            asset_description text,
            asset_issuer_document text,
            asset_issuer_name text,
            asset_country_code text,
            asset_maturity_date date,
            asset_invested_fund_document text
        );

        DROP TABLE IF EXISTS stage_net_worths;
        CREATE TEMP TABLE stage_net_worths (
            fund_cnpj char(14) NOT NULL,
            fund_name text NOT NULL,
            fund_type text,
            reference_date date NOT NULL,
            net_worth numeric,
            row_hash char(64) NOT NULL
        );
        """;

    public const string CopyPositions = """
        COPY stage_positions (
            fund_cnpj, fund_name, fund_type, reference_date, block, is_confidential, application_type, asset_type,
            trading_intent, issuer_is_related, quantity, market_value, cost_value, bought_quantity, bought_value,
            sold_quantity, sold_value, confidential_until, details, row_hash,
            asset_identity_key, asset_block, asset_application_type, asset_asset_type, asset_isin, asset_ticker,
            asset_description, asset_issuer_document, asset_issuer_name, asset_country_code, asset_maturity_date,
            asset_invested_fund_document
        ) FROM STDIN (FORMAT BINARY)
        """;

    public const string CopyNetWorths = """
        COPY stage_net_worths (fund_cnpj, fund_name, fund_type, reference_date, net_worth, row_hash) FROM STDIN (FORMAT BINARY)
        """;

    // Com vários workers, duas versões do mesmo mês não podem gravar revisões ao mesmo tempo.
    // A trava vale por nome de arquivo e é liberada sozinha no fim da transação; meses diferentes seguem em paralelo.
    public const string LockSameSourceFile = """
        SELECT pg_advisory_xact_lock(hashtext(source_key)) FROM dataset_files WHERE id = @datasetFileId;
        """;

    // Cada mês tem a sua partição na tabela de posições; a função é criada em PartitionedPositionsSql.
    public const string EnsureMonthlyPartitions = """
        SELECT ensure_positions_partition(month)
        FROM (SELECT DISTINCT date_trunc('month', reference_date)::date AS month FROM stage_positions) AS months;
        """;

    public const string IndexStageTables = """
        CREATE INDEX ON stage_positions (fund_cnpj, reference_date);
        ANALYZE stage_positions;
        ANALYZE stage_net_worths;
        """;

    // Um "reporter" é cada fundo ou classe que mandou carteira numa data.
    // O fingerprint resume todas as linhas dele: se não mudou, a carteira não mudou.
    public const string BuildReporters = """
        CREATE TEMP TABLE stage_reporters ON COMMIT DROP AS
        SELECT fund_cnpj,
               reference_date,
               min(fund_name) AS fund_name,
               min(fund_type) AS fund_type,
               encode(sha256(convert_to(string_agg(row_hash, '' ORDER BY row_hash), 'UTF8')), 'hex')::char(64) AS fingerprint,
               NULL::bigint AS fund_id,
               NULL::bigint AS fund_class_id,
               NULL::numeric AS net_worth,
               NULL::bigint AS portfolio_id,
               false AS changed
        FROM (SELECT fund_cnpj, reference_date, fund_name, fund_type, row_hash FROM stage_positions
              UNION ALL
              SELECT fund_cnpj, reference_date, fund_name, fund_type, row_hash FROM stage_net_worths) AS reported
        GROUP BY fund_cnpj, reference_date;
        """;

    // Carteira que chega antes do cadastro ganha um fundo provisório, completado depois pelo cadastro.
    public const string CreatePlaceholderFunds = """
        INSERT INTO funds (cnpj, name, fund_type, is_placeholder, updated_at)
        SELECT DISTINCT ON (r.fund_cnpj) r.fund_cnpj, r.fund_name, r.fund_type, true, now()
        FROM stage_reporters r
        WHERE NOT EXISTS (SELECT 1 FROM fund_classes c WHERE c.cnpj = r.fund_cnpj)
        ORDER BY r.fund_cnpj
        ON CONFLICT (cnpj) DO NOTHING;
        """;

    // Primeiro procuramos o CNPJ entre as classes; se não for classe, é o próprio fundo.
    public const string ResolveFunds = """
        UPDATE stage_reporters r SET fund_id = c.fund_id, fund_class_id = c.id
        FROM fund_classes c WHERE c.cnpj = r.fund_cnpj;

        UPDATE stage_reporters r SET fund_id = f.id
        FROM funds f WHERE r.fund_id IS NULL AND f.cnpj = r.fund_cnpj;

        UPDATE stage_reporters r SET net_worth = n.net_worth
        FROM (SELECT DISTINCT ON (fund_cnpj, reference_date) fund_cnpj, reference_date, net_worth
              FROM stage_net_worths ORDER BY fund_cnpj, reference_date) n
        WHERE n.fund_cnpj = r.fund_cnpj AND n.reference_date = r.reference_date;
        """;

    public const string MarkChangedReporters = """
        UPDATE stage_reporters r SET changed = NOT EXISTS (
            SELECT 1 FROM portfolios p
            WHERE p.fund_id = r.fund_id
              AND p.fund_class_id IS NOT DISTINCT FROM r.fund_class_id
              AND p.reference_date = r.reference_date
              AND p.is_current
              AND p.fingerprint = r.fingerprint);
        """;

    // A revisão antiga continua guardada; ela só deixa de ser a atual.
    public const string CreateRevisions = """
        UPDATE portfolios p SET is_current = false
        FROM stage_reporters r
        WHERE r.changed
          AND p.fund_id = r.fund_id
          AND p.fund_class_id IS NOT DISTINCT FROM r.fund_class_id
          AND p.reference_date = r.reference_date
          AND p.is_current;

        INSERT INTO portfolios (fund_id, fund_class_id, reference_date, revision, is_current, net_worth, fingerprint, dataset_file_id, created_at)
        SELECT r.fund_id, r.fund_class_id, r.reference_date,
               COALESCE((SELECT max(p.revision) FROM portfolios p
                         WHERE p.fund_id = r.fund_id
                           AND p.fund_class_id IS NOT DISTINCT FROM r.fund_class_id
                           AND p.reference_date = r.reference_date), 0) + 1,
               true, r.net_worth, r.fingerprint, @datasetFileId, now()
        FROM stage_reporters r
        WHERE r.changed;

        UPDATE stage_reporters r SET portfolio_id = p.id
        FROM portfolios p
        WHERE r.changed
          AND p.fund_id = r.fund_id
          AND p.fund_class_id IS NOT DISTINCT FROM r.fund_class_id
          AND p.reference_date = r.reference_date
          AND p.is_current;
        """;

    // A descrição de um ativo é atualizada com a ocorrência mais recente; a identidade nunca muda.
    public const string InsertNewAssets = """
        INSERT INTO assets (identity_key, block, application_type, asset_type, isin, ticker, description,
                            issuer_document, issuer_name, country_code, maturity_date, invested_fund_document)
        SELECT DISTINCT ON (asset_identity_key)
               asset_identity_key, asset_block, asset_application_type, asset_asset_type, asset_isin, asset_ticker,
               asset_description, asset_issuer_document, asset_issuer_name, asset_country_code, asset_maturity_date,
               asset_invested_fund_document
        FROM stage_positions
        WHERE asset_identity_key IS NOT NULL
        ORDER BY asset_identity_key
        ON CONFLICT (identity_key) DO UPDATE SET
            ticker = EXCLUDED.ticker,
            description = EXCLUDED.description,
            issuer_name = EXCLUDED.issuer_name,
            maturity_date = EXCLUDED.maturity_date
        WHERE (assets.ticker, assets.description, assets.issuer_name, assets.maturity_date)
              IS DISTINCT FROM (EXCLUDED.ticker, EXCLUDED.description, EXCLUDED.issuer_name, EXCLUDED.maturity_date);
        """;

    // O percentual é sobre o PL e fica vazio quando o PL é desconhecido, zero ou negativo.
    public const string InsertPositions = """
        INSERT INTO positions (portfolio_id, reference_date, asset_id, is_confidential, block, application_type, asset_type,
                               trading_intent, issuer_is_related, quantity, market_value, cost_value, bought_quantity,
                               bought_value, sold_quantity, sold_value, share_of_net_worth, confidential_until, details)
        SELECT r.portfolio_id, s.reference_date, a.id, s.is_confidential, s.block, s.application_type, s.asset_type,
               s.trading_intent, s.issuer_is_related, s.quantity, s.market_value, s.cost_value, s.bought_quantity,
               s.bought_value, s.sold_quantity, s.sold_value,
               CASE WHEN r.net_worth > 0 THEN round(s.market_value / r.net_worth, 10) END,
               s.confidential_until, s.details
        FROM stage_positions s
        JOIN stage_reporters r ON r.fund_cnpj = s.fund_cnpj AND r.reference_date = s.reference_date AND r.changed
        LEFT JOIN assets a ON a.identity_key = s.asset_identity_key;
        """;

    // Como cada fundo escreveu o nome de cada emissor no mês. Vale para todos os fundos do arquivo,
    // mesmo os que não mudaram: assim reprocessar um mês antigo também preenche esta tabela.
    public const string RecordIssuerSpellings = """
        DELETE FROM issuer_spellings s
        USING stage_reporters r
        WHERE s.fund_id = r.fund_id
          AND s.fund_class_id IS NOT DISTINCT FROM r.fund_class_id
          AND s.reference_date = r.reference_date;

        INSERT INTO issuer_spellings (issuer_document, issuer_name, fund_id, fund_class_id, reference_date)
        SELECT DISTINCT lpad(s.asset_issuer_document, 14, '0'), s.asset_issuer_name, r.fund_id, r.fund_class_id, s.reference_date
        FROM stage_positions s
        JOIN stage_reporters r ON r.fund_cnpj = s.fund_cnpj AND r.reference_date = s.reference_date
        WHERE s.asset_issuer_document ~ '^[0-9]{12,14}$'
          AND s.asset_issuer_name IS NOT NULL
          AND s.asset_invested_fund_document IS NULL;
        """;

    public const string CountChangedReporters = "SELECT count(*)::int FROM stage_reporters WHERE changed;";
}
