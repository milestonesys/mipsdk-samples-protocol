---
description: This sample demonstrates how to access the RESTful Alarms API through the API Gateway from a Python-based application.
keywords:
- Protocol integration
- RESTful Alarms API
- API Gateway
- Identity Provider
- Alarms
lang: en-US
title: Alarms REST API - Python
---

# RESTful Alarms API - Python [BETA]

This sample shows how to trigger and retrieve alarms from a Python application.

The sample gets an OAuth token, triggers an alarm, retrieves the alarm by id and retrieves all stored alarms with their metadata.

The RESTful Alarms API is in beta version. I.e., both the API and this sample might change without preserving backwards compatibility as long as it is in beta.

## Prerequisites

- XProtect 2023 R2 or later.
- The API Gateway installed on the same host as the management server.
- A user with the Administrators role.
- An existing camera.
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
- How to trigger an alarm
- How to retrieve an alarm by id
- How to retrieve all stored alarms with their metadata
- How to update an alarm state

## Using

- RESTful Alarms API

## Related samples

- mipsdk-samples-protocol/RestfulCommunicationPython
- mipsdk-samples-protocol/AlarmsRestApiPython
- mipsdk-samples-protocol/EventsRestApiPython

## Environment

- None

## Visual Studio Python project

- [AlarmsRestApiPython.pyproj](javascript:clone('https://github.com/milestonesys/mipsdk-samples-protocol','src/ProtocolSamples.sln');)