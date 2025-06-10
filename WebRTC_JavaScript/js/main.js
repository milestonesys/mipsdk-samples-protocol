'use strict';

let peerConnection, sessionId, dataChannel;
let apiGatewayUrl, webRtcUrl, deviceId, streamId, token, playbackTime, skipGaps, speed, stunUrl, turnUrl, turnUserName, turnCredential, includeAudio;
let candidates = [];
let iceServers = [];
let refreshTimerId;
let frameStartTime;
let reconnect = false;
let isPlayback = false;
let frameDate;
let aux1, aux2, aux3, aux4 = false;
let apiEndPoint;

// The audio clock rate of PCMU is 8000 Hz. If another audio codec is used, this rate needs to be adjusted accordingly.
let audioClockRate = 8000;


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

const onAnimationFrameReceived = () => {
    let receiver = peerConnection.getReceivers()[0];
    if (receiver) {
        let kind = receiver.track.kind;
        if (kind == "audio")
        {
            let synchronizationData = receiver.getSynchronizationSources()[0];
            if (synchronizationData) {
                // Synchronization timestamp from the audio receiver contains the sample rate and not milliseconds; therefore, we have to convert the sample rate to milliseconds.
                frameDate = new Date(frameStartTime + (synchronizationData.rtpTimestamp / (audioClockRate / 1000)));
                document.getElementById("frameTimeLabel").innerHTML = formatDate(frameDate);
            }
        }
        if (kind == "video") {
            let synchronizationData = receiver.getSynchronizationSources()[0];
            if (synchronizationData) {
                frameDate = new Date(frameStartTime + (synchronizationData.rtpTimestamp));
                document.getElementById("frameTimeLabel").innerHTML = formatDate(frameDate);
            }
        }
        
    }
    requestAnimationFrame(onAnimationFrameReceived);
}


async function commonSetup() {
    let startTime = Date.now();
    frameStartTime = Date.now();
    if (peerConnection != null) await closePeerConnection();

    apiGatewayUrl = document.getElementById("apiGatewayUrl").value;
    if (apiGatewayUrl.slice(-1) == '/')
        apiGatewayUrl = apiGatewayUrl.slice(0, -1);
    webRtcUrl = apiGatewayUrl + apiEndPoint;
    deviceId = document.getElementById("deviceId").value;
    streamId = document.getElementById("streamId").value;
    includeAudio = document.getElementById("includeAudio").checked;
    playbackTime = document.getElementById("playbackTime").value;
    skipGaps = document.getElementById("skipGaps").checked;
    speed = document.getElementById("speed").value;
    stunUrl = document.getElementById("stunUrl").value;
    turnUrl = document.getElementById("turnUrl").value;
    turnUserName = document.getElementById("turnUserName").value;
    turnCredential = document.getElementById("turnCredential").value;

    //Audio stream is disabled when playback speed is not 1.0(default value when not explicitly set)
    includeAudio = includeAudio && (!speed || parseFloat(speed) === 1);
		
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
    videoObject = document.querySelector('#videoCtl');

    requestAnimationFrame(onAnimationFrameReceived);

    // Diagnostics
    peerConnection.onconnectionstatechange = () => {
        log("Connection state", peerConnection.connectionState);
        if (peerConnection.connectionState == "failed") {
            reconnect = true;
            tryReConnect(0);
        }
        if (peerConnection.connectionState == "connected") {
            let endTime = Date.now();
            log(`Establishing connection time: ${endTime - startTime} ms`);
        }
    }
    peerConnection.oniceconnectionstatechange = () => log("ICE connection state", peerConnection.iceConnectionState);
    peerConnection.onicegatheringstatechange = () => log("ICE gathering state", peerConnection.iceGatheringState);
    peerConnection.onsignalingstatechange = () => log("Signaling state", peerConnection.signalingState);
}

const onFrameReceived = (now, metadata) => {
    frameDate = new Date(frameStartTime + metadata.rtpTimestamp);
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

async function tabSwitch(caller, tabName) { 
    var i;
    var x = document.getElementsByClassName("tabs");
    var y = document.getElementsByClassName("bar-item");
    for(i = 0; i< x.length; i++) {
        x[i].style.display = "none";
        y[i].id = "";
        
    }
    caller.id = "selected";
    document.getElementById(tabName).style.display = "block";
}

async function command(command) {
    var ptzCommand = { 
        ApiVersion : "1.0",
        type: "request",
        method: "ptzMove",
        params: { direction: command }
    };
    dataChannel.send(JSON.stringify(ptzCommand));
}

async function aux(auxNumb) {
    var state;
    switch(auxNumb) {
        case "1":
            state = !aux1;
            aux1 = state;
            break;
        case "2":
            state = !aux2;
            aux2 = state;
            break;
        case "3":
            state = !aux3;
            aux3 = state;
            break;
        case "4":
            state = !aux4;
            aux4 = state;
            break;
    }
    var auxCommand = { 
        ApiVersion : "1.0",
        type: "request",
        method: "setAux",
        params: { 
            on: state,
            auxNumber: auxNumb
        }
    };
    dataChannel.send(JSON.stringify(auxCommand));
}

function clearDiagnosticsLog() {
    document.getElementById('diagnostics').innerHTML = '';
}
