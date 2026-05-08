"""Small HTTP helpers for the Simply Plural / Apparyllis public REST API."""

from __future__ import annotations

import json
import urllib.request
from typing import Any


def get_json(api_base: str, path: str, token: str) -> Any:
    """Fetch JSON from the API using the token as the Authorization header."""

    url = api_base.rstrip("/") + "/" + path.lstrip("/")
    request = urllib.request.Request(
        url,
        headers={
            "Authorization": token,
            "Accept": "application/json",
        },
        method="GET",
    )

    with urllib.request.urlopen(request) as response:
        body = response.read().decode("utf-8")

    return json.loads(body)
