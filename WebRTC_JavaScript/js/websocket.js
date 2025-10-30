let socket;
let registerId, connectId, inviteId = -1;
let receivedIceCandidates = [];

async function startWebsocket() {
    apiEndPoint = "/ws/webrtc/v1";
    await commonSetup();

    peerConnection.onicecandidate = evt => evt.candidate && trickle(evt.candidate);

    peerConnection.onicegatheringstatechange = () => {
        if (peerConnection.iceGatheringState == "gathering") {
            addReceivedIceCandidates();
        }
    };

    await openWebsocket();
}

async function openWebsocket() {
    socket = new WebSocket(webRtcUrl, "webrtc.onvif.org");
    socket.addEventListener("open", (event) => {
        registerClient();
    });
    socket.addEventListener("message", (event) => {
        var parsed = JSON.parse(event.data);
        readMessage(parsed);
    });
}

async function registerClient() {
    registerId = 1;
    socket.send(JSON.stringify(createJson("register", {authorization: token}, registerId)));
}

async function connectToDevice() {
    connectId = 2;
    var params = {
        authorization: token,
        peer: deviceId
    };
    if(streamId !== "") {
        params.streamID = streamId;
    }
    if(playbackTime !== "") {
        params.PlaybackTime = playbackTime;
        params.skipGaps = skipGaps;
    }
    if(speed !== "") {
        params.PlaybackSpeed = speed;
    }
    
    if(iceServers.length > 0) {
        params.iceServers = iceServers;
    }

    // includeAudio is optional parameter and if not set, it will default to true
    params.includeAudio = includeAudio;
    
    socket.send(JSON.stringify(createJson("connect", params, connectId)));
}

async function answerSDP(sdp, id) {
    var answer = {
        jsonrpc: "2.0",
        result: {
            answer: sdp
        },
        id: id
    };
    socket.send(JSON.stringify(answer));
}

function createJson(methodName, data, id) {
    var json = {
        jsonrpc: "2.0",
        method: methodName,
        params: data
    }
    if(id !== null) {
        json.id = id;
    }
    return json;
}

function readMessage(data) {
    if(data.hasOwnProperty("id")) {
        if(data.id == registerId) {
            connectToDevice();
        }
        else if (data.id == connectId) {
            //all parameters in the response is optional. If needed, we can do logic here, which uses some specific implementation. But for generic implementation, no parameters from connect should be used.            
        }
        else {
            if (data.method == "invite") {
                sessionId = data.params.session;
                var offer = {
                    "type": "offer",
                    "sdp": data.params.offer
                };
                peerConnection.setRemoteDescription(offer);

                peerConnection.createAnswer()
                    .then((answer) => peerConnection.setLocalDescription(answer))
                    .then(() => console.log("local sdp:\n" + peerConnection.localDescription.sdp))
                    .then(() => answerSDP(peerConnection.localDescription.sdp, data.id))
                    .then(() => {
                        dataChannel = peerConnection.createDataChannel("commands", { protocol: "videoos-commands" });
                        console.log(`Data channel opened with protocol ${dataChannel.protocol}`);
                    });
            }
        }
    }
    else {
        if (data.method === "trickle") {
            if (peerConnection.iceGatheringState == "gathering") {
                peerConnection.addIceCandidate(data.params.candidate);
            } else {
                receivedIceCandidates.push(data.params.candidate);
            }
        }
        else {
            log(`Server error: ${data.params.message}`)
        }
    }
}

function addReceivedIceCandidates() {
    if (receivedIceCandidates.length > 0) {
        receivedIceCandidates.forEach(candidate => {
            peerConnection.addIceCandidate(candidate);
        });
        receivedIceCandidates = [];
    }
}

async function tryReConnect(counter) {
    try{
    setTimeout(() => {
        openWebsocket();
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

async function trickle(candidate) {
    var obj = {
        session: sessionId,
        candidate: candidate
    }
    socket.send(JSON.stringify(createJson("trickle", obj)));
}