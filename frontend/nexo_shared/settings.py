import os


def env(name: str, default: str | None = None) -> str | None:
    """Lê uma variável de ambiente, tratando texto vazio como ausente."""
    value = os.environ.get(name, "").strip()
    return value or default


def api_url() -> str:
    return env("NEXO_API_URL", "http://localhost:5080")
