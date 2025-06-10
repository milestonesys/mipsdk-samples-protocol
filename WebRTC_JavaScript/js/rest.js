// Timeout in milliseconds for polling API Gateway
const pollingTimeout = 20;
let videoObject;
let addServerCandidatesErrorCounter = 0;

async function start() {
    apiEndPoint = "/REST/v1/WebRTC";
    await commonSetup();

    peerConnection.onicecandidate = evt => evt.candidate && sendIceCandidate(JSON.stringify(evt.candidate));
		
    initiateWebRTCSession();
}

async function initiateWebRTCSession() {
    try {
        let body = { deviceId: deviceId, resolution: "notInUse" };

        if (streamId)
            body.streamId = streamId;

        // includeAudio is optional parameter and if not set in the body, it will default to true
        body.includeAudio = includeAudio;

        if (playbackTime && !reconnect || !frameDate) {
            let playbackTimeNode = { playbackTime: playbackTime };
            if (speed)
                playbackTimeNode.speed = speed;
            playbackTimeNode.skipGaps = skipGaps;
            body.playbackTimeNode = playbackTimeNode;
        }
        else if(playbackTime && reconnect) {
            let playbackTimeNode = { playbackTime: frameDate };
            if(speed)
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
                .then(() => updateAnswerSDP(JSON.stringify(peerConnection.localDescription)))
                .then(() => {
                    dataChannel = peerConnection.createDataChannel("commands", { protocol: "videoos-commands" });
                    console.log(`Data channel opened with protocol ${dataChannel.protocol}`);
                });

            // Add server ICE candidates
            addServerCandidatesErrorCounter = 0;
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

            //success, reset error counter
            addServerCandidatesErrorCounter = 0;

        }).catch(function (error) {
            let msg = "Failed to retrieve ICE candidate from server - " + error;
            console.log(msg);
            log(msg);
            addServerCandidatesErrorCounter++;
        });

        if(addServerCandidatesErrorCounter < 3) {
            setTimeout(addServerIceCandidates, pollingTimeout);
        }
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

async function tryReConnect(counter) {
    try{
    setTimeout(() => {
        initiateWebRTCSession();
    }, 10000);
    }
    catch(error) {
        if(counter > 4) {
             tryReConnect(counter + 1);
        }
        else {
            closePeerConnection();
        }
    }
    reconnect = false;
}