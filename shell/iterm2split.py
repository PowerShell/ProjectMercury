import sys
import importlib.util
  
# Check the iTerm2 package is installed
package_name = "iterm2"
if importlib.util.find_spec(package_name) is None:
    print(package_name +" is not installed, please install it with your pip tool")
    exit()
    
import iterm2

# iTerm needs to be running for this to work
async def main(connection):
    app = await iterm2.async_get_app(connection)
    print(app_path)

    # Foreground the app
    await app.async_activate()

    window = app.current_terminal_window
    if window is not None:
        # Get the current pane so that we can split it 
        currentpane = app.current_terminal_window.current_tab.current_session
        # Split pane vertically
        splitpane = await currentpane.async_split_pane(vertical=True)


        await splitpane.async_send_text(f'{app_path} --channel {channel}\n')
        
    else:
        # You can view this message in the script console.
        print("No current window")


if len(sys.argv) > 1:
    app_path = sys.argv[1]  
    channel = sys.argv[2]
    # Passing True for the second parameter means keep trying to
    # connect until the app launches.
    iterm2.run_until_complete(main, True)
else:
    print("Please provide the application path as a command line argument.")

# path to aish (string)
# piped channel name (string) 
# figure out how to split the pane

# Example usage
check_and_install_module('numpy')
