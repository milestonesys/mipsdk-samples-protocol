"""
Handles caching and displaying of state, looking up names using config_api
"""

import os
import config_api


state_cache = {}


def update_state_cache(source, state_group, event_type):
    if source not in state_cache:
        state_cache[source] = {}
    state_cache[source][state_group] = event_type


def display_header():
    if os.name == "nt":
        os.system("cls")
    else:
        os.system("clear")
    print("Press ESC to return to menu")
    print("=" * 100)
    print("Source".ljust(50), "State group".ljust(30), "State".ljust(30))
    print("=" * 100)


async def process_events(events, gateway_uri, access_token):
    for event in events:
        if "stategroupid" in event:
            source = event["source"]
            state_group = event["stategroupid"]
            event_type = event["type"]
            update_state_cache(source, state_group, event_type)
            
    await display_states(gateway_uri, access_token)


async def display_states(gateway_uri, access_token):
    display_header()
    
    for source_id, attributes in state_cache.items():
        source_name = await config_api.get_source_name(source_id, gateway_uri, access_token)

        for state_group_id, event_type_id in attributes.items():
            state_group = await config_api.get_state_group_name(state_group_id, gateway_uri, access_token)
            state = await config_api.get_state_name(event_type_id, gateway_uri, access_token)
            print(source_name.ljust(50), state_group.ljust(30), state.ljust(30))
            source_name = ""
        print("-" * 100)
