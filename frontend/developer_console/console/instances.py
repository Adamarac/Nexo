"""Quais instâncias .NET estão vivas agora, pela idade da última métrica que mandaram."""

from console.data import fetch

AGE_QUERY = "time() - timestamp(dotnet_process_memory_working_set_bytes)"

# Os serviços mandam métricas a cada minuto; sem sinal por mais de dois minutos e meio, a instância provavelmente parou.
STALE_AFTER_SECONDS = 150


def ages() -> dict[str, float]:
    reading = fetch("prometheus", "instant", AGE_QUERY)
    if not reading.ok or reading.value.empty:
        return {}
    return dict(zip(reading.value["instance"], reading.value["value"], strict=True))


def active() -> set[str]:
    return {instance for instance, seconds in ages().items() if seconds <= STALE_AFTER_SECONDS}
