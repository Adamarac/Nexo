# Imagem das telas em Streamlit. APP escolhe qual: developer_console ou financial_analytics.
FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PIP_NO_CACHE_DIR=1 PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /app
COPY frontend/requirements.txt .
RUN pip install -r requirements.txt

ARG APP
COPY frontend/nexo_shared ./nexo_shared
COPY frontend/${APP} ./${APP}

RUN useradd --create-home --uid 10001 nexo
USER nexo

# O Streamlit lê o tema de .streamlit/config.toml na pasta onde roda.
WORKDIR /app/${APP}
ENV PYTHONPATH=/app
EXPOSE 8501

HEALTHCHECK --interval=15s --timeout=5s --retries=5 \
  CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8501/_stcore/health')"

CMD ["streamlit", "run", "app.py", "--server.port=8501", "--server.address=0.0.0.0"]
