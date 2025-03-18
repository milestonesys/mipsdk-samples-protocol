// ************************** Websocket command handling **************************

const commandTimeout = 3000; // msec.
const timeoutResolution = 20; // mesc

let commandResponse = null; // response to commands
let commandId = 0; // running id of commands


async function sendCommand(websocket, command) {
  console.debug(command);
  websocket.send(JSON.stringify(command));
  
  for (let i = 0; i < (commandTimeout/timeoutResolution); i++) {
    // Check if global variable 'commandResponse' is set by event handler
    const result = commandResponse;
    if (result != null) {
      commandResponse = null;
    
      // Verify that the command id matches. This sample does not support receiving responses out of order.
      if (result.commandId != command.commandId) {
        throw new Error(`${command.command} failed. Receiving responses out of order is not supported.`);
      }
    
      if (result.status < 200 || result.status > 299) {
        throw new Error(`${command.command} failed. Status: ${result.status}. Error: ${result.error.errorText}`);
      }
      return result;
    }
    await sleep(timeoutResolution);
  }
  throw new Error(`${command.command} command timed out.`);
}


function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}


async function authenticate(websocket, token) {
  const command = {
    command: 'authenticate',
    commandId: commandId++,
    token: `Bearer ${token}`
  };

  await sendCommand(websocket, command);
}


async function startSession(websocket, sessionId, eventId) {
  const command = {
    command:'startSession',
    commandId: commandId++,
    sessionId: sessionId,
    eventId: eventId
  };

  const response = await sendCommand(websocket, command);
  return response.sessionId;
}


async function subscribeToEvents(websocket) {
  // Change the filter to match use-case
  const filter = {
    modifier: 'Include',
    resourceTypes: ['*'], // events from all source types
    sourceIds: ['*'],
    eventTypes: ['*']
  };

  const command = {
    command:'addSubscription',
    commandId: commandId++,
    subscriptionId: '',
    filters: [filter]        
  };

  const response = await sendCommand(websocket, command);
  return response.subscriptionId;
}



// ************************** Update UI **************************

function formatEvent(event) {
  var ts = new Date(event.time).toLocaleString();
  // Print source, type, and state group id (if present).
  // These ids can be resolved by calling the Configuration REST API
  if (event.stategroupid != null) {
    return `${ts}\n    Source: ${event.source}\n    Type: ${event.type}\n    StateGroupId: ${event.stategroupid}`;
  } else {
    return `${ts}\n    Source: ${event.source}\n    Type: ${event.type}`;
  }
}

function refreshConnectionStatus(status) {
  connectionStatusElement.innerText = status ? 'Connected': 'Disconnected';
}

function displayEvents(events) {
  events.reverse(); // display newest on top
  const formattedEvents = events.map((x) => formatEvent(x)); 
  textarea.value = formattedEvents.join('\n') + '\n' + textarea.value; // Display events on separate lines in the textarea
}
 

// ************************** Websocket connection handling **************************

// Create web socket connection and setup listeners
function connectToEventSource(webSocketUri, token, sessionId, eventId) {
  let currentSessionId = sessionId;
  let mostRecentEventId = eventId;

  websocket = new WebSocket(webSocketUri);

  websocket.onopen = async (event) => {
    try {
    await authenticate(websocket, token);
    console.info('Authenticated');

    currentSessionId = await startSession(websocket, sessionId, eventId);

    if (currentSessionId != sessionId) {
      // Either a new session was requested or failed to resume session (in which case events may have been lost)
      console.info('New session created');

      const subscriptionId = await subscribeToEvents(websocket);
      console.info('Subscription added');
    } else {
      console.info('Session resumed sucessfully');
    }

    // Switch from login to event list
    loginPage.style.display = "none";
    eventsPage.style.display = "block";
    
    refreshConnectionStatus(true);
  }
  catch(error) {
    console.error('Error configuring subscription:', error);
    websocket.close();
  }
};

  websocket.onmessage = (message) => {
    const messageData = JSON.parse(message.data);
    console.debug(messageData);

    const { commandId, events, status } = messageData;

    if (commandId != null) {
      commandResponse = messageData;
    }

    if (events != null) {
      mostRecentEventId = events.slice(-1)[0].id;
      displayEvents(events);
    }
  };

  websocket.onclose = (event) => {
    console.info('WebSocket connection closed', event);
    refreshConnectionStatus(false);
    setTimeout(() => connectToEventSource(webSocketUri, token, currentSessionId, mostRecentEventId), 2000);
  };

  websocket.onerror = (error) => {
    console.error('WebSocket error:', error);
    refreshConnectionStatus(false);
  };
}


// ************************** main **************************

const apiGwInput = document.getElementById("apigw");
const usernameInput = document.getElementById("username");
const passwordInput = document.getElementById("password");
const connectButton = document.getElementById("connect");
const loginPage = document.getElementById("loginPage");
const eventsPage = document.getElementById("eventsPage");
const textarea = document.getElementById("events");
const connectionStatusElement = document.getElementById("connectionStatus")

refreshConnectionStatus(false);

connectButton.addEventListener("click", async () => {
  const username = usernameInput.value;
  const password = passwordInput.value;

  const apigw = apiGwInput.value;
  const host = new URL(apigw).hostname;

  const isHttps = apigw.startsWith('https');
  const httpScheme = isHttps ? 'https' : 'http';
  const wsScheme = isHttps ? 'wss' : 'ws';
  const tokenProviderUri = `${httpScheme}://${host}`;
  const webSocketUri = `${wsScheme}://${host}/api/ws/events/v1`;
 
  const token = await getToken(tokenProviderUri, username, password);
  if (token == null) {
    connect.classList.add('error')
    return;
  } 
  console.info("Received token:", token)

  connectToEventSource(webSocketUri, token, '','');
});
