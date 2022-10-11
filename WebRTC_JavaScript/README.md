---
description: This sample uses a RESTful API for the signaling required to establish a WebRTC connection to an XProtect VMS through the API Gateway.
keywords:
- Protocol integration
- RESTful communication
- API Gateway
- WebRTC
- ICE candidates
lang: en-US
title: WebRTC - JavaScript client
---

# WebRTC - JavaScript client

> WebRTC support in XProtect 2022 R3 is a pre-release. A stable release of this feature is expected in XProtect 2023 R1. Changes may occur before the stable release.

This sample uses a RESTful API for the signaling required to establish a WebRTC connection to an XProtect VMS through the API Gateway.

## Prerequisites

- XProtect 2022 R3 or later.
- Camera with H.264 protocol.
- An XProtect basic user, created locally, with access to the camera
- WebRTC enabled on the server where the API Gateway is installed.
- Either not behind a symmetric NAT, or mDNS disabled in browser, see [Limitations](#limitations).
- Chrome or Edge web browser.

## Setting up

The most simple setup is on a single computer installation, that is, with XProtect management server and API Gateway on the same host. A distributed installation will require a bit more setup.

1. [Enable WebRTC support](#enable-webrtc-support) on the API Gateway host.
2. If the sample web page is not served from the same host URL as the API Gateway, you must configure [API Gateway CORS settings](#api-gateway-cors-settings).

You can either open the sample webpage `index.html` in a browser directly from the sample directory, or host the sample directory on a web server. If you want to use the **Username** and **Password** fields to get a bearer token, you must host the sample directory on a web server:

1. Copy the `WebRTC_JavaScript` sample directory to a host with a web server, for example your XProtect management server host.
2. Configure the web server to serve the sample directory at, for example, `/webrtc`.

### Enable WebRTC support

WebRTC support is not enabled by default in XProtect 2022 R3.

To enable WebRTC support on the API Gateway host:

1. Add the following DWORD value to the registry and set it to `1`:

   ```ini
   Windows Registry Editor Version 5.00

   [HKEY_LOCAL_MACHINE\SOFTWARE\VideoOS\Server\Gateway]
   "EnableWebRTC"=dword:00000001
   ```

2. Restart the IIS, or at least recycle `VideoOS ApiGateway AppPool`.

### API Gateway CORS settings

If the sample web page is not served from the same origin host URL as the API Gateway, requests to the API Server from the sample script will be blocked unless the API Gateway is configured to support CORS (Cross-Origin Resource Sharing).

You enable and configure CORS support in `%ProgramFiles%\Milestone\XProtect API Gateway\appsettings.json`. By default, CORS is not enabled.

For development and test purposes, you can use a very permissive policy:

1. Edit the CORS section in `appsettings.json`, for example:

   ```json
   "CORS": {
       "Enabled": true,
       "Access-Control-Allow-Origin": "*",
       "Access-Control-Allow-Headers": "*",
       "Access-Control-Allow-Methods": "*"
     }
   ```

2. Restart the IIS, or at least recycle `VideoOS ApiGateway AppPool`.

This will allow calls from any origin host to the API Gateway.

> In a production environment, this is not secure, and CORS should be configured correctly in order to keep the system secure.

### STUN server address

To help establish a connection through NATs, WebRTC uses a STUN (Session Traversal Utilities for NAT) server. By default, the sample and the API Gateway use the Google STUN server `stun1.l.google.com:19302`.

If you want to change this, you must edit the STUN server address in two places:

1. In the sample, the STUN server address is declared near the top of `main.js`.
2. On the API Gateway host, the STUN server address is set in `%ProgramFiles%\Milestone\XProtect API Gateway\GatewayConfig.json`.
3. Restart the website that hosts the API Gateway, typically `Default Web Site`, for changes in `GatewayConfig.json` to take effect.

## Running

![WebRTC - JavaScript client](WebRTC-JavaScriptClient.png)

1. Is the sample web page served from a web server?
   1. If yes, open the URL from where the sample is served in your browser. If the sample is served from the management server host, the URL could be something like `https://managementServer.example.com/webrtc`.
   2. If no, open `index.html` in your browser directly from the sample directory.
2. Enter the URL of the API Gateway. Usually, the API Gateway is installed on the same host as the management server, that is, the API Gateway will be something like `https://managementServer.example.com/api`.
3. [Get the CameraId](#get-a-cameraid) of a camera that supports H.264.
4. Enter the `CameraId`.
5. Is the sample web page served from the same host URL as the management server?
   1. If yes, enter management server host URL, Username and Password, and select **Login** to retrieve a bearer token.
   2. If not, you must [get a bearer token](#get-a-bearer-token) by other means and copy it to the **Token** field.
6. Select **Start** to establish the connection.

The bearer token will expire after one hour.

### Get a CameraId

You can use the Management Client to get a `CameraId`:

1. In the **Site Navigation** pane, select **Servers** and then select the recording server.
2. Select a camera that support H.264 in the **Overview** pane.
3. Select the **Info** tab in the **Properties** pane.
4. Ctrl+Click the video image in the **Preview** pane.  
   The camera ID will be displayed at the bottom of the **Properties** pane.

### Get a bearer token

You won't be able to retrieve a bearer token using the **Username** and **Password** fields if the sample web page is not served from the same host URL as the management server.

Instead, you can get a bearer token using one of the methods described at [Milestone Integration Platform VMS API Reference - Verify that the API Gateway is operational](https://doc.developer.milestonesys.com/mipvmsapi/#verify-that-the-api-gateway-is-operational), step 5.

## Description

We suggest you look at the `main.js` code while reading the following steps:

1. In `initiateWebRTCSession()`, the session is initiated by a POST request to `API/WebRTC/v1/WebRTCSession`.  
   The request body contains the `cameraId` (and `resolution` which may be supported in the future).
2. The response contains the newly created `sessionId` and the `offerSDP` which is used to update the `RTCPeerConnection` object `pc`.
3. An `answerSDP` is created based on `pc`, and it is sent to the session by a PUT request to `API/WebRTC/v1/WebRTCSession`.
4. Next, ICE candidates will be exchanged in two methods: `addServerIceCandidates()` polls the API Gateway server while the connection state is `new` or `checking`, and `SendIceCandidate()` is called every time `pc` finds an ICE candidate.
5. Once ICE candidates have been exchanged, WebRTC will try to establish a connection between the peers.

For general information about WebRTC, please refer to:
[Mozilla WebRTC Developer guide](https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Signaling_and_video_calling)

## Limitations

### No WebRTC connection through a symmetric NAT firewall

WebRTC cannot create a connection through a symmetric NAT firewall without using a TURN (Traversal Using Relays around NAT) server. Currently, the API Gateway does not support using TURN. Check with your system administrator if you are behind a symmetric NAT firewall. In that case, a WebRTC connection is possible only within the firewall, that is, both your browser and the API Gateway must be behind the firewall.

### WebRTC connection behind a symmetric NAT firewall

By default, browsers use mDNS (multicast DNS) to obfuscate the browser address when running behind a symmetric NAT firewall. The API Gateway does not support mDNS, and that will block STUN communication. To run the sample behind a symmetric NAT firewall, mDNS must be disabled in your browser. This can be done by opening `chrome://flags` or `edge://flags` and setting **Anonymize local IPs exposed by WebRTC** to `Disabled`.

### The internal IDP does not support CORS

If the sample web page is not served from the same origin host URL as the management server, requests to the internal IDP from the sample script will be blocked. That is, you won't be able to retrieve a bearer token using the **Username** and **Password** fields, and must get a bearer token by other means.

## Troubleshooting tips

Open your browser Developer tools and select the Console tab to see connection progress and error messages.

Error responses from the API Gateway are returned with error details in the response body as a JSON object. To see error responses from the API Gateway, debug to the location where the response is received, and inspect the response body.

Errors are sometime presented in the browser as CORS error without being actual CORS issues. If you see a CORS error message in the browser, it could be related to configuration issues in the IIS. Open your browser Developer tools and select the Network tab. If it is not an CORS error, the actual error will be shown here in the messages received before the CORS error.

In case of a IIS crash with only very general error information, try temporarily enabling 32-Bit Applications in the IIS application pool `VideoOS ApiGateway AppPool`. If WebRTC is the cause of the crash, enabling 32-Bit Applications may provide you with more detailed error information.

## The sample demonstrates

- The signaling required to set up a WebRTC connection with an XProtect VMS
- Using an OAuth2 bearer token to authenticate WebRTC signaling and media streaming

## Using

- A simple WebRTC client based on [RTCPeerConnection](https://developer.mozilla.org/en-US/docs/Web/API/RTCPeerConnection)
- A RESTfull API for WebRTC signaling with an XProtect API Gateway
