"""
Get a bearer access token for the MIP VMS RESTful API gateway.
"""
import requests


def get_token(session: requests.Session, username: str, password: str, serverUrl: str) -> str:
    """
    Requests an OAuth 2.0 access token from the identity provider on a VMS server for a VMS basic user. 
    The API Gateway forwards the request to the identity provider

    :param session: A requests.Session object which will be used for the duration of the
        integration to maintain logged-in state
    :param username: The username of an XProtect basic user with the XProtect Administrators role
    :param password: The password of the user logging in
    :param server: The hostname of the machine hosting the identity provider, e.g. "vms.example.com"

    :returns: session.Response object. The value of the 'access_token' property is the bearer token.

        Note the "expires_in" property; if you're planning on making a larger integration, you will
        have to renew before it has elapsed.
    """
    url = f"{serverUrl}/API/IDP/connect/token"
    payload = f"grant_type=password&username={username}&password={password}&client_id=GrantValidatorClient"
    headers = {
        'Content-Type': 'application/x-www-form-urlencoded'
        }
    return session.request("POST", url, headers=headers, data=payload, verify=False)
