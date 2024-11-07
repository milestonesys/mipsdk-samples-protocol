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
<!-- markdownlint-disable MD024 MD033 -->

# WebRTC - JavaScript client

This sample uses a RESTful API for the signaling required to establish a WebRTC connection with an XProtect VMS through the API Gateway.

## Prerequisites

- XProtect 2023 R1 or later (stream, playback and TURN server configuration requires 2023 R3).
- Camera with H.264 protocol.
- An XProtect basic user, created locally, with access to the camera.
- Chrome, Edge, Firefox, or Safari web browser.
- CORS configuration if the sample webpage is not served from the same origin host URL as the API Gateway, see [API Gateway CORS settings][cors].
- If both browser and the API Gateway are on a local private network:
  - Both on same local network segment and API Gateway supports mDNS,  
    see [WebRTC connection on a local network uses mDNS][locm], or
  - Routers on local network does not block `X-Forwarded-For` or `Remote_Addr` headers,  
    see [WebRTC connections across routers in a local network][loch], or
  - mDNS disabled in the browser,  
    see [Disable browser mDNS support][disa].
- If there's a NAT between browser and the API Gateway, you'll need a STUN or TURN server:
  - A TURN server is required if there's a *symmetric NAT* between browser and the API Gateway. TURN servers usually also function as STUN servers.
  - A STUN server is required if there's a *non-symmetric (Full Cone) NAT* between browser and the API Gateway.

## Set up WebRTC sample

You can open the sample webpage `index.html` in a browser directly from the sample directory, or host the sample directory `WebRTC_JavaScript` on a web server.

If you intend to serve the sample directly from the sample directory:

1. Configure [API Gateway CORS settings][cors].

If you intend to serve the sample from a web server:

1. Copy the `WebRTC_JavaScript` sample directory to a host with a web server, for example the API Gateway host.
2. Configure the web server to serve the sample directory at, for example, `/WebRTC_JavaScript`.
3. If the sample webpage is *not* served from the same host URL as the API Gateway, you must configure [API Gateway CORS settings][cors].

## Configuration files

API Gateway configuration files are located in the installation location, by default `%ProgramFiles%\Milestone\XProtect API Gateway\`.

These configuration files are relevant for the sample:

- `appsettings.production.json`: Overrides the configuration settings in `appsettings.json`.
- `appsettings.json`: Reverse proxy (routing), CORS, WebRTC, log levels, etc.

> Do not edit `appsettings.json` manually. This file is created by the product installer and maintained by the **Server Configurator**.

If needed, create `appsettings.production.json` and add configuration overrides to this file. This file will not be removed or changed by product updates.

> Use a validating editor to edit configuration files. Most popular code editors support JSON and XML syntax validation, either by default or through extensions.

Syntax errors in the API Gateway configurations files result in `502 Bad Gateway` or `503 Service Unavailable` server errors and will show up in the Windows Application event log and the IIS request log.

### API Gateway CORS settings

If the sample webpage is not served from the same origin host URL as the API Gateway, the browser will block requests to the API Gateway from the sample scripts unless the API Gateway is configured to support CORS (Cross-Origin Resource Sharing).

CORS is disabled by default. You enable and configure CORS support by creating and editing `appsettings.production.json`.

1. Create `appsettings.production.json` (if not already created).

2. For development and test purposes, you can use a very permissive policy:

   ```json
   {
     "CORS": {
       "Enabled": true,
       "Access-Control-Allow-Origin": "*",
       "Access-Control-Allow-Headers": "*",
       "Access-Control-Allow-Methods": "*"
     }
   }
   ```

   This will allow calls from any origin, including a local file system, to the API Gateway.

   > In a production environment, this is not secure, and CORS should be configured correctly in order to keep the system secure.

3. Restart the IIS, or at least recycle `VideoOS ApiGateway AppPool`.

For more information about CORS, please refer to [Cross-Origin Resource Sharing (CORS)][mdn-cors].

## Running

![WebRTC - JavaScript client](WebRTC-JavaScriptClient.png)

1. Open index.html in a browser
2. Enter the URL of the API Gateway.  
   Usually, the API Gateway is installed on the same host as the management server, that is, the API Gateway will be something like `https://managementServer.example.com/api`.
3. Enter the **CameraId** of a camera that supports H.264.  
   See [Get the CameraId](#get-a-cameraid).
4. Optionally, enter a **StreamId**. See [Get a StreamId](#get-a-streamid).  
5. Optionally, enter an ISO 8601-formatted datetime string in **Playback Time** to request video playback instead of live video.
6. Optionally, select **Skip Gaps** to skip gaps between video sequences in video playback.
7. Optionally, enter a number in **Playback Speed**. The default is 1.0, and any value > 0 and ≤ 32 is valid.
8. Enter **Username** and **Password** of a basic user with access to the camera.
9. Optionally, enter a STUN server address, for example `stun:stun1.l.google.com:19302`.
10. Optionally, enter a TURN server address, username, and credential (password).
11. Select **Start** to establish the connection.

### Get a CameraId

You can use the Management Client to get a `CameraId`:

1. In the **Site Navigation** pane, select **Servers** and then select the recording server.
2. Select a camera that support H.264 in the **Overview** pane.
3. Select the **Info** tab in the **Properties** pane.
4. Ctrl+Click the video image in the **Preview** pane.  
   The camera ID will be displayed at the bottom of the **Properties** pane.

### Get a StreamId

Cameras may have multiple streams. You can specify a `StreamId` to request a specific stream. If the connection is initiated without a `StreamId`, the camera's default stream will be used.

If an invalid `StreamId` is specified, a WebRTC session cannot be initiated, and the diagnostics log will display an error message.

You can use the Management Client to get a `StreamId`:

1. In the **Site Navigation** pane, select **Servers** and then select the recording server.
2. Select the camera hardware in the **Overview** pane.
3. Unfold the **camera hardware** and select the **camera device**.
4. Select the Streams tab in the **Properties** pane.
5. Ctrl+Click the video image in the **Preview** pane.
6. Select the **Stream XML** button.
7. A small XML document describing available streams will open in your default XML application.
8. Use the value of a `<referenceid>` element of an H.264 as `StreamId`.

## Description

Please look at the `main.js` code while reading the following steps:

1. In `initiateWebRTCSession()`, the session is initiated by a POST request to `API/REST/v1/WebRTC/Session`.  
   The request body contains
   - the `cameraId`, see [Get a CameraId](#get-a-cameraid)
   - `resolution`, currently not supported
   - optionally a `streamId`, see [Get a StreamId](#get-a-streamid),
   - optionally a `PlaybackTimeNode`, see [Playback of recorded video](#playback-of-recorded-video),
   - `iceServers` see [STUN and TURN server addresses](#stun-and-turn-server-addresses),
2. The response contains the newly created `sessionId` and the `offerSDP` which is used to update the `RTCPeerConnection` object `pc`.
3. An `answerSDP` is created based on `pc`, and it is sent to the session by a PUT request to `API/REST/v1/WebRTC/Session`.
4. Next, ICE candidates will be exchanged in two methods: `addServerIceCandidates()` polls the API Gateway server while the connection state is `new` or `checking`, and `SendIceCandidate()` is called every time `pc` finds an ICE candidate.
5. Once ICE candidates have been exchanged, WebRTC will try to establish a connection between the peers.

The bearer token expires (default after 1 hour). Code for getting and refreshing the token can be found in `tokenCommunication.js`; see the methods `getToken` and `refreshToken`.

For more information about the signaling involved in establishing a WebRTC connection, please refer to: [WebRTC API, Signaling and video calling][mdn-webrtc-sign]

### Playback of recorded video

If a playback start time is provided, recorded video is streamed instead of live video. The API Gateway will start streaming at the requested time. If there's no video recorded at that time and skip gaps is enabled, the stream will immediately forward to the first video sequence after the requested time.

Playback is controlled by including an optional `PlaybackTimeNode` object when initiating the session. `PlaybackTimeNode` is an object taking these values:

- `playbackTime`, a datetime string in [ISO 8601][iso-8601] format.  
  Example: the 10th of July 2020 at 3 PM UTC can be represented as `2020-07-10T15:00:00.000Z` (or `2020-07-10T17:00:00.000+02:00`).
- `skipGaps`, optional boolean. If `true`, gaps between video sequences are skipped during playback; otherwise, no frames are streamed for the duration of the gap.
- `speed`, optional number. Sets the speed at which the video is played back.  The default is 1.0, and any value > 0 and ≤ 32 is valid.

<!--
```json
{
  "$schema": "http://json-schema.org/draft-04/schema#",
  "type": "object",
  "properties": {
    "PlaybackTimeNode": {
      "type": "object",
      "properties": {
        "playbackTime": {
          "type": "string"
        },
        "skipGaps": {
          "type": "boolean"
        },
        "speed": {
          "type": "number"
        }
      },
      "required": [
        "playbackTime"
      ]
    }
  },
  "required": [
    "PlaybackTimeNode"
  ]
}
```
-->

#### How to calculate time for current frame

1. In `start()` in `main()`,
   - `frameStartTime` is set to either `Date.now` or the requested playback time. `frameStartTime` will be considered the start of streaming.
   - A callback function is registered for the `<video>` player. The function will be called next time a frame has been received.
   - The WebRTC session is initiated.
2. In the callback function, the `rtpTimestamp` is extracted from WebRTC frame metadata. `rtpTimestamp` is the time since streaming started in milliseconds.  
   To get `frameDate` (the date and time for the current frame), the `rtpTimestamp` value is added to `frameStartTime`.
3. The function is registered again to be called next time a frame has been received.

To register the callback function, the sample uses `requestVideoFrameCallback()` (currently a W3C Draft Community Group Report) for most browsers, and `requestAnimationFrame()` for FireFox:

- The `requestVideoFrameCallback()` method registers a callback to run when a new video frame has been received. See [HTMLVideoElement.requestVideoFrameCallback()][video-rvfc].
- The `requestAnimationFrame()` method tells the browser to perform an animation and requests that the browser call a specified function to update an animation before the next repaint. See [Window: requestAnimationFrame() method][mdn-raf].

When there are gaps between video sequences:

- If `skipGaps` was not enabled, no frames are streamed during gaps between video sequences.
- If `skipGaps` was enabled, frames from the next video sequence get streamed immediately as if no gap had occurred.

In any case, the `rtpTimestamp` for the next video sequence jumps as if frames had been streamed during the gap.

### STUN and TURN server addresses

To help establish a connection through NATs, WebRTC uses STUN (Session Traversal Utilities for NAT) and/or TURN (Traversal Using Relays around NAT) servers.

A STUN server is used to discover the public IP address and port number of a device behind a NAT.

A TURN server is used to relay traffic between peers when a direct connection is not possible due to firewall or NAT restrictions.

The sample supports specifying one of each in the user interface. More than one STUN and/or TURN server can be specified, but the sample only supports up to one of each.

The values passed on from the user interface are used in:

- `start()` method in `main.js` where an `RTCPeerConnection` instance is created. This makes sure that the client uses the STUN and/or TURN servers passed on from the user interface when generating local ICE candidates.

- `initiateWebRTCSession()` method in `main.js` where `body.iceServers` is populated. This sends the configuration to the API Gateway and makes sure that the API Gateway uses the STUN and/or TURN server defined in the user interface.

No default STUN or TURN server URLs are configured API Gateway-side. To do so, the URLs for STUN and TURN servers can be defined in `appsettings.production.json`:

```json
{
  "WebRTC": {
    "iceServers": [ 
      { "url": "stun:mystun.zyx:3478"}, 
      { "url": "turn:myturn.zyx:5349"} 
    ] 
  }
}
```

While it is no longer necessary to send the configuration to the API Gateway during signaling (in `body.iceServers` in `initiateWebRTCSession()` method), setting `body.iceServers` when creating a `RTCPeerConnection` instance is still required.

> **TURN servers that require username and credential**  
> `appsettings.production.json` cannot be used for a TURN server that require username and credential.

For more information about STUN and TURN, see [WebRTC API STUN][mdn-webrtc-stun] and [WebRTC API TURN][mdn-webrtc-turn].

### Trickle ICE

The API Gateway fully supports [trickle ICE][ietf-trickle-ice], but the sample keeps checking on new candidates from the server **only** while the ICE gathering state is *new* or *checking*. Once the connection between peers has been established, the sample stops polling the API Gateway.

To support getting candidates from the server at any time during the connection, the API Gateway must be polled periodically during the whole duration of the connection.

### WebRTC features in browsers

Each browser has different levels of support for WebRTC features. The sample makes use of the [Adapter.js library][webrtc-adapter] which allows for improved browser compatibility when using [WebRTC API][mdn-webrtc-api].

## Limitations and workarounds

### WebRTC connection on a local network uses mDNS

To prevent private IP addresses from leaking from a local network when running WebRTC applications, modern browsers by default send mDNS (multicast DNS) addresses as ICE Candidates to the signaling server.

#### API Gateway support for mDNS

The signaling server running in the API Gateway supports resolving mDNS addresses when running on a Windows version with native support for mDNS. Native support for mDNS was introduced in Windows version 1809 (October 2018) or later, and is available in any recently updated Windows Server 2019 or Windows 10 installations, and all Windows Server 2022 and Windows 11 installations.

#### WebRTC connections across routers in a local network

mDNS relies on multicast which by default will not pass through routers. This means that in enterprise environments, mDNS will fail in many cases:

- mDNS over wired Ethernet works on the same local network segment, but in more complex network solution (most enterprise environments), mDNS will fail.
- mDNS over WiFi will only work on simple network configurations (as for wired networks). In configurations with WiFi extender or Mesh networks, mDNS will likely fail.

The signaling server running in the API Gateway supports a workaround for connections across routers on a local network. The signaling server will attempt to get the client's local IP network address from `X-Forwarded-For` and `Remote_Addr` headers in the HTTP request and use that to add an ICE Candidate with higher priority than the ICE Candidate with the mDNS address. This will not work in all cases; on some networks, `X-Forwarded-For` is removed and `Remote_Addr` will not contain the local IP address of client.

#### Disable browser mDNS support

As a last resort, you can try disabling browser mDNS support to force the browser to reveal the local IP network address in WebRTC connections.

In Chromium-based browsers, mDNS support can be disabled by opening `chrome://flags` or `edge://flags` and setting **Anonymize local IPs exposed by WebRTC** to `Disabled`.

## Troubleshooting tips

### CORS errors

#### Symptoms

- Sample Diagnostics log messages: "Failed to retrieve token", "Failed to initiate WebRTC session".
- Browser Developer tools Console shows CORS errors:

  ```txt
  Access to fetch at 'http://test-01/api/IDP/connect/token' from origin 'http://localhost' has been blocked by CORS policy: . . .
  Access to fetch at 'http://test-01/api/REST/v1/WebRTC/Session' from origin 'http://localhost' has been blocked by CORS policy: . . .
  ```

#### Cause

The sample is not served from same host server URL as the API Gateway, and CORS support has not been enabled.

#### Remedy

Enable CORS support as described in [API Gateway CORS settings][cors].

#### Cause

Errors are sometime presented in the browser as CORS error without being actual CORS issues. If you see a CORS error message in the browser, it could be related to configuration issues in the IIS.

#### Remedy

Open your browser Developer tools and select the Network tab. If it is not an CORS error, the actual error will be shown here in the messages received before the CORS error.

#### Cause

CORS error can occur if using Firefox and self-signed certificates.

#### Remedy

Firefox does not use the Windows Certificate Store, so importing the certificate into Trusted Root Certification Authorities will not work in Firefox.
Add the API Gateway server to the certificate administration -> servers in Firefox under security. If your IDP server is hosted at another url, make sure to add this as well.
See following examples:

API Gateway url: myurl.domain/api

IDP url: myurl.domain/IDP

In this example myurl.domain needs to be added to the excemptions.

API Gateway url: myurl.domain/api

IDP url: mysecondurl.domain/IDP

In this example both myurl.domain and mysecondurl.domain need to be added to the excemptions.

### No connection through a symmetric NAT firewall

#### Symptoms

- Sample Diagnostics log show progress but eventually fails with "ICE connection state: disconnected".

#### Cause

WebRTC cannot create a connection *through* a symmetric NAT firewall without using a TURN (Traversal Using Relays around NAT) server. Without using a TURN server, a WebRTC connection is possible only *within* the symmetric NAT firewall, that is, both your browser and the API Gateway must be behind the firewall.

Check with your system administrator if you are behind a symmetric NAT firewall, or run the test described here: [Am I behind a Symmetric NAT?][webrtchacks-sym-nat].

#### Remedy

Specify a TURN server.

See [STUN and TURN server addresses](#stun-and-turn-server-addresses).

### Server errors

#### Symptoms

- Sample Diagnostic log messages: "Failed to retrieve token - SyntaxError: Unexpected token '<'", "Failed to initiate WebRTC session - SyntaxError: Unexpected token '<'".
- Browser Developer tools Network shows error status `502 Bad Gateway` or `503 Service Unavailable`.
- Error events in the Windows Application log and IIS request log.

#### Cause

Syntax errors in the appsettings configuration files will prevent the API Gateway from starting.

#### Remedy

> Do not edit `appsettings.json` manually. This file is created by the product installer and maintained by the **Server Configurator**.

Open and edit `appsettings.production.json` in a validating editor. For more information, see [Configuration][conf].

### IIS crashes

#### Symptoms

- IIS crash with only very general error information.

#### Remedy

Try temporarily enabling 32-Bit Applications in the IIS application pool `VideoOS ApiGateway AppPool`. If WebRTC is the cause of the crash, enabling 32-Bit Applications may provide you with more detailed error information.

### No video shown

#### Symptoms

- A connection is established, but no video is shown.

#### Cause

The video stream is not H.264. The API Gateway supports only H.264.

#### Remedy

Change the camera's codec to H.264 or use a camera that supports H.264.

#### Cause

The requested playback time is more than 24 days before the start of the recorded video. This causes an integer wraparound in the WebRTC code.

#### Remedy

Use a playback time closer than 24 days to the start of recorded video.

## The sample demonstrates

- The signaling required to set up a WebRTC connection with an XProtect VMS
- Using an OAuth2 bearer token to authenticate WebRTC signaling and media streaming

## Using

- A simple WebRTC client based on [RTCPeerConnection][mdn-webrtc-rtcpeer]
- A RESTfull API for WebRTC signaling with an XProtect API Gateway

[disa]: #disable-browser-mdns-support
[conf]: #configuration-files
[cors]: #api-gateway-cors-settings
[locm]: #webrtc-connection-on-a-local-network-uses-mdns
[loch]: #webrtc-connections-across-routers-in-a-local-network

[iso-8601]: https://www.iso.org/iso-8601-date-and-time-format.html
[ietf-trickle-ice]: https://datatracker.ietf.org/doc/html/draft-ietf-mmusic-trickle-ice
[mdn-webrtc-api]: https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/
[mdn-webrtc-sign]: https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Signaling_and_video_calling
[mdn-webrtc-stun]: https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Protocols#stun
[mdn-webrtc-turn]: https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Protocols#turn
[mdn-webrtc-rtcpeer]: https://developer.mozilla.org/en-US/docs/Web/API/RTCPeerConnection
[mdn-cors]: https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS
[mdn-raf]: https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame
[video-rvfc]: https://wicg.github.io/video-rvfc/
[webrtc-adapter]: https://github.com/webrtc/adapter/
[webrtchacks-sym-nat]: https://webrtchacks.com/symmetric-nat/
