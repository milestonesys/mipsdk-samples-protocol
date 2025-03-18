---
description: This sample demonstrates how to connect to the VMS and subscribe to events by means of JavaScript and WebSocket.
keywords:
- Protocol integration
- WebSockets Evenst and Status
- API Gateway
- Identity Provider
lang: en-US
title: Events and State WebSocket - JavaScript
--- 

# Events and State WebSocket - JavaScript

This sample demonstrates how to connect to the VMS and subscribe to events by means of JavaScript and WebSocket.

The sample gets a token from IDP, connects to the EventsAndState WebSocket endpoint. Sets up a session and performs a subscription to events.

This sample is not production ready and has been kept simple in purpose. 

To learn more about the WebSockets Events API, [read the full docs](/mipvmsapi/api/events-ws/v1/).

## Prerequisites

- XProtect 2024 R1 or later.
- The API Gateway installed on the same host as the management server.
- The API Gateway configured to [allow CORS](#api-gateway-cors-settings).
- A basic user must be created in the VMS.

## The sample demonstrates

- How to login using OpenID Connect/OAuth2 from a JavaScript application
- How to create an event sesstion
- How to create an event subscription
- How to handle incoming events

## Using

- WebSockets Events and State API

## Related samples

- mipsdk-samples-plugin/EventsAndStateWebSocketApiPython

## Environment

- None

### API Gateway CORS settings

If the sample webpage is not served from the same origin host URL as the API Gateway, the browser will block requests to the API Gateway from the sample scripts unless the API Gateway is configured to support CORS (Cross-Origin Resource Sharing).

See more details here [CORS settings in the API Gateway](/mipvmsapi/content/cors/).

For more information about CORS, please refer to [Cross-Origin Resource Sharing (CORS)](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS).

