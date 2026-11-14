"""Arquivos da CVM guardados em disco. O console enxerga o volume dos workers em modo só leitura."""

import os
import shutil
from pathlib import Path


class StoredFiles:
    def __init__(self, root: str | None):
        self._root = root

    def usage(self) -> dict:
        """Quantos arquivos e bytes existem na pasta, e quanto sobra no disco onde ela está."""
        if not self._root or not Path(self._root).is_dir():
            raise FileNotFoundError(f"A pasta {self._root or '(não configurada)'} não está montada neste container")

        files, total = 0, 0
        for folder, _, names in os.walk(self._root):
            for name in names:
                try:
                    total += (Path(folder) / name).stat().st_size
                    files += 1
                except OSError:
                    continue

        disk = shutil.disk_usage(self._root)
        return {"files": files, "bytes": total, "disk_total": disk.total, "disk_free": disk.free}
