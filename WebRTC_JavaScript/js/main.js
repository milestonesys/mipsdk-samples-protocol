'use strict';

const STUN_URL = "stun:stun1.l.google.com:19302";

var pc, sessionId;
var serverUrl, cameraId, token;
var candidates = [];

async function start() {
    if (pc != null) await pc.close();

    serverUrl = document.getElementById("serverUrl").value;
    cameraId = document.getElementById("cameraId").value;
    token = document.getElementById("token").value;
    
    pc = new RTCPeerConnection({ iceServers: [{ urls: STUN_URL }] });

    pc.ontrack = evt => document.querySelector('#videoCtl').srcObject = evt.streams[0];
    pc.onicecandidate = evt => evt.candidate && sendIceCandidate(JSON.stringify(evt.candidate));
    pc.onconnectionstatechange = () => {
        if (pc.connectionState === 'connected') {
            console.log('Peers connected')
        }
        else {
            console.log('Connection state changed: ' + pc.connectionState)
        }
    }

    // Diagnostics
    pc.onicegatheringstatechange = () => console.log("onicegatheringstatechange: " + pc.iceGatheringState);
    pc.oniceconnectionstatechange = () => console.log("oniceconnectionstatechange: " + pc.iceConnectionState);
    pc.onsignalingstatechange = () => console.log("onsignalingstatechange: " + pc.signalingState);
		
    initiateWebRTCSession();
}

async function closePeer() {
    await pc.close();
};

async function initiateWebRTCSession() {
    try {
        // Initiate WebRTC session on the server
        const body = { cameraId: cameraId, resolution: "notInUse" };
        const response = await fetch(serverUrl + "/WebRTC/v1/WebRTCSession", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + token
            },
            body: JSON.stringify(body),
        });
        const json = await response.json();
        sessionId = json["sessionId"];

        // Update answerSDP value on the server
        await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(json["offerSDP"])));
        console.log("remote sdp:\n" + pc.remoteDescription.sdp);
        pc.createAnswer()
            .then((answer) => pc.setLocalDescription(answer))
            .then(() => updateAnswerSDP(json, JSON.stringify(pc.localDescription)));
        console.log("local description:\n" + pc.setLocalDescription);

        // Add server Ice candidates
        addServerIceCandidates();

        console.log('InitiateWebRTCSession end');
        return;
    }
    catch (error) {
        console.log(error);
        return error;
    }
}

async function updateAnswerSDP(data, localDescription) {
    try {
        data["answerSDP"] = localDescription;
        const response = await fetch(serverUrl + "/WebRTC/v1/WebRTCSession", {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + token
            },
            body: JSON.stringify(data)
        });

        if (await response.ok) {
            console.log('AnswerSDP updated sucessfully');
        }

        const json = response.json();
        console.log('Updated WebRTC session object: ' + json);
        return json;
    }
    catch (error) {
        console.log(error);
        return error;
    }
}

async function addServerIceCandidates() {
    try {
        const response = await fetch(serverUrl + "/WebRTC/v1/IceCandidates/" + sessionId, {
            method: 'GET',
            headers: {
                'Authorization': 'Bearer ' + token
            }
        });

        const json = await response.json();
        for (const element of json["candidates"]) {
            if (!candidates.includes(element)) {
                console.log("Ice candidate data: " + element);
                candidates.push(element);
                var obj = JSON.parse(element);
                await pc.addIceCandidate(obj);
            }
        }

        if (pc.iceConnectionState == "new" ||
            pc.iceConnectionState == "checking") {
            setTimeout(addServerIceCandidates, 20);
        }
        return;
    }
    catch (error) {
        console.log(error);
        return error;
    }
}

async function sendIceCandidate(candidate) {
    try {
        const body = { sessionId: sessionId, candidates: [candidate] };
        const response = await fetch(serverUrl + "/WebRTC/v1/IceCandidates", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + token
            },
            body: JSON.stringify(body)
        });

        if (await response.ok) {
            console.log('Client candidates sent successfully');
        }

        return;
    }
    catch (error) {
        console.log(error);
        return error;
    }
}

async function login() {
    if(pc != null) await pc.close();

    try {
        var idpserver = document.getElementById("idpserver").value;
        var username = document.getElementById("username").value;
        var password = document.getElementById("password").value;

        var lastchar = idpserver.substr(idpserver.length -1);
        if(lastchar != "/") {
            idpserver += "/";
        }
        var final = idpserver + "IDP/connect/token";

        var urlencoded = new URLSearchParams();
            urlencoded.append("grant_type", "password");
            urlencoded.append("username", username);
            urlencoded.append("password", password);
            urlencoded.append("client_id", "GrantValidatorClient");

        const response = await fetch(final, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: urlencoded,
        });
        const json = await response.json();
        token = json["access_token"];
        document.getElementById("token").value = token;
        return;
    } catch (error) {
        console.log(error);
        return error;
    }
}