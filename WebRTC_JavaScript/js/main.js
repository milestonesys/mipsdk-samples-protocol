'use strict';

const STUN_URL = "stun:stun1.l.google.com:19302";

var pc, sessionId;
var apiGatewayUrl, webRtcUrl, cameraId, token;
var candidates = [];
var refreshTimerId;

var startTime, endTime;
// Timeout in milliseconds for polling API Gateway
const pollingTimeout = 20;


async function start() {
    startTime = Date.now();

    if (pc != null) await pc.close();

    apiGatewayUrl = document.getElementById("apiGatewayUrl").value;
    if (apiGatewayUrl.slice(-1) == '/')
        apiGatewayUrl = apiGatewayUrl.slice(0, -1);
    webRtcUrl = apiGatewayUrl + "/REST/v1/WebRTC";
    cameraId = document.getElementById("cameraId").value;

    await login();

    pc = new RTCPeerConnection({ iceServers: [{ urls: STUN_URL }] });

    pc.ontrack = evt => document.querySelector('#videoCtl').srcObject = evt.streams[0];
    pc.onicecandidate = evt => evt.candidate && sendIceCandidate(JSON.stringify(evt.candidate));

    // Diagnostics
    pc.onconnectionstatechange = () => {
        log("Connection state", pc.connectionState);
        if (pc.connectionState === "failed") {
            closePeer();
        }

        if (pc.connectionState === "connected") {
            endTime = Date.now();
            log(`Establishing connection time: ${endTime - startTime} ms`);
        }
    }
    pc.oniceconnectionstatechange = () => log("ICE connection state", pc.iceConnectionState);
    pc.onicegatheringstatechange = () => log("ICE gathering state", pc.iceGatheringState);
    pc.onsignalingstatechange = () => log("Signaling state", pc.signalingState);
		
    initiateWebRTCSession();
}

async function closePeer() {
    await pc.close();
    document.querySelector('#videoCtl').srcObject = null;
    clearAnyRefreshTimers();
};

function log() {
    let diagnostics = document.getElementById('diagnostics');
    let argumentsArray = Array.prototype.slice.call(arguments);
    let logMessage = argumentsArray.join(': ');
    diagnostics.innerHTML += logMessage + '<br>';
}

async function initiateWebRTCSession() {
    try {
        // Initiate WebRTC session on the server
        const body = { cameraId: cameraId, resolution: "notInUse" };
        await fetch(webRtcUrl + "/Session", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + token
            },
            body: JSON.stringify(body),
            
        }).then(async function (response)
        {
            await checkResponse(response);

            var sessionData = await response.json();
            sessionId = sessionData["sessionId"];

            // Update answerSDP value on the server
            await pc.setRemoteDescription(JSON.parse(sessionData["offerSDP"]));
            console.log("remote sdp:\n" + pc.remoteDescription.sdp);
            pc.createAnswer()
                .then((answer) => pc.setLocalDescription(answer))
                .then(() => console.log("local sdp:\n" + pc.localDescription.sdp))
                .then(() => updateAnswerSDP(JSON.stringify(pc.localDescription)));

            // Add server ICE candidates
            addServerIceCandidates();

            console.log('InitiateWebRTCSession end');
            return;
        }).catch(function (error) {
            var msg = "Failed to initiate WebRTC session - " + error;
            console.log(msg);
            log(msg);
        });
    }
    catch (error) {
        console.log(error);
        return error;
    }
}

async function updateAnswerSDP(localDescription) {
    var patchAnswerSDPData = {
        'answerSDP': localDescription
    };

    await fetch(webRtcUrl + "/Session/" + sessionId, {
        method: 'PATCH',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + token
        },
        body: JSON.stringify(patchAnswerSDPData)
    }).then(async function (response) {
        await checkResponse(response);

        if (await response.ok) {
            console.log('AnswerSDP updated successfully');
        }

        const json = response.json();
        console.log('Updated WebRTC session object: ' + json);
        return json;
    }).catch(function(error){
        var msg = "Failed to update session with answerSDP - " + error;
        console.log(msg);
        log(msg);
    });
}

// Polling API Gateway to get remote ICE candidates
async function addServerIceCandidates() {
    if (pc.iceConnectionState == "new" ||
        pc.iceConnectionState == "checking") {

        await fetch(webRtcUrl + "/IceCandidates/" + sessionId, {
            method: 'GET',
            headers: {
                'Authorization': 'Bearer ' + token
            }
        }).then(async function (response) {
            await checkResponse(response);

            const json = await response.json();
            for (const element of json["candidates"]) {
                if (!candidates.includes(element)) {
                    console.log("ICE candidate data: " + element);
                    candidates.push(element);
                    var obj = JSON.parse(element);
                    await pc.addIceCandidate(obj);
                }
            }

        }).catch(function (error) {
            var msg = "Failed to retrieve ICE candidate from server - " + error;
            console.log(msg);
            log(msg);
        });
    }
}

async function sendIceCandidate(candidate) {
    const body = { candidates: [candidate] };

    await fetch(webRtcUrl + "/IceCandidates/" + sessionId, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + token
        },
        body: JSON.stringify(body)
    }).then(async function (response) {

        await checkResponse(response);

        console.log('Client candidates sent successfully');
    }).catch (function (error) {
        var msg = "Failed to send ICE candidate - " + error;
        console.log(msg);
        log(msg);
    });
}

async function checkResponse(response) {
    if (!response.ok) {
        var errorInfo = await response.json();
        throw Error(errorInfo);
    }
    return true;
}

async function login() {
    if(pc != null) await pc.close();

    try {
        token = await getToken();
        return;
    } catch (error) {
        console.log(error);
        return error;
    }
}


