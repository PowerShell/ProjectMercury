# Microsoft.PowerShell.Copilot

```
██████╗ ███████╗ ██████╗ ██████╗ ██████╗ ██╗██╗      ██████╗ ████████╗
██╔══██╗██╔════╝██╔════╝██╔═══██╗██╔══██╗██║██║     ██╔═══██╗╚══██╔══╝
██████╔╝███████╗██║     ██║   ██║██████╔╝██║██║     ██║   ██║   ██║
██╔═══╝ ╚════██║██║     ██║   ██║██╔═══╝ ██║██║     ██║   ██║   ██║
██║     ███████║╚██████╗╚██████╔╝██║     ██║███████╗╚██████╔╝   ██║
╚═╝     ╚══════╝ ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚══════╝ ╚═════╝    ╚═╝
```

This module enable an interactive chat mode as well as getting the last error and sending to GPT.

Your API Key must be stored in `$env:AZURE_OPENAI_API_KEY`.
If you want to use a custom model endpoint, you can store it in `$env:AZURE_OPENAI_ENDPOINT`.
A custom initial system prompt can be stored in `$env:AZURE_OPENAI_SYSTEM_PROMPT`.

Additional opportunities:

- integration with debugger to get assistance
- identifying and fixing issues in a script file, enhance fixes with existing pester tests
- generation of Pester tests
- malware, unsafe security issues detection

**Using the Tool:**

Guide for Signing Up For API Key
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
