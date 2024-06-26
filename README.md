---
description:  This repo contains samples that demonstrate MIP SDK protocol integration.
keywords:
- MIP SDK
- Protocol integration
lang: en-US
title: Milestone Integration Platform Software Development Kit protocol integration samples
---

# Milestone Integration Platform Software Development Kit protocol integration samples

The Milestone Integration Platform (MIP) enables fast and flexible integration between
Milestone XProtect video management software (VMS), applications available from
[Milestone Technology Partner Finder](<https://www.milestonesys.com/partners/technology-partners/technology-partner-finder/>),
and other third-party applications and devices.

The Milestone Integration Platform Software Development Kit (MIP SDK) offers a suite of integration options, including

- protocol integration
- component integration (stand-alone applications using MIP .NET libraries)
- plug-in integration (hosted by XProtect application environments)

This repo contains samples that demonstrate MIP SDK protocol integration:

| Sample                                 | Description                                                                                                                         |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| Alarm List                             | How to retrieve a short list of already generated alarms from the event server.                                                     |
| Bookmark Creator                       | How bookmarks can be created, maintained and deleted via SOAP protocol.                                                             |
| Login .NET SOAP                        | The most simple C\# code for performing a login.                                                                                    |
| OAuth Login Flow                       | How to log in as a user managed by an external identity provider, for example Okta.                                                 |
| RESTful Alarms API - Python            | How to log in and use the RESTful Alarms API from a Python application.                                                             |
| RESTful Config API - Python            | How to log in and use the RESTful Config API from a Python application.                                                             |
| RESTful Events API - Python            | How to log in and use the RESTful Events API from a Python application.                                                             |
| TCP Video Viewer                       | How to utilize the ImageServer protocol to retrieve a sequence of images.                                                           |
| Analytics Event Trigger via XML        | How analytics events can be submitted to the Event Server via HTTP formatted as XML.                                                |
| Trigger Generic Event                  | How to send a character string to the XProtect Generic Event interpreter.                                                           |
| Trigger Generic Event Stream           | How to send a character string to the XProtect Generic Event interpreter. This sample work continuously on the same TCP/IP session. |
| WebRTC client - JavaScript             | Use a RESTful API to establish a WebRTC connection with an XProtect VMS through the API Gateway                                     |
| Chat with WS Messages API - JavaScript | Use the WebSockets Messages API to publish and subscribe to messages from a JavaScript browser application.                         |
| Event and State WebSocket API - Python | How to access the Events and State WebSocket API through the API Gateway from a Python application.                                 |

A Visual Studio solution file in the `src` folder includes a Visual Studio project for each sample.

## Prerequisites

Please refer to the [MIP SDK Getting Started Guide](https://download.milestonesys.com/MIPSDK/MilestoneMIPSDK_GettingStartedGuide_en-US.pdf)
for information about how to set up a development environment for Milestone XProtect integrations.

## Documentation and support

Ask questions and find answers to common questions at the
[Milestone Developer Forum](<https://developer.milestonesys.com/>).

Browse overview and reference documentation at
[MIP SDK Documentation](<https://doc.developer.milestonesys.com>).

Get access to free eLearning at
[Learning | Milestone Systems](https://learn.milestonesys.com/).

Watch tutorials about how to set up and use Milestone products at
[Milestone Video Tutorials](https://www.youtube.com/user/Milestonesys).

## Contributions

We do not currently accept contributions through pull request.

In case you want to comment on or contribute to the samples, please reach out through
the [Milestone Developer Forum](<https://developer.milestonesys.com/>).

## License

MIP SDK sample code is is licensed under the [MIT license](<LICENSE.md>).
