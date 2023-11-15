"""
Handles displaying of events, looking up names using config_api
"""

import os
from datetime import datetime
import config_api


def display_header():
    os.system("cls")
    print("Press ESC to return to menu")
    print("=" * 100)
    print("Timestamp".ljust(30), "Source".ljust(50), "Event".ljust(30))
    print("=" * 100)

        
async def process_events(events, gateway_uri, access_token):
    for event in events:
        event_source = event["source"]
        event_type = event["type"]
        source_name = await config_api.get_source_name(event_source, gateway_uri, access_token)
        event_name = await config_api.get_event_name(event_type, gateway_uri, access_token)
        timestamp = datetime.fromisoformat(event["time"])
        
        print(timestamp.strftime("%Y-%m-%d %H:%M:%S").ljust(30), source_name.ljust(50), event_name.ljust(30))
