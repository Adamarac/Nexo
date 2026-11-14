import json

from console.sources import database, loki, prometheus, rabbitmq


def test_queue_table_reads_counts_rates_and_roles():
    queues = [
        {
            "name": "nexo.process",
            "messages_ready": 2,
            "messages_unacknowledged": 1,
            "consumers": 3,
            "state": "running",
            "arguments": {},
            "message_stats": {"publish_details": {"rate": 0.5}, "deliver_get_details": {"rate": 0.4}},
        }
    ]
    row = rabbitmq.queue_table(queues).iloc[0]
    assert row["papel"] == "arquivos para processar"
    assert (row["prontas"], row["em uso"], row["consumidores"]) == (2, 1, 3)
    assert row["entrada /s"] == 0.5


def test_consumers_are_matched_to_the_container_that_opened_the_connection():
    connections = [{"name": "172.18.0.10:35278 -> 172.18.0.3:5672", "client_properties": {"connection_name": "nexo@90efae49d152"}}]
    consumers = [
        {
            "queue": {"name": "nexo.process"},
            "channel_details": {"connection_name": "172.18.0.10:35278 -> 172.18.0.3:5672"},
            "prefetch_count": 1,
        }
    ]
    hosts = rabbitmq.consumers_by_host(consumers, connections)
    assert hosts == {"90efae49d152": [{"fila": "nexo.process", "prefetch": 1, "ativo": True}]}


def test_dead_letter_message_is_summarized():
    body = {"messageId": "m1", "correlationId": "c1", "datasetFileId": 47, "sourceKey": "cda_fi_202610", "occurredAt": "2026-11-14"}
    message = {
        "payload": json.dumps(body),
        "payload_encoding": "string",
        "redelivered": True,
        "properties": {"headers": {"x-attempt": 4, "x-last-error": "timeout"}},
    }
    summary = rabbitmq.describe_message(message)
    assert (summary["arquivo"], summary["origem"], summary["tentativas"], summary["último erro"]) == (47, "cda_fi_202610", 4, "timeout")


def test_log_query_combines_filters_and_escapes_text():
    query = loki.build_query(
        loki.LogFilter(
            services=("nexo-worker",),
            levels=("Error", "Fatal"),
            text='falha "grave" (x)',
            source_key="cda_fi_202610",
            instance="nexo-watcher",
        )
    )
    assert query.startswith('{service_name=~"nexo-worker", service_instance_id="nexo-watcher"}')
    assert '| severity_text=~"Error|Fatal"' in query
    assert '| SourceKey="cda_fi_202610"' in query
    # Espaço fica como está; parênteses e aspas ganham escape.
    assert r'|~ "(?i)falha \"grave\" \\(x\\)"' in query


def test_log_frame_puts_newest_first_and_hides_sdk_noise():
    streams = [
        {
            "stream": {"service_name": "nexo-worker", "severity_text": "Error", "SourceKey": "a", "telemetry_sdk_name": "serilog"},
            "values": [["1794641194000000000", "antigo"], ["1794641195000000000", "novo"]],
        }
    ]
    frame = loki.log_frame(streams)
    assert frame["mensagem"].tolist() == ["novo", "antigo"]
    assert "telemetry_sdk_name" not in frame.iloc[0]["atributos"]


def test_prometheus_frames_turn_labels_into_columns():
    vector = prometheus.vector_frame([{"metric": {"instance": "w1", "job": "nexo-worker"}, "value": [1794641477.4, "157732864"]}])
    assert vector.iloc[0]["instance"] == "w1" and vector.iloc[0]["value"] == 157732864.0

    matrix = prometheus.matrix_frame([{"metric": {"instance": "w1"}, "values": [[1, "1"], [2, "3"]]}])
    assert matrix["value"].tolist() == [1.0, 3.0]


def test_partition_month_reads_the_suffix():
    assert database.partition_month("positions_202610") == "2026-10"
    assert database.partition_month("positions_default") is None
