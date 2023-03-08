//Security token have an expiry time, this function updates the token
async function refreshToken() {

    var refreshedToken = await getToken();
    if (token != null && token != refreshedToken) {
        token = refreshedToken;
    }

    var patchTokenData = {
        'token': token
    };

    await fetch(webRtcUrl + "/WebRTCSession/" + sessionId, {
        method: 'PATCH',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + token
        },
        body: JSON.stringify(patchTokenData)

    }).then(async function (response) {

        if (await checkResponseFromIdp(response)) {
            console.log('token updated successfully');
        }
        sessionData = await response.json();

    }).catch(function (error) {
        clearAnyRefreshTimers();
        var msg = "Failed to refresh token - " + error;
        console.log(msg);
        log(msg);
    });
}

async function getToken() {
    token = null;
    clearAnyRefreshTimers();

    var username = document.getElementById("username").value;
    var password = document.getElementById("password").value;
    var idpUrl = apiGatewayUrl + "/IDP/connect/token";

    var urlencoded = new URLSearchParams();
    urlencoded.append("grant_type", "password");
    urlencoded.append("username", username);
    urlencoded.append("password", password);
    urlencoded.append("client_id", "GrantValidatorClient");

    await fetch(idpUrl, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: urlencoded,
    }).then(async function (response) {
        await checkResponseFromIdp(response);
        const json = await response.json();
        token = json["access_token"];

        setupRefreshTokenTimer(json["expires_in"]);
    }).catch(function (error) {
        var msg = "Failed to retrieve token - " + error;
        console.log(msg);
        log(msg);
    });

    return token;
}

//The token should be refreshed before it expires, this methods handles that
function setupRefreshTokenTimer(expiresInSeconds) {
    var refreshTokenIn = (expiresInSeconds - 60) * 1000; //expires_in is in second. We need milliseconds
    refreshTimerId = setTimeout(refreshToken, refreshTokenIn);
}

//The timer does not stop when connection closes. This methods stops any timers
function clearAnyRefreshTimers() {
    if (refreshTimerId) {
        clearTimeout(refreshTimerId);
        refreshTimerId = null;
    }
}

async function checkResponseFromIdp(response) {
    if (!response.ok) {
        var errorInfo = await response.json();
        //errors from IDP will contain error description
        if (errorInfo.error_description) {
            errorInfo = errorInfo.error_description;
        }
        throw Error(errorInfo);
    }
    return true;
}