namespace Nexo.Infrastructure.Persistence.Loading;

/// <summary>SQL da carga do cadastro de fundos e classes, na ordem em que roda.</summary>
internal static class RegistryLoadSql
{
    public const string CreateStageTables = """
        DROP TABLE IF EXISTS stage_registry_funds;
        CREATE TEMP TABLE stage_registry_funds (
            registry_id bigint NOT NULL,
            cnpj char(14) NOT NULL,
            name text NOT NULL,
            registered_at date,
            fund_type text,
            situation text,
            administrator_name text,
            manager_name text,
            net_worth numeric,
            net_worth_date date
        );

        DROP TABLE IF EXISTS stage_registry_classes;
        CREATE TEMP TABLE stage_registry_classes (
            fund_registry_id bigint NOT NULL,
            cnpj char(14) NOT NULL,
            name text NOT NULL,
            registered_at date,
            class_type text,
            situation text,
            anbima_classification text,
            target_audience text
        );
        """;

    public const string CopyFunds = """
        COPY stage_registry_funds (registry_id, cnpj, name, registered_at, fund_type, situation,
                                   administrator_name, manager_name, net_worth, net_worth_date) FROM STDIN (FORMAT BINARY)
        """;

    public const string CopyClasses = """
        COPY stage_registry_classes (fund_registry_id, cnpj, name, registered_at, class_type, situation,
                                     anbima_classification, target_audience) FROM STDIN (FORMAT BINARY)
        """;

    // O mesmo CNPJ aparece em registros antigos e cancelados. Vale o registro mais recente.
    public const string UpsertFunds = """
        INSERT INTO funds (cnpj, name, fund_type, situation, administrator_name, manager_name, net_worth, net_worth_date, is_placeholder, updated_at)
        SELECT DISTINCT ON (cnpj) cnpj, name, fund_type, situation, administrator_name, manager_name, net_worth, net_worth_date, false, now()
        FROM stage_registry_funds
        ORDER BY cnpj, registered_at DESC NULLS LAST, registry_id DESC
        ON CONFLICT (cnpj) DO UPDATE SET
            name = EXCLUDED.name,
            fund_type = EXCLUDED.fund_type,
            situation = EXCLUDED.situation,
            administrator_name = EXCLUDED.administrator_name,
            manager_name = EXCLUDED.manager_name,
            net_worth = EXCLUDED.net_worth,
            net_worth_date = EXCLUDED.net_worth_date,
            is_placeholder = false,
            updated_at = now()
        WHERE funds.is_placeholder
           OR (funds.name, funds.fund_type, funds.situation, funds.administrator_name, funds.manager_name, funds.net_worth, funds.net_worth_date)
              IS DISTINCT FROM
              (EXCLUDED.name, EXCLUDED.fund_type, EXCLUDED.situation, EXCLUDED.administrator_name, EXCLUDED.manager_name, EXCLUDED.net_worth, EXCLUDED.net_worth_date);
        """;

    // A classe aponta para o fundo pelo ID de registro, que levamos até o CNPJ do fundo.
    public const string UpsertClasses = """
        INSERT INTO fund_classes (fund_id, cnpj, name, class_type, situation, anbima_classification, target_audience)
        SELECT DISTINCT ON (c.cnpj) f.id, c.cnpj, c.name, c.class_type, c.situation, c.anbima_classification, c.target_audience
        FROM stage_registry_classes c
        JOIN (SELECT DISTINCT ON (registry_id) registry_id, cnpj
              FROM stage_registry_funds
              ORDER BY registry_id, registered_at DESC NULLS LAST) AS registration ON registration.registry_id = c.fund_registry_id
        JOIN funds f ON f.cnpj = registration.cnpj
        ORDER BY c.cnpj, c.registered_at DESC NULLS LAST
        ON CONFLICT (cnpj) DO UPDATE SET
            fund_id = EXCLUDED.fund_id,
            name = EXCLUDED.name,
            class_type = EXCLUDED.class_type,
            situation = EXCLUDED.situation,
            anbima_classification = EXCLUDED.anbima_classification,
            target_audience = EXCLUDED.target_audience
        WHERE (fund_classes.fund_id, fund_classes.name, fund_classes.class_type, fund_classes.situation,
               fund_classes.anbima_classification, fund_classes.target_audience)
              IS DISTINCT FROM
              (EXCLUDED.fund_id, EXCLUDED.name, EXCLUDED.class_type, EXCLUDED.situation,
               EXCLUDED.anbima_classification, EXCLUDED.target_audience);
        """;

    public const string CountClassesWithoutFund = """
        SELECT count(*)::int
        FROM stage_registry_classes c
        WHERE NOT EXISTS (SELECT 1 FROM stage_registry_funds f WHERE f.registry_id = c.fund_registry_id);
        """;

    // Carteiras que chegaram antes do cadastro ficaram num fundo provisório com o CNPJ da classe.
    // Agora que a classe é conhecida, elas passam para o fundo e a classe certos.
    public const string AttachEarlyPortfolios = """
        UPDATE portfolios p SET fund_id = c.fund_id, fund_class_id = c.id
        FROM funds placeholder
        JOIN fund_classes c ON c.cnpj = placeholder.cnpj
        WHERE p.fund_id = placeholder.id
          AND placeholder.is_placeholder
          AND c.fund_id <> placeholder.id;

        DELETE FROM funds placeholder
        WHERE placeholder.is_placeholder
          AND EXISTS (SELECT 1 FROM fund_classes c WHERE c.cnpj = placeholder.cnpj AND c.fund_id <> placeholder.id)
          AND NOT EXISTS (SELECT 1 FROM portfolios p WHERE p.fund_id = placeholder.id);

        UPDATE portfolios p SET fund_class_id = c.id
        FROM funds f
        JOIN fund_classes c ON c.fund_id = f.id AND c.cnpj = f.cnpj
        WHERE p.fund_id = f.id AND p.fund_class_id IS NULL;
        """;
}
