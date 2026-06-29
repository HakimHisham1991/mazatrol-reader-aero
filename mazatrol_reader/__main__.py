"""Entry point for the Mazatrol Reader application."""

from __future__ import annotations

import logging
import sys


def configure_logging(level: int = logging.INFO) -> None:
    logging.basicConfig(
        level=level,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        datefmt="%H:%M:%S",
    )


def main() -> int:
    configure_logging()

    try:
        import wx
    except ImportError:
        print("wxPython is required. Install with: pip install wxPython", file=sys.stderr)
        return 1

    from mazatrol_reader.gui import MazatrolApp

    app = MazatrolApp(False)
    app.MainLoop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
