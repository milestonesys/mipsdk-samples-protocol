import json
import requests
import time
import identity_provider
from api_gateway import Gateway

def main():
    username = 'basicUserName' # Replace with an XProtect basic user with the XProtect Administrators role
    password = 'BasicPWD' # Replace with password for basic user
    serverUrl = 'https://managementserver.domain' # Replace with the hostname of the management server, assuming that the API Gateway has been installed on the same host

    # First we need a session to ensure that we stay logged in
    session = requests.Session()

    # Now authenticate using the identity provider and get access token
    response = identity_provider.get_token(session, username, password, serverUrl)
    if response.status_code == 200:
        token_response = response.json()
        print(f"IDP access token response:\n{token_response}\n\n")
        access_token = token_response['access_token'] # The token that we'll use for RESTful API calls
    else:
        error = response.json()['error']
        print(error)
        return

    # Create an API Gateway
    api_gateway = Gateway(serverUrl)


    # Demo of creating, updating, and deleting a user-defined event through the API Gateway
    crud_user_defined_event(api_gateway, session, access_token)

    
    # Demo of invoking a task through the API Gateway
    # NOTE To run the tasks demonstration below, the GUID of a PTZ camera is needed
    # 1. In Management Client, find a PTZ camera 
    # 2. Get a list of all cameras
    # 3. Find the PTZ camera and assign the id value to cameraGuidId
    # 4. Uncomment the commented region below

    # response = api_gateway.get(session, 'cameras', access_token)  # Get all cameras
    # if response.status_code == 200:
    #     cameras_array = response.json()['array']
    #     print(f"Cameras:\n{json.dumps(cameras_array, indent=2)}\n\n")
    # else:
    #     error = response.error
    #     print(error)
    #     return
    # cameraGuidId = 'a87d2b67-e37f-491e-b5b3-d058e9b48fa2'        # Replace value with the id of a PTZ camera
    # cameras_and_tasks(api_gateway, session, access_token, cameraGuidId)


def crud_user_defined_event(api_gateway: Gateway, session: requests.Session, token: str):
    """Create, update, and delete a user-defined event"""

    # Create a user defined event
    payload = json.dumps({'name': 'my Python event'})
    response = api_gateway.create_item(session, 'userDefinedEvents', payload, token)
    if response.status_code == 201:
        create_result = response.json()['result']
        print(f"Create item result:\n{create_result}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return

    # Get the user defined event that we just created
    event_id = create_result['id']
    response = api_gateway.get_single(session, 'userDefinedEvents', event_id, token)
    if response.status_code == 200:
        event_data = response.json()['data']
        print(f"Get item data:\n{event_data}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return

    # Update the user defined event
    payload = json.dumps({'name': 'my updated Python event'})
    response = api_gateway.update_item(session, 'userDefinedEvents', payload, event_id, token)
    if response.status_code == 200:
        update_data = response.json()['data']
        print(f"Update item data:\n{update_data}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return

    # Delete the user defined event
    response = api_gateway.delete_item(session, 'userDefinedEvents', event_id, token)
    if response.status_code == 200:
        delete_item_state = response.json()
        print(f"Delete item state:\n{delete_item_state}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return


def cameras_and_tasks(api_gateway: Gateway, session: requests.Session, token: str, camera_id: str):
    """Find, invoke, and clean up a camera device task"""

    # Get a list of available tasks on camera PTZ presets
    response = api_gateway.get_child_item_tasks(session, 'cameras', camera_id, 'ptzpresets', token)
    if response.status_code == 200:
        preset_tasks = response.json()['tasks']
        print(f"PTZ preset tasks:\n{preset_tasks}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return

    # Assert that a GetDevicePresets task is available
    get_device_presets_task = None
    for task in preset_tasks:
        if task['id'] == 'GetDevicePresets':
            get_device_presets_task = task
    if get_device_presets_task is None:
        print(f"Camera PTZ presets does not have a GetDevicePresets task, use another camera\n\n")
        return

    # Invoke a task, in this case a task for retrieving PTZ presets from the device
    # sessionDataId should be set to 0 when invoking a new task. The purpose is to be able to approve special two steps tasks
    payload = json.dumps({'sessionDataId': 0})
    response = api_gateway.perform_child_task(session, 'cameras', camera_id, 'ptzpresets', 'GetDevicePresets', payload, token)
    if response.status_code == 200:
        task_result = response.json()['result']
        task_id = task_result['path']['id']
        print(f"GetDevicePresets task result:\n{task_result}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return

    # Poll the status of the task started in the previous step
    task_status = ''
    while task_status != 'Success':
        response = api_gateway.get_single(session, 'tasks', task_id, token)
        if response.status_code == 200:
            task_data = response.json()['data']
            task_status = task_data['state']
            print(f"GetDevicePresets task data:\n{task_data}\n\n")
        else:
            error = response.json()['error']
            print(error)
            return
        time.sleep(0.5)

    # Clean up the task. This is done by starting a TaskCleanup task on the task.
    response = api_gateway.perform_task(session, 'tasks', task_id, 'TaskCleanup', '{}', token)
    if response.status_code == 200:
        task_result = response.json()['result']
        print(f"TaskCleanup task result:\n{task_result}\n\n")
    else:
        error = response.json()['error']
        print(error)
        return

if __name__ == '__main__':
    main()
