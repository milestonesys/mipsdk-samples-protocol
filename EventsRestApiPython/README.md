---
description: This sample demonstrates how to access the RESTful Events API through the API Gateway from a Python-based application.
keywords:
- Protocol integration
- RESTful Events API
- API Gateway
- Identity Provider
- Events
lang: en-US
title: Events REST API - Python
---

# RESTful Events API - Python [BETA]

This sample shows how to trigger and retrieve events from a Python application.

The sample logs into the server, retrieves any existing user-defined event, triggers it, retrieves the event by id and retrieves all stored events with their metadata.

The RESTful Events API is in beta version. I.e., both the API and this sample might change without preserving backwards compatibility as long as it is in beta.

## Prerequisites

- XProtect 2023 R2 or later.
- The API Gateway installed on the same host as the management server.
- A user with the Administrators role.
- An existing user defined event with event type retention policy greater than 0 days.
- Python version 3.7 or newer.
- The Python packages 'requests' and 'requests-ntlm'. To install the package:
  - In a command prompt, enter `pip install <package-name>`.
  - In Visual Studio Solution Explorer, select a Python environment under Python Environments, then from the context menu select Manage Python Packages and search for *\<package-name>*.

The sample is verified with the following versions of Python packages:

- requests: 2.26.0
- requests-ntlm: 1.2.0
- urllib3: 1.26.16

Using different package versions might result in unexpected errors when running the sample.

## The sample demonstrates

- How to login using OpenID Connect/OAuth2 from a Python application
- How to retrieve event types
- How to trigger an event
- How to retrieve an event by id
- How to retrieve all stored events with their metadata

## Using

- RESTful Events API

## Related samples

- mipsdk-samples-protocol/RestfulCommunicationPython
- mipsdk-samples-protocol/AlarmsRestApiPython
- mipsdk-samples-protocol/EventsRestApiPython

## Environment

- None

## Visual Studio Python project

- [EventsRestApiPython.pyproj](javascript:clone('https://github.com/milestonesys/mipsdk-samples-protocol','src/ProtocolSamples.sln');)