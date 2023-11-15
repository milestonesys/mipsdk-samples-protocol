"""
Provide access to the Events and State web socket API.
"""

import json

command_id = 0


async def start_session(web_socket, session_id, event_id):
    """ Start a session.
    
    session_id and event_id may be left blank to start a new session.

    To resume a previous session (within 30 seconds), session_id and event_id of the last received event must be provided.
    This will resume any subscriptions previously made and recover lost events.
    If the session cannot be resumed (e.g. due to timeout) a new session will be created, and any subscriptions must be created again.

    response["status"] will contain the status
    - 200 indicates an existing session was successfully resumed.
    - 201 indicates a new session was created.
    """
    return await send_command(web_socket, {
        "command": "startSession",
        "sessionId": session_id,
        "eventId": event_id
    })


async def create_subscription(web_socket, filters):
    """ Create a subscription.
    
    filters must be in the format:
    [
        {
            "modifier": "include",
            "resourceTypes": [ "cameras", "microphones", "outputs", ... ],
            "sourceIds": [ "25d08c6a-cc08-4498-8772-242111f9840c", ... ],
            "eventTypes": [ "7a78f5bb-d8c3-4997-89b7-cae72713b7db", ... ]
        },
        ...
    ]

    "modifier" must be "include" or "exclude".
    "resourceTypes" must be a list of resource types or [ "*" ]. Available values include: "hardware", "cameras", "microphones", "outputs", "inputEvents", "userDefinedEvents", etc.
    "sourceIds" must be a list of source ids (GUIDs) or [ "*" ]
    "eventTypes" must be a list of event ids (GUIDs) or [ "*" ]

    response["status"] will contain the status - 200 indicates subscription was successfully created.
    response["subscriptionId"] will contain an id used to unsubscribe.
    """
    return await send_command(web_socket, {
        "command": "addSubscription",
        "filters": filters
    })


async def get_state(web_socket):
    """ Get state based on current subscriptions.
    
    response["status"] will contain the status - 200 indicates success.
    response["states"] will contain a list of states / statuful events.
    """
    return await send_command(web_socket, {
        "command": "getState",
    })


async def receive_events(web_socket):
    """ Wait for events to be received.
    
    response["events"] will contain a list of events.
    """
    msg = await web_socket.recv()
    events = json.loads(msg)

    if "events" not in events:
        raise Exception(f"Unexpected message received: {msg}")

    return events


async def send_command(web_socket, command):
    global command_id
    command_id += 1
    command["commandId"] = command_id

    await web_socket.send(json.dumps(command))

    # Read from the websocket until we get a command response (discarding any other messages - e.g. events)
    response = {}
    while "commandId" not in response or response["commandId"] != command_id:
        msg = await web_socket.recv()
        response = json.loads(msg)

    # Raise an exception with errorText if the status does not indicate success
    if not 200 <= response["status"] <= 299:
        raise Exception(f"Command failed. {response['status']}: {response['error']['errorText']}")

    return response


