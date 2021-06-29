---
description: This sample shows how a minimal part of the
  ServerCommmandService SOAP proxy can be implemented for performing a
  logon to an XProtect system.
keywords: Protocol integration
lang: en-US
title: Login .NET SOAP
---

# Login .NET SOAP

This sample shows how a minimal part of the ServerCommmandService SOAP
proxy can be implemented for performing a logon to an XProtect system.

After the login is successful, it will connect to one camera and display
the result on the console.

Please note that the sample is using a hardcoded GUID to identify a
camera, as well as the recording server port.

## MIP Environment - Protocol

![](LoginDotNetSoap1.png)

The readable part of the protocol communication is displayed in the
console window.

## The sample demonstrates

-   Use of ServerCommandService for performing login
-   Formatting of XML on the ImageServer protocol.

## Using

-   ServerCommandService proxy
-   ImageServer protocol

## Environment

-   None

## Visual Studio C\# project

-   [LoginDotNetSoap.csproj](javascript:openLink('..\\\\ProtocolSamples\\\\LoginDotNetSoap\\\\LoginDotNetSoap.csproj');)
