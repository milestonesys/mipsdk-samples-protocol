"""
Get a bearer access token from the identity provider
"""
import requests
from requests_ntlm import HttpNtlmAuth


def get_token(
    session: requests.Session,
    username: str,
    password: str,
    serverUrl: str,
    isBasicUser: bool,
    verify: bool
) -> str:
    """
    Requests an OAuth 2.0 access token from the identity provider on a VMS server for a VMS user.
    The API Gateway forwards the request to the identity provider

    :param session: A requests.Session object which will be used for the duration of the
        integration to maintain logged-in state
    :param username: The username of an XProtect user with the XProtect Administrators role
    :param password: The password of the user logging in
    :param server: The hostname of the machine hosting the identity provider, e.g. "vms.example.com"
    :param isBasicUser: Defines whether the login should be done using basic authentication

    :returns: session.Response object. The value of the 'access_token' property is the bearer token.

        Note the "expires_in" property; if you're planning on making a larger integration, you will
        have to renew before it has elapsed.
    """

    if isBasicUser:
        return get_token_basic(session, username, password, serverUrl, verify)
    return get_token_windows(session, username, password, serverUrl, verify)


def get_token_basic(
    session: requests.Session, username: str, password: str, serverUrl: str, verify: bool
) -> str:

    url = f"{serverUrl}/API/IDP/connect/token"
    headers = {"Content-Type": "application/x-www-form-urlencoded"}
    payload = f"grant_type=password&username={username}&password={password}&client_id=GrantValidatorClient"
    return session.request("POST", url, headers=headers, data=payload, verify=verify)


def get_token_windows(
    session: requests.Session, username: str, password: str, serverUrl: str, verify: bool
) -> str:
    # Get the token directly from the identity provider as MIP VMS RESTful API gateway doesn't support pass-through of NTLM authentication
    url = f"{serverUrl}/IDP/connect/token"
    headers = {"Content-Type": "application/x-www-form-urlencoded"}
    payload = f"grant_type=windows_credentials&client_id=GrantValidatorClient"
    return session.request(
        "POST",
        url,
        headers=headers,
        data=payload,
        verify=verify,
        auth=HttpNtlmAuth(username, password),
    )
