"""Estado do Redis, que guarda o cache das consultas da API."""

import redis

EPOCH_KEY = "nexo:cache-epoch"


class Cache:
    def __init__(self, url: str):
        self._client = redis.Redis.from_url(url, socket_timeout=5, socket_connect_timeout=5, decode_responses=True)

    def info(self) -> dict:
        """Junta as seções do INFO que interessam, mais a época atual do cache e o total de chaves."""
        info = self._client.info()
        return {
            "version": info.get("redis_version"),
            "uptime_seconds": info.get("uptime_in_seconds"),
            "clients": info.get("connected_clients"),
            "used_memory": info.get("used_memory"),
            "peak_memory": info.get("used_memory_peak"),
            "max_memory": info.get("maxmemory"),
            "eviction_policy": info.get("maxmemory_policy"),
            "hits": info.get("keyspace_hits", 0),
            "misses": info.get("keyspace_misses", 0),
            "evicted": info.get("evicted_keys", 0),
            "expired": info.get("expired_keys", 0),
            "keys": self._client.dbsize(),
            "epoch": self._client.get(EPOCH_KEY),
        }
