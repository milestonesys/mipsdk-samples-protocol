"""
Main entrypoint for this sample
"""

import asyncio
from websockets import connect
import menu
import keyboard
import ess_api
import event_types
import event_viewer
import state_viewer


subscription_filters =  [
    {
        "modifier": "include",
        "resourceTypes": [ "cameras" ],
        "sourceIds": [ "*" ],
        "eventTypes": [ event_types.motion_started, event_types.motion_stopped, event_types.recording_started, event_types.recording_stopped ]
    }
]


async def check_for_escape():
    while True:
        if keyboard.is_pressed("esc"):
            return
        await asyncio.sleep(0.1)  


async def main(gateway_uri, access_token, mode):
    session_id = ""
    last_event_id = ""

    # Start task checking for escape key
    escape_task = asyncio.create_task(check_for_escape())
    
    if mode == "stateviewer":
        viewer = state_viewer
    elif mode == "eventviewer":
        viewer = event_viewer

    # Connection loop: Reconnect on failure
    while not escape_task.done():
        try:
            viewer.display_header()

            if "https" in gateway_uri.split("//")[0]:
                connection_uri = f"wss://{gateway_uri.split('//')[1]}/api/ws/events/v1/"
            else:
                connection_uri = f"ws://{gateway_uri.split('//')[1]}/api/ws/events/v1/"

            receive_events_task = None

            async with connect(connection_uri, additional_headers={"Authorization": f"Bearer {access_token}"}) as web_socket:


                # Start or resume session
                session = await ess_api.start_session(web_socket, session_id, last_event_id)

                state_events = []
                if session["status"] == 201:
                    # Create subscription
                    subscription = await ess_api.create_subscription(web_socket, subscription_filters)
                    
                    session_id = session["sessionId"]

                    # Get state
                    if mode == "stateviewer":
                        state = await ess_api.get_state(web_socket)
                        state_events = state["states"]

                await viewer.process_events(state_events, gateway_uri, access_token)

                # Receive events loop
                while not escape_task.done():
                    receive_events_task = asyncio.create_task(ess_api.receive_events(web_socket))

                    # Wait for either events to be received or user escape
                    await asyncio.wait([receive_events_task, escape_task], return_when=asyncio.FIRST_COMPLETED)
                    if escape_task.done():
                        break

                    events = await receive_events_task
                    last_event_id = events["events"][-1]["id"]

                    # Process events
                    await viewer.process_events(events["events"], gateway_uri, access_token)

            # Await the receive_events_task to observe exception
            if receive_events_task is not None:
                await receive_events_task

        except Exception as e:
            if not escape_task.done():
                print(f"[!] {e}")
                print("[*] RECONNECTING...")
                await asyncio.sleep(1)


if __name__ == '__main__':
    while True:
        gateway_uri, access_token, mode = menu.show()
        asyncio.run(main(gateway_uri, access_token, mode))
