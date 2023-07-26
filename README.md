# Microsoft.PowerShell.Copilot
```
  ____    _              _   _      ____                   _   _           _
 / ___|  | |__     ___  | | | |    / ___|   ___    _ __   (_) | |   ___   | |_
 \___ \  | '_ \   / _ \ | | | |   | |      / _ \  | '_ \  | | | |  / _ \  | __|
  ___) | | | | | |  __/ | | | |   | |___  | (_) | | |_) | | | | | | (_) | | |_
 |____/  |_| |_|  \___| |_| |_|    \____|  \___/  | .__/  |_| |_|  \___/   \__|
                                                  |_|
```

This module includes AI model management with the ability to enable an interactive chat mode as well as getting the last error and sending to GPT. 

| Commands:  | Description:                                              |
| ---------- | --------------------------------------------------------  |
| register   | Create a new model for use                                |
| set        | Set the registration information of a model               |
| unregister | Unregister the specified model                            |
| use        | Use the specified model                                   |
| get        | Get the registration information of a model               |
| list       | List the registration information of all registered models|
| export     | Export the registration information of a model            |
| import     | Import the registration information of a model            |
After building, run 'ai' for help page.

**Using the Default Microsoft Model:**

The default Microsoft Model has already been registered in the system. 
If you would like to gain access to the model, follow the instructions below to gain access to an API Key:
1.  Navigate to <https://pscopilot.developer.azure-api.net>
2.  Click `Sign Up` located on the top right corner of the page.
3.  Sign up for a subscription by filling in the fields (email, password,
    first name, last name).
4.  Verify the account (An email should have been sent from
    <apimgmt-noreply@mail.windowsazure.com> to your email)
5.  Click `Sign In` located on the top right corner of the page.
6.  Enter the email and password used when signing up.
7.  Click `Products` located on the top right corner of the page
8.  In the field stating `Your new product subscription name`, Enter
    `Azure OpenAI Service API`.
9.  Click `Subscribe` to be subscribed to the product.

In order to view your subscription/API key,
1.  Click `Profile` located on the top right corner of the page.
2.  Your Key should be located under the `Subscriptions`
    section. 
    Click on `Show` to view the primary or secondary key.

**Registering A New Model:**
If you would like to register a model into the system, please enter the register command with the following fields:
| Options:                        | Description:                                              |
| ----------------                | --------------------------------------------------------  |
| -n, --name                      | Name of the model                                         |
| -d, --description `(optional)`  | Description of the model                                  |
| -e, --endpoint                  | Endpoint URL to use for this model                        |
| -k, --key `(optional)`          | The API key for the model                                 |
| -m, --deployment                | The deployment id (ex.)                                   |
| -o, --openai-model `(optional)` | The name of the OpenAI model used by the deployment       |
| -p, --system-prompt             | The system prompt for the model                           |
| --trust `(optional)`            | The trust level of the model (public/private)             |
