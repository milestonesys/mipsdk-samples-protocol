// ************************** utils **************************

const CHAT_SAMPLE_TOPIC = "samples.chat";

const buildMessage = (text, sent) => {
    return `
        <div class="message ${sent? 'sent': 'received'}">
            ${text}
        </div>
    `;
}

const htmlToElement = (html) => {
    const template = document.createElement('template');
    template.innerHTML = html.trim();
    return template.content.firstChild;
}

const uuidv4 = () => {
    return ([1e7] + -1e3 + -4e3 + -8e3 + -1e11).replace(/[018]/g, c =>
        (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
    );
}

const buildAuthCommand = (token) => {
    return JSON.stringify({
        id: uuidv4(),
        type: 'auth.v1',
        body: {
            token: `Bearer ${token}`
        }
    })
}

const buildPulseCommand = (lastSeq) => {
    return JSON.stringify({
        id: uuidv4(),
        type: 'pulse.v1',
        body: {
            seq: lastSeq
        }
    })
}

const buildPublishCommand = (textMessage) => {
    return JSON.stringify({
        id: uuidv4(),
        type: 'pub.v1',
        body: {
            data: {  // custom data
                textMessage: textMessage
            },
            topic: CHAT_SAMPLE_TOPIC 
        }
    })
}

const buildSubscribeCommand = (topic) => {
    return JSON.stringify({
        id: uuidv4(),
        type: 'sub.v1',
        body: {
            topic: topic
        }
    })
}


const getToken = async (apigw, username, password) => {
    try {
        const r = await fetch(`${apigw}/api/IDP/connect/token`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: new URLSearchParams({
                grant_type: 'password',
                username: username,
                password: password,
                client_id: 'GrantValidatorClient'
            })
        })
        const { access_token } = await r.json();
        return access_token;
    } catch {
        return null;
    }
} 


// ************************** main **************************

const messagesDiv = document.getElementById("messages");
const messageInput = document.getElementById("messageInput");
const apiGwInput = document.getElementById("apigw");
const usernameInput = document.getElementById("username");
const passwordInput = document.getElementById("password");
const connectButton = document.getElementById("connect");
const loginPage = document.getElementById("loginPage");
const chatPage = document.getElementById("chatPage");

var websocket = null;
var lastSeq = -1;
var sentMessage = null;  // over-simplified filtering of own messages because we subscribe to the same topic we publish to

messageInput.addEventListener("keypress", (e) => {
    if (e.key === "Enter") {
        e.preventDefault();
        console.log("Sending:", e.target.value);
        messagesDiv.appendChild(
            htmlToElement
                (buildMessage(e.target.value, true)
                )
        )
        sentMessage = e.target.value;
        websocket.send(buildPublishCommand(e.target.value))
        e.target.value = "";
    }
});

connectButton.addEventListener("click", async () => {
    const username = usernameInput.value;
    const password = passwordInput.value;

    const apigw = apiGwInput.value;
    const host = new URL(apigw).hostname;
    const isHttps = apigw.startsWith('https');
    const httpScheme = isHttps ? 'https' : 'http';

    const token = await getToken(`${httpScheme}://${host}`, username, password);
    if (token == null) {
        connect.classList.add('error')
        return;
    } 
    console.log("Received token:", token)

    
    const wsScheme = isHttps ? 'wss' : 'ws';
    websocket = new WebSocket(`${wsScheme}://${host}/api/ws/messages/v1`);
    websocket.addEventListener("open", (event) => {
        websocket.send(buildAuthCommand(token));
    });
    websocket.addEventListener("message", (event) => {
        const command = JSON.parse(event.data);
        console.debug(command);

        const { type, body, id } = command;

        if (type == "hello.v1") {
            const { pulsePeriodSeconds } = body
            loginPage.style.display = 'none';
            chatPage.style.display = 'flex';

            websocket.send(buildSubscribeCommand(CHAT_SAMPLE_TOPIC))

            console.log(`Starting pulse every ${pulsePeriodSeconds} after receiving hello...`)
            window.setInterval(function () {
                console.log(`Sending pulse. Last message received is: ${lastSeq}`)
                websocket.send(buildPulseCommand(lastSeq))
            }, pulsePeriodSeconds * 1000);
        }

        if (type == "msg.v1") {
            const { data, seq, topic } = body;

            lastSeq = Math.max(seq, lastSeq);  // update last received sequence to send in the pulse

            if (topic === CHAT_SAMPLE_TOPIC) {
                const { textMessage } = data;  // custom data
                if (textMessage == sentMessage) {
                    sentMessage = null;
                    return;  // sent by me
                }
                messagesDiv.appendChild(
                    htmlToElement
                        (buildMessage(textMessage, false)
                        )
                )
            }
        }
    });
});

