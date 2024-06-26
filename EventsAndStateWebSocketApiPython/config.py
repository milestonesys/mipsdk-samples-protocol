"""
Handles reading and writing API Gateway address and username to settings.json
"""

import os
import json
import sys
from getpass import getpass
import re


password = None


def clear_password():
    global password
    password = None


def set_password():
    global password

    print("[?] Enter password")
    password = getpass(prompt="[PASSWORD] ->")
    sys.stdout.flush()


def is_valid_uri(uri):
    return re.match(r"^https?://[a-zA-Z0-9.-]+/?$", uri)


def write_settings():
    print("[?] Enter API gateway address (example: https://ABC123-DEF456)")
    
    while True:
        api_gateway = input("[ADDRESS] ->")
        if is_valid_uri(api_gateway):
            break
        print("[!] Invalid URI - Make sure to format it correctly like in the example.")

    print("[?] Enter username")
    username = input("[USERNAME] ->")

    # Keep password in memory, but never save it to disk
    set_password()

    settings = {
        "apigateway": api_gateway,
        "username": username
    }

    with open("settings.json", "w+") as f:
        json.dump(settings, f, indent=4)


def read_settings():
    global password

    if not os.path.exists("settings.json"):
        write_settings()

    with open("settings.json", "r") as f:
        settings = json.load(f)

        api_gateway = settings.get("apigateway")
        username = settings.get("username")

        is_basic_user = "\\" not in username

        if password is None:
            set_password()

        return api_gateway, username, password, is_basic_user
