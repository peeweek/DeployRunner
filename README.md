# DeployRunner - Windows/Linux server and Unity Client

![Screenshot of DeployRunner](https://raw.githubusercontent.com/peeweek/DeployRunner/refs/heads/master/.images/screenshot.jpg)

Rough Simple AF Utility to Remotely Deploy (FTP) and Run game builds over local network.
Initially intended as a sidekick for deploying and running unity builds over machines in my LAN, similarly to basic functionality any Console Devkit provides.

- Works on Windows/Linux
- Requires python 3.11+, pip, venv on remote machines

## Disclaimer

⚠ NOT INTENDED TO BE USED ON A PUBLIC, OR NON-TRUSTED NETWORK ! ⚠ 
THIS UTILITY ACCEPTS ANY REMOTE FILES AND RUNS EXECUTABLES ON REMOTE MACHINES
WHILE IT DOES NOT ESCALATE PRIVILEGES, IT CAN STILL CAUSE HARM IF USED BY
SOMEONE YOU DO NOT TRUST. THIS SOFTWARE IS CLUNKY, PROVIDED AS-IS. 

USE WITHOUT ANY WARRANTY OF ANY KIND. 
I AM NOT RESPONSIBLE FOR ANY LOSS OF DATA, NUCLEAR WINTER FALLOUT, 
OR ANYTHING RELATED TO MISUSE OF THIS SOFTWARE.

- WHILE(!UNDERSTOOD) READ_AGAIN();
- TURN OFF WHEN NOT USED

## Install and Run

Preqrequisites:
- Clone this git repo or download a release
- Install Python (on windows make sure you add it to path)

On Linux:
- Use install_linux.sh script
- Run using run.sh script

On Windows
- Use install_windows.bat script
- Run using run.bat script

Unity (2022.3+)
- Create Symlink of the ./Unity/net.peeweek.deploy-runner/ directory in your project ./Packages/net.peeweek.deploy-runner/ directory, or reference directly the package using the package manager GUI.
- The pacakge is Editor Only, so it shouldn't bother your builds.

## What it does (TL;DR)

- Runs an anonymous FTP Server (default on port 8021) so builds can be uploaded to Data Directory
- Runs a python HTTP server that helps manage builds remotely, using some old-school text API (not REST, nor JSON)
- Accepts HTTP requests to manage builds (delete, run, stop)
- Provides a Unity Package (inside Unity folder) with minimal code to interact with the server.
  - Hosts are saved as preferences (for convenience through projects)
  - Can upload builds from the editor, run them, delete them, kill them, attach the profiler to them 

# TODO
- Security, passwords, etc.
- Manage PEBKAC issues (for instance delete while running build)
