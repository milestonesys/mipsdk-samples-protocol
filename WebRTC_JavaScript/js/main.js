'use strict';

let peerConnection, sessionId;
let apiGatewayUrl, webRtcUrl, cameraId, streamId, token, playbackTime, skipGaps, speed, stunUrl, turnUrl, turnUserName, turnCredential;
let candidates = [];
let iceServers = [];
let refreshTimerId;
let frameStartTime;

// Timeout in milliseconds for polling API Gateway
const pollingTimeout = 20;
let videoObject;

async function start() {
    let startTime = Date.now();
    frameStartTime = Date.now();

    if (peerConnection != null) await closePeerConnection();

    apiGatewayUrl = document.getElementById("apiGatewayUrl").value;
    if (apiGatewayUrl.slice(-1) == '/')
        apiGatewayUrl = apiGatewayUrl.slice(0, -1);
    webRtcUrl = apiGatewayUrl + "/REST/v1/WebRTC";
    cameraId = document.getElementById("cameraId").value;
    streamId = document.getElementById("streamId").value;
    playbackTime = document.getElementById("playbackTime").value;
    skipGaps = document.getElementById("skipGaps").checked;
    speed = document.getElementById("speed").value;
    stunUrl = document.getElementById("stunUrl").value;
    turnUrl = document.getElementById("turnUrl").value;
    turnUserName = document.getElementById("turnUserName").value;
    turnCredential = document.getElementById("turnCredential").value;

    if (stunUrl) {
        iceServers.push({ urls: stunUrl });
    }
    if (turnUrl) {
        iceServers.push({ urls: turnUrl, username: turnUserName, credential: turnCredential });
    }
    if (playbackTime) {
        
        frameStartTime = Date.parse(playbackTime);
    }
    await login();

    peerConnection = new RTCPeerConnection({ iceServers: iceServers });

    peerConnection.ontrack = evt => document.querySelector('#videoCtl').srcObject = evt.streams[0];
    peerConnection.onicecandidate = evt => evt.candidate && sendIceCandidate(JSON.stringify(evt.candidate));

    videoObject = document.querySelector('#videoCtl');

    if (navigator.userAgent.search("Firefox")) {
        requestAnimationFrame(onAnimationFrameReceived);
    } else {
        videoObject.requestVideoFrameCallback?.(onFrameReceived);
    }

    // Diagnostics
    peerConnection.onconnectionstatechange = () => {
        log("Connection state", peerConnection.connectionState);
        if (peerConnection.connectionState == "failed") {
            closePeerConnection();
        }

        if (peerConnection.connectionState == "connected") {
            let endTime = Date.now();
            log(`Establishing connection time: ${endTime - startTime} ms`);
        }
    }
    peerConnection.oniceconnectionstatechange = () => log("ICE connection state", peerConnection.iceConnectionState);
    peerConnection.onicegatheringstatechange = () => log("ICE gathering state", peerConnection.iceGatheringState);
    peerConnection.onsignalingstatechange = () => log("Signaling state", peerConnection.signalingState);
		
    initiateWebRTCSession();
}


const onAnimationFrameReceived = () => {
    let receiver = peerConnection.getReceivers()[0];
    if (receiver) {
        let synchronizationData = receiver.getSynchronizationSources()[0];
        if (synchronizationData) {
            let frameDate = new Date(frameStartTime + synchronizationData.rtpTimestamp);
            document.getElementById("frameTimeLabel").innerHTML = formatDate(frameDate);
        }
    }

    requestAnimationFrame(onAnimationFrameReceived);
}


const onFrameReceived = (now, metadata) => {
    let frameDate = new Date(frameStartTime + metadata.rtpTimestamp);
    document.getElementById("frameTimeLabel").innerHTML = formatDate(frameDate);

    // Re-register the callback to be notified about the next frame.
    videoObject.requestVideoFrameCallback(onFrameReceived);
};

function formatDate(date) {
    let month = prependZero(date.getUTCMonth() + 1);
    let day = prependZero(date.getUTCDate());
    let hours = prependZero(date.getUTCHours());
    let minutes = prependZero(date.getUTCMinutes());
    let seconds = prependZero(date.getUTCSeconds());

    return month + "/" + day + " " + hours + ":" + minutes + ":" + seconds;
}

function prependZero(number) {
    if (number < 10)
        return "0" + number;
    else
        return number;
}

async function closePeerConnection() {
    await peerConnection.close();
    document.querySelector('#videoCtl').srcObject = null;
    candidates.length = 0; iceServers.length = 0;
    document.getElementById("frameTimeLabel").innerHTML = "";
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
        let body = { cameraId: cameraId, resolution: "notInUse" };
        if (streamId)
            body.streamId = streamId;
        if (playbackTime) {
            let playbackTimeNode = { playbackTime: playbackTime };
            if (speed)
                playbackTimeNode.speed = speed;
            playbackTimeNode.skipGaps = skipGaps;
            body.playbackTimeNode = playbackTimeNode;
        }
        // pass any configured STUN or TURN servers on to the VMS
        body.iceServers = [];
        if (stunUrl) {
            body.iceServers.push({ url: stunUrl });
        }
        if (turnUrl) {
            body.iceServers.push({ url: turnUrl, username: turnUserName, credential: turnCredential });
        }

        // Initiate WebRTC session on the server        
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

            let sessionData = await response.json();
            sessionId = sessionData["sessionId"];

            // Update answerSDP value on the server
            await peerConnection.setRemoteDescription(JSON.parse(sessionData["offerSDP"]));
            console.log("remote sdp:\n" + peerConnection.remoteDescription.sdp);

            peerConnection.createAnswer()
                .then((answer) => peerConnection.setLocalDescription(answer))
                .then(() => console.log("local sdp:\n" + peerConnection.localDescription.sdp))
                .then(() => updateAnswerSDP(JSON.stringify(peerConnection.localDescription)));

            // Add server ICE candidates
            addServerIceCandidates();

            console.log('InitiateWebRTCSession end');
            return;
        }).catch(function (error) {
            let msg = "Failed to initiate WebRTC session - " + error;
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
    let patchAnswerSDPData = {
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
        let msg = "Failed to update session with answerSDP - " + error;
        console.log(msg);
        log(msg);
    });
}

// Polling API Gateway to get remote ICE candidates
async function addServerIceCandidates() {
    if (peerConnection.iceConnectionState == "new" ||
        peerConnection.iceConnectionState == "checking") {

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
                    let obj = JSON.parse(element);
                    await peerConnection.addIceCandidate(obj);
                }
            }

        }).catch(function (error) {
            let msg = "Failed to retrieve ICE candidate from server - " + error;
            console.log(msg);
            log(msg);
        });

        setTimeout(addServerIceCandidates, pollingTimeout);
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
        let msg = "Failed to send ICE candidate - " + error;
        console.log(msg);
        log(msg);
    });
}

async function checkResponse(response) {
    if (!response.ok) {
        let errorInfo = await response.json();
        throw Error(errorInfo);
    }
    return true;
}

async function login() {
    if(peerConnection != null) await peerConnection.close();

    try {
        token = await getToken();
        return;
    } catch (error) {
        console.log(error);
        return error;
    }
}


