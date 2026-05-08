"""Configuration helpers for PluralBridge."""

from __future__ import annotations

import os
from dataclasses import dataclass


DEFAULT_API_BASE = "https://api.apparyllis.com"


@dataclass(frozen=True)
class PluralBridgeConfig:
    api_base: str = DEFAULT_API_BASE
    output_dir: str = "exports/json"
    avatar_output_dir: str = "exports/member_images"
    token_env_var: str = "SP_TOKEN"

    @property
    def token(self) -> str:
        value = os.environ.get(self.token_env_var, "")
        if not value:
            raise RuntimeError(
                f"Missing API token. Set {self.token_env_var} before running this script."
            )
        return value
