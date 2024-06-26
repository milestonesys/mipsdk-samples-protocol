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

    # Get an existing user defined event type
    response = requests.get(
        f"{serverUrl}/api/rest/v1/userDefinedEvents", headers=headers, verify=verify
    )
    if response.status_code != 200:
        error = response.json()["error"]
        print(error)
        return

    user_defined_events = response.json()["array"]
    print(f"Retrieved {len(user_defined_events)} user defined event types")

    if len(user_defined_events) == 0:
        print(f"You need to have at least one user defined event to run this sample")
        return

    event_type = user_defined_events[0]
    print(f"Triggering an event for event type {event_type['id']}")

    # Trigger an event
    response = requests.post(
        f"{serverUrl}/api/rest/v1/events",
        headers=headers,
        data=json.dumps({"type": event_type["id"]}),
        verify=verify,
    )
    if response.status_code != 202:
        error = response.json()
        print(error)
        return

    event = response.json()["data"]
    print(f"Triggered an event: {event}")

    # Retrieve the first 10 events with additional event data
    response = requests.get(
        f"{serverUrl}/api/rest/v1/events?page=0&size=10&include=data", headers=headers, verify=verify
    )
    if response.status_code != 200:
        error = response.json()
        print("Unable to retrieve events.", error)
        return

    events = response.json()["array"]
    print(f"Retrieved first page of events: {events}")

    # Retrieve an event by id
    response = requests.get(
        f"{serverUrl}/api/rest/v1/events/{events[-1]['id']}",
        headers=headers,
        verify=verify,
    )
    if response.status_code != 200:
        error = response.json()
        print(
            "Unable to retrieve event.",
            f"Make sure that the event type retention of {event_type['name']!r} is greater than zero.",
            error,
        )
        return

    event = response.json()["data"]
    print(f"Retrieved an event: {event}")


if __name__ == "__main__":
    main()
