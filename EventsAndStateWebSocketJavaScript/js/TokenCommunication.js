async function getToken(apigw, username, password) {
  try {
    const response = await fetch(`${apigw}/api/IDP/connect/token`, {
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
    if (response.ok) {
      const { access_token } = await response.json();
      return access_token;
    } else {
      console.error('Error logging in', response);
      return null;
    }
  }
  catch(error) {
    console.error('Error logging in', error);
    return null;
  }
} 
