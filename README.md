# ShellCopilot

```
███████╗██╗  ██╗███████╗██╗     ██╗      ██████╗ ██████╗ ██████╗ ██╗██╗      ██████╗ ████████╗
██╔════╝██║  ██║██╔════╝██║     ██║     ██╔════╝██╔═══██╗██╔══██╗██║██║     ██╔═══██╗╚══██╔══╝
███████╗███████║█████╗  ██║     ██║     ██║     ██║   ██║██████╔╝██║██║     ██║   ██║   ██║   
╚════██║██╔══██║██╔══╝  ██║     ██║     ██║     ██║   ██║██╔═══╝ ██║██║     ██║   ██║   ██║   
███████║██║  ██║███████╗███████╗███████╗╚██████╗╚██████╔╝██║     ██║███████╗╚██████╔╝   ██║   
╚══════╝╚═╝  ╚═╝╚══════╝╚══════╝╚══════╝ ╚═════╝ ╚═════╝ ╚═╝     ╚═╝╚══════╝ ╚═════╝ ╚═╝                                                                                             
```

This is a repository of various A.I + Shell prototypes we have created to test out experiences and
features. **ShellCopilot** is the latest and most finished prototype. It is a CLI tool that creates
an interactive chat session with a registered Large Language Model. 

![GIF showing demo of ShellCopilot](./docs/media/ShellCopilotDemo.gif)

## Installing and Using ShellCopilot

To install ShellCopilot, simply clone this repo onto your system. To build the latest version you
see in the GIF above, run `./build.ps1 -ShellCopilot` and the executable will be in the
`./out/ShellCopilot.App/` path. We suggest adding this directory to your `$PATH` and `$PROFILE`, by
adding this line `$env:PATH += <path to project>\out\ShellCopilot.App` to your profile by doing
`code $PROFILE` if you have VSCode installed. Depending on your OS directory paths may be `\` on
Windows or `/` on Mac. 

## Getting an Azure OpenAI Endpoint

Currently we only support Azure OpenAI LLM endpoints. We are currently hosting a internal only Azure
OpenAI endpoint that you can get and use without getting your Azure OpenAI instance.

Guide for Signing Up For API Key
1.  Navigate to <https://pscopilot.developer.azure-api.net>
2.  Click `Sign Up` located on the top right corner of the page.
3.  Sign up for a subscription by filling in the fields (email, password, first name, last name).
4.  Verify the account (An email should have been sent from
    <apimgmt-noreply@mail.windowsazure.com> to your email)
5.  Click `Sign In` located on the top right corner of the page.
6.  Enter the email and password used when signing up.
7.  Click `Products` located on the top right corner of the page
8.  In the field stating `Your new product subscription name`, Enter `Azure OpenAI Service API`.
9.  Click `Subscribe` to be subscribed to the product.

In order to view your subscription/API key,
1.  Click `Profile` located on the top right corner of the page.
2.  Your Key should be located under the `Subscriptions` section. Click on `Show` to view the
    primary or secondary key.

## Registering your endpoint with ShellCopilot

To register an endpoint you can use the `aish register` subcommand.


PS /Users/stevenbucher/Documents/GitHub/ShellCopilot> aish register --name testendpoint --endpoint https://pscopilot.azure-api.net --key 8429c2a895874d1582dd100c868405a0 --deployment 

# PowerShell Copilot 

**PowerShell Copilot** was the first prototype we created. It is a PowerShell module that allows you
to also have an interactive chat session with an LLM. It has slightly different UX/UI than
ShellCopilot and is specific to PowerShell.

To build PSCopilot, run `./build.ps1 -PSCopilot` and the contents of the module will be put in
`./out/PSCopilot`. Simply run `Import-Module ./out/PSCopilot`, like for ShellCopilot we recommend
you add this command to your `$PROFILE` to be able to use it anytime you open up PowerShell. 

There are two required environment variables you need to set in order for this module to work: 
- `$env:AZURE_OPENAI_API_KEY` to store your API key
- `$env:AZURE_OPENAI_ENDPOINT` to store the endpoint.

You can use the same endpoint and key you got from **Getting an AzureOpenAI endpoint**.

There is an additional optional environment variable called `$env:<TODO GET SYSTEM PROMPT SYNTAX>`,
which when added to will append to the prompt you ask the LLM to help guide its direction. 

For the best experience we suggest running `Enable-PSCopilitKeyHandler` first. This will set `F3`
open and close the interactive chat session in an alternate screen buffer. You can manually enter
and exit the session by using the `Enter-Copilot` cmdlet or typing `exit` when in the interactive
chat session.

**Using the Tool:**

Guide for Signing Up For API Key
1.  Navigate to <https://pscopilot.developer.azure-api.net>
2.  Click `Sign Up` located on the top right corner of the page.
3.  Sign up for a subscription by filling in the fields (email, password, first name, last name).
4.  Verify the account (An email should have been sent from
    <apimgmt-noreply@mail.windowsazure.com> to your email)
5.  Click `Sign In` located on the top right corner of the page.
6.  Enter the email and password used when signing up.
7.  Click `Products` located on the top right corner of the page
8.  In the field stating `Your new product subscription name`, Enter `Azure OpenAI Service API`.
9.  Click `Subscribe` to be subscribed to the product.

In order to view your subscription/API key,
1.  Click `Profile` located on the top right corner of the page.
2.  Your Key should be located under the `Subscriptions` section. Click on `Show` to view the
    primary or secondary key.
