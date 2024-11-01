# DeployRunner

Simple AF Utility to Remotely Deploy (FTP) and Run game builds over local network.
Initially intended as a sidekick for deploying and running unity builds over machines in my LAN.

- Works on Windows/Linux
- Requires python 3.11+, pip, venv

## Disclaimer

⚠ NOT INTENDED TO BE USED ON A PUBLIC, OR NON-TRUSTED NETWORK ! ⚠ 
THIS UTILITY ACCEPTS FILES AND RUNS EXECUTABLES FROM REMOTE MACHINES
WHILE IT DOES NOT ESCALATE PRIVILEGES, IT CAN STILL CAUSE HARM IF USED BY
SOMEONE YOU DO NOT TRUST. USE WITHOUT ANY WARRANTY OF ANY KIND

- USE WITH CAUTION
- TURN OFF WHEN NOT USED
- CHANGE DEFAULT PORTS

## Install and Run

Preqrequisites:
- Clone this git repo or download a release
- Install Python (on windows make sure you add it to path

On Linux:
= Use install_linux.sh script
- Run using run.sh script

On Windows
- Use install_windows.bat script
- Run using run.bat script

## What it does

- Runs an anonymous FTP Server on port 8021 so builds can be uploaded
- Runs a HTTP server that helps manage builds remotely
- Accepts HTTP requests to manage builds (delete, run, stop)
