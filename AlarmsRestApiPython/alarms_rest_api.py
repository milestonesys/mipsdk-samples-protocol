import json
import requests
import identity_provider
import urllib3


def main():
    username = (
        "username"  # Replace with an XProtect user with the XProtect Administrators role
    )
    password = "password"  # Replace with password for the user, never hardcode credentials in a real usecase
    isBasicUser = True  # Set accordingly to the authentication used: True for basic authentication and False for Windows authentication
    serverUrl = "https://managementserver.domain"  # Replace with the hostname of the management server, assuming that the API Gateway has been installed on the same host
    verify = True  # Set to false to avoid verifying certificates in development

    if not verify:
        urllib3.disable_warnings(
            urllib3.exceptions.InsecureRequestWarning
        )  # Remove this line if verifying the certificate (which is recommended)

    # First we need a session to ensure that we stay logged in
    session = requests.Session()

    # Now authenticate using the identity provider and get access token
    response = identity_provider.get_token(
        session, username, password, serverUrl, isBasicUser, verify
    )
    if response.status_code == 200:
        token_response = response.json()
        print(f"IDP access token response:\n{token_response}\n\n")
        access_token = token_response[
            "access_token"
        ]  # The token that we'll use for RESTful API calls
    else:
        error = response.json()["error"]
        print(error)
        return

    headers = {
        "Authorization": f"Bearer {access_token}",
        "Content-type": "application/json",
    }

    # Get an existing camera
    response = requests.get(
        f"{serverUrl}/api/rest/v1/cameras", headers=headers, verify=verify
    )
    if response.status_code != 200:
        error = response.json()["error"]
        print(error)
        return

    cameras = response.json()["array"]
    print(f"Retrieved {len(cameras)} cameras")

    if len(cameras) == 0:
        print(f"You need to have at least one cameras to run this sample")
        return

    # Trigger an alarm
    response = requests.post(
        f"{serverUrl}/api/rest/v1/alarms",
        headers=headers,
        data=json.dumps(
            {
                "name": "my first alarm name",
                "message": "my first alarm message",
                "source": cameras[0]["id"],
            }
        ),
        verify=verify,
    )
    if response.status_code != 202:
        error = response.json()
        print(error)
        return

    alarm = response.json()["data"]
    print(f"Triggered an alarm: {alarm}")

    # Retrieve an alarm by id
    response = requests.get(
        f"{serverUrl}/api/rest/v1/alarms/{alarm['id']}?include=data",
        headers=headers,
        verify=verify,
    )
    if response.status_code != 200:
        error = response.json()
        print(
            "Unable to retrieve alarm.",
            error,
        )
        return

    alarm = response.json()["data"]
    print(f"Retrieved alarm with id={alarm['id']}: {alarm}")

    # Retrieve a random state
    response = requests.get(
        f"{serverUrl}/api/rest/v1/alarmStates", headers=headers, verify=verify
    )
    if response.status_code != 200:
        error = response.json()
        print(
            "Unable to retrieve alarm states.",
            error,
        )
        return

    alarm_state = response.json()["array"][0]
    print(f"Retrieved alarm state with id={alarm_state['id']}: {alarm_state}")

    # Retrieve a random priority
    response = requests.get(
        f"{serverUrl}/api/rest/v1/alarmPriorities", headers=headers, verify=verify
    )
    if response.status_code != 200:
        error = response.json()
        print(
            "Unable to retrieve alarm priorities.",
            error,
        )
        return

    alarm_priority = response.json()["array"][-1]
    print(f"Retrieved alarm priority with id={alarm_priority['id']}: {alarm_priority}")

    # Update an alarm state and priority
    response = requests.patch(
        f"{serverUrl}/api/rest/v1/alarms/{alarm['id']}",
        data=json.dumps(
            {
                "state": alarm_state["id"],
                "priority": alarm_priority["id"],
                "comment": None,
                "assignedTo.displayName": None,
                "reasonForClosing": None,
            }
        ),
        headers=headers,
        verify=verify,
    )
    if response.status_code != 202:
        error = response.json()
        print(
            "Unable to update alarm.",
            error,
        )
        return

    alarm = response.json()["data"]
    print(f"Updated alarm with id={alarm['id']}: {alarm}")

    # Retrieve first 10 alarms
    response = requests.get(
        f"{serverUrl}/api/rest/v1/alarms?page=0&size=10", headers=headers, verify=verify
    )
    if response.status_code != 200:
        error = response.json()
        print(
            "Unable to retrieve alarms.",
            error,
        )
        return

    alarms = response.json()["array"]
    print(f"Retrieved all alarms: {alarms}")


if __name__ == "__main__":
    main()
