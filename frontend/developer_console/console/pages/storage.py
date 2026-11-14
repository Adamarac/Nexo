"""Armazenamento: quanto o banco ocupa, como as posições se dividem por mês e quanto os arquivos da CVM pesam."""

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

from console import data, theme, ui
from console.data import fetch
from console.sources.database import partition_month
from nexo_shared import formatting, labels, viz


def render() -> None:
    live = ui.page_header("Armazenamento", "Tamanho do banco, partições de posições e arquivos guardados em disco")
    ui.run(_body, live)


def _body() -> None:
    stats, tables, disk = fetch("database", "stats"), fetch("database", "table_sizes"), fetch("files", "usage")
    partitions, stored = fetch("database", "partitions"), fetch("database", "stored_files")
    _indicators(stats, tables, disk)

    left, right = st.columns([3, 2], gap="medium")
    with left, ui.card("tables-map", "Espaço por tabela", ui.sources(tables)):
        _tables_map(tables)
    with right, ui.card("disk", "Disco dos arquivos da CVM", ui.sources(disk, stored)):
        _disk(disk, stored)

    with ui.card("partitions", "Partições de posições por mês", ui.sources(partitions)):
        _partitions(partitions)

    with ui.card("tables", "Tabelas", "linhas estimadas pelas estatísticas do PostgreSQL"):
        _tables(tables)


def _indicators(stats: data.Reading, tables: data.Reading, disk: data.Reading) -> None:
    positions = None
    if tables.ok:
        found = tables.value[tables.value["table_name"] == "positions"]
        positions = found.iloc[0] if not found.empty else None
    ui.kpis(
        [
            ui.Kpi(
                "Banco de dados",
                formatting.size(stats.value["database_bytes"]) if stats.ok else "—",
                "tamanho total",
            ),
            ui.Kpi(
                "Posições",
                formatting.size(positions["total_bytes"]) if positions is not None else "—",
                f"{formatting.compact(positions['estimated_rows'])} linhas estimadas" if positions is not None else "",
            ),
            ui.Kpi(
                "Partições",
                formatting.number(positions["partitions"]) if positions is not None else "—",
                "uma por mês de referência",
            ),
            ui.Kpi(
                "Arquivos em disco",
                formatting.size(disk.value["bytes"]) if disk.ok else "—",
                f"{formatting.number(disk.value['files'])} arquivos" if disk.ok else (disk.error or ""),
            ),
            ui.Kpi(
                "Disco livre",
                formatting.size(disk.value["disk_free"]) if disk.ok else "—",
                f"de {formatting.size(disk.value['disk_total'])}" if disk.ok else "",
            ),
        ]
    )


def _tables_map(tables: data.Reading) -> None:
    if not tables.ok:
        ui.unavailable(tables)
        return
    frame = tables.value[tables.value["total_bytes"] > 0].sort_values("total_bytes", ascending=False)
    # Quatro maiores tabelas com cor própria; o resto soma em "outras", em cinza.
    top, rest = frame.head(4), frame.iloc[4:]
    names = top["table_name"].tolist() + (["outras"] if not rest.empty else [])
    sizes = top["total_bytes"].astype(float).tolist() + ([float(rest["total_bytes"].sum())] if not rest.empty else [])
    total = sum(sizes)
    colors = theme.SERIES[: len(top)] + ([theme.OTHER] if not rest.empty else [])
    ui.donut(
        names, sizes, colors, [f"{formatting.size(size)} · {formatting.percent(size / total)}" for size in sizes], formatting.size(total)
    )


def _disk(disk: data.Reading, stored: data.Reading) -> None:
    if not disk.ok:
        ui.unavailable(disk)
        return
    usage = disk.value
    used = 1 - usage["disk_free"] / usage["disk_total"] if usage["disk_total"] else None
    if used is not None:
        ui.chart(viz.ring(used, theme.ACCENT, theme.TRACK, formatting.percent(used)), 160)
        ui.note("do disco onde o volume está montado já está em uso")
    rows = {"Zips da CVM no volume": f"{formatting.size(usage['bytes'])} em {formatting.number(usage['files'])} arquivos"}
    if stored.ok:
        for _, row in stored.value.iterrows():
            rows[f"Registrados como {labels.file_status(row['status']).lower()}"] = (
                f"{formatting.number(row['files'])} · {formatting.size(row['bytes'])}"
            )
    ui.rows(rows)


def _partitions(partitions: data.Reading) -> None:
    if not partitions.ok:
        ui.unavailable(partitions)
        return
    if partitions.value.empty:
        ui.empty("A tabela de posições ainda não tem partições.")
        return
    frame = partitions.value.assign(mês=partitions.value["partition_name"].map(partition_month)).dropna(subset=["mês"])
    megabytes = frame["total_bytes"].astype(float) / 1_048_576
    figure = go.Figure(
        go.Bar(
            x=frame["mês"],
            y=megabytes,
            marker_color=theme.SERIES[0],
            customdata=list(zip(frame["total_bytes"].map(formatting.size), frame["estimated_rows"].map(formatting.compact), strict=True)),
            hovertemplate="%{x}<br>%{customdata[0]} · %{customdata[1]} linhas<extra></extra>",
        )
    )
    figure.update_yaxes(title_text="MB")
    figure.update_xaxes(type="category", nticks=12, tickangle=0)
    figure.update_layout(bargap=0.2, hovermode="closest")
    ui.chart(figure, 280)
    with st.expander("Ver em tabela"):
        st.dataframe(
            pd.DataFrame(
                {
                    "partição": frame["partition_name"],
                    "mês": frame["mês"],
                    "linhas estimadas": frame["estimated_rows"].astype(float).round().astype("int64"),
                    "total": frame["total_bytes"].map(formatting.size),
                    "índices": frame["index_bytes"].map(formatting.size),
                }
            ),
            hide_index=True,
            width="stretch",
            column_config={"linhas estimadas": ui.count_column()},
        )


def _tables(tables: data.Reading) -> None:
    if not tables.ok:
        ui.unavailable(tables)
        return
    frame = tables.value
    st.dataframe(
        pd.DataFrame(
            {
                "tabela": frame["table_name"],
                "partições": frame["partitions"].astype(int),
                "linhas estimadas": frame["estimated_rows"].astype(float).round().astype("int64"),
                "dados": (frame["total_bytes"] - frame["index_bytes"]).map(formatting.size),
                "índices": frame["index_bytes"].map(formatting.size),
                "total": frame["total_bytes"].map(formatting.size),
                "bytes": frame["total_bytes"].astype("int64"),
            }
        ),
        hide_index=True,
        width="stretch",
        column_config={"linhas estimadas": ui.count_column(), "bytes": ui.count_column("bytes (para ordenar)")},
    )
