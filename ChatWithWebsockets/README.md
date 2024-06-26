---
description: This sample demonstrates how to build a realtime chat with the WebSockets Messages API through the API Gateway from a JavaScript-based application.
keywords:
- Protocol integration
- WebSockets Messages API
- Message Communication
- API Gateway
- Identity Provider
- Messages
lang: en-US
title: Chat with WebSockets Messages API - JavaScript
--- 

# Chat with WebSockets Messages API - JavaScript [BETA]

This sample shows how to publish and subscribe to messages from a JavaScript browser application.

The sample gets a token from IDP, connects to the messaging service, subscribes and publishes messages.

Open the sample in two different tabs and send messages between them. You can also test the sample with a Chat plugin sample running in another XProtect component like Smart Client (see _mipsdk-samples-plugin/ChatWithWebsockets_).

This sample is not production ready and has been kept simple in purpose. Learn about:

- [Handling reconnections](/mipvmsapi/api/messages-ws/v1/#section/API-reference/Session-lifecycle)
- [Receiving acknowledgements](/mipvmsapi/api/messages-ws/v1/#section/API-reference/Ack)

To learn more about the WebSockets Messages API, [read the full docs](/mipvmsapi/api/messages-ws/v1/).

The WebSockets Messages API is in beta version. I.e., both the API and this sample might change without preserving backwards compatibility as long as it is in beta.

## Prerequisites

- XProtect 2023 R2 or later.
- The API Gateway installed on the same host as the management server.
- The API Gateway configured to [allow CORS](/mipvmsapi/content/cors/).
- A basic user.
 
## The sample demonstrates

- How to login using OpenID Connect/OAuth2 from a JavaScript application
- How to subscribe to message topics
- How to publish messages
- How to send pulse messages

## Using

- WebSockets Messages API

## Related samples

- mipsdk-samples-plugin/ChatWithWebsockets

## Environment

- None
