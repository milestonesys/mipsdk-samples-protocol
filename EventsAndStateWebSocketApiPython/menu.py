"""
Displays a menu and performs login when required
"""

import os
import ctypes
from tkinter.tix import DisplayStyle
import requests
import urllib3
import identity_provider
import config
from http import HTTPStatus

art = r"""

   ______               _                   _____ _        _                  _____ _____ 
  |  ____|             | |         ___     / ____| |      | |           /\   |  __ \_   _|
  | |____   _____ _ __ | |_ ___   ( _ )   | (___ | |_ __ _| |_ ___     /  \  | |__) || |  
  |  __\ \ / / _ \ '_ \| __/ __|  / _ \/\  \___ \| __/ _` | __/ _ \   / /\ \ |  ___/ | |  
  | |___\ V /  __/ | | | |_\__ \ | (_>  <  ____) | || (_| | ||  __/  / ____ \| |    _| |_ 
  |______\_/ \___|_| |_|\__|___/  \___/\/ |_____/ \__\__,_|\__\___| /_/    \_\_|   |_____|
                                                                                                                       
"""

menu = """
                                (1) - [STATE VIEWER]
                                (2) - [EVENT VIEWER]
                                (3) - [SETTINGS]
                                (4) - [EXIT]
"""

verify_ssl = False

if not verify_ssl:
    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

    
def print_options():
    ctypes.windll.kernel32.SetConsoleTitleW(f"Events and state API sample")
    os.system("clear")
    print(art)
    print(menu)


def login():
    session = requests.Session()
    server_url, username, password, is_basic_user = config.read_settings()

    try:
        response = identity_provider.get_token(session, username, password, server_url, is_basic_user, verify_ssl)

        if response.status_code == 200:
            token_response = response.json()
            access_token = token_response["access_token"]
            return server_url, access_token
        else:
            error = f"{response.status_code} ({HTTPStatus(response.status_code).phrase})"
            print(f"[!] Login failed: {error}")

            if response.status_code == 401 or "InvalidCredential" in response.text:
                config.clear_password()

    except Exception as e:
        print(f"[!] Login failed: {e}")

    return None, None


def show():
    print_options()

    while True:
        choice = input("[OPTION] ->")

        if choice == "1" or choice == "2":
            server_url, access_token = login()
            if access_token is None:
                continue

            if choice == "1":
                return server_url, access_token, "stateviewer"
            elif choice == "2":
                return server_url, access_token, "eventviewer"

        elif choice == "3":
            config.write_settings()
            print_options()

        elif choice == "4":
            exit(0)
        else:
            print("[!] Invalid input - Select either 1, 2, 3, or 4")
