from dataclasses import dataclass

from nexo_shared.settings import api_url, env


@dataclass(frozen=True)
class ConsoleSettings:
    api_url: str
    admin_api_key: str | None
    rabbitmq_url: str
    rabbitmq_user: str
    rabbitmq_password: str
    prometheus_url: str
    loki_url: str
    database_url: str | None
    redis_url: str
    files_root: str | None


def load() -> ConsoleSettings:
    """Endereços das fontes. Os padrões servem para rodar fora do Docker com as portas do compose."""
    return ConsoleSettings(
        api_url=api_url(),
        admin_api_key=env("NEXO_ADMIN_API_KEY"),
        rabbitmq_url=env("RABBITMQ_MANAGEMENT_URL", "http://localhost:15672"),
        rabbitmq_user=env("RABBITMQ_USER", "nexo"),
        rabbitmq_password=env("RABBITMQ_PASSWORD", ""),
        prometheus_url=env("PROMETHEUS_URL", "http://localhost:9090"),
        loki_url=env("LOKI_URL", "http://localhost:3100"),
        database_url=env("DATABASE_URL"),
        redis_url=env("REDIS_URL", "redis://localhost:6379"),
        files_root=env("NEXO_FILES_ROOT"),
    )
