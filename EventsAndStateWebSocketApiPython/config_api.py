"""
Provide access to the Configuration REST API.
Results are cached to provide better performance and prevent putting a high load on the API Gateway.
"""

import aiohttp
import json

config_cache = {}
verify_ssl = True


async def get_source_name(resource_path, gateway_uri, access_token):
    result = await lookup(resource_path, "displayName", gateway_uri, access_token)
    return result


async def get_state_name(event_type_id, gateway_uri, access_token):
    result = await lookup(f"eventTypes/{event_type_id}", "state", gateway_uri, access_token)
    return result


async def get_event_name(event_type_id, gateway_uri, access_token):
    result = await lookup(f"eventTypes/{event_type_id}", "displayName", gateway_uri, access_token)
    return result


async def get_state_group_name(state_group_id, gateway_uri, access_token):
    result = await lookup(f"stateGroups/{state_group_id}", "displayName", gateway_uri, access_token)
    return result


async def lookup(resource_path, data_key, gateway_uri, access_token):
    if resource_path in config_cache:
        return config_cache[resource_path]

    name = "Unknown"
    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{gateway_uri}/api/rest/v1/{resource_path}", headers={"Authorization": f"Bearer {access_token}"}, verify_ssl=verify_ssl) as response:
                data = await response.text()

                if response.status == 200:
                    data = json.loads(data)
                    name = data['data'][data_key]
                    config_cache[resource_path] = name
                elif 400 <= response.status <= 499:
                    # Bad request - cache "Unknown", so we don't retry
                    config_cache[resource_path] = name

    except:
        pass

    return name
