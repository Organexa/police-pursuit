"""Turns the GOOGLE_SERVICES_JSON secret into Android string resources for Push.androidlib.

Does what the google-services Gradle plugin would do. The output is gitignored and nothing is printed from the file.
"""
import json
import os
import pathlib
import sys
from xml.sax.saxutils import escape

PACKAGE = "com.policepursuit.game"
OUT = "unity/Assets/Plugins/Android/Push.androidlib/src/main/res/values/firebase.xml"


def resources(text):
    config = json.loads(text)
    clients = [c for c in config["client"]
               if c["client_info"]["android_client_info"]["package_name"] == PACKAGE]
    if not clients:
        raise ValueError("google-services.json has no Android app for " + PACKAGE)
    values = {
        "google_app_id": clients[0]["client_info"]["mobilesdk_app_id"],
        "google_api_key": clients[0]["api_key"][0]["current_key"],
        "gcm_defaultSenderId": config["project_info"]["project_number"],
        "project_id": config["project_info"]["project_id"],
    }
    lines = "".join(f'    <string name="{k}" translatable="false">{escape(v)}</string>\n' for k, v in values.items())
    return f'<?xml version="1.0" encoding="utf-8"?>\n<resources>\n{lines}</resources>\n'


if __name__ == "__main__":
    secret = os.environ.get("GOOGLE_SERVICES_JSON", "")
    if not secret:
        print("::notice::GOOGLE_SERVICES_JSON not set (DEPLOYMENT §3). Building without push.")
        sys.exit(0)
    out = pathlib.Path(OUT)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(resources(secret), encoding="utf-8")
