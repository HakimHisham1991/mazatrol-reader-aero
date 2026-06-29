#!/usr/bin/env python3
"""Legacy entry point — delegates to mazatrol_reader package."""

from mazatrol_reader.__main__ import main

if __name__ == "__main__":
    raise SystemExit(main())
