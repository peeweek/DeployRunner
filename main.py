import base64
import os, yaml, threading, subprocess, time, socket, datetime, re
import platform
import shutil
import logging
import pyfiglet
import webbrowser
import argparse

from subprocess import Popen

from flask import Flask, Response, request, render_template
from werkzeug.serving import make_server

from pyftpdlib.authorizers import DummyAuthorizer
from pyftpdlib.handlers import FTPHandler
from pyftpdlib.servers import FTPServer

root = os.path.join(os.path.curdir,'data')
if not os.path.isdir(root):
    os.makedirs(root)


## Log Levels
## 0 = CRITICAL, 1 = ERROR,  2 = INTENT, 3 = INFO, 4 = VERBOSE, 5 = DEBUG
loglevel:int = 5

def log(message : str, level : int = 3):
    if level <= loglevel :
        print(message)

class FTPThread(threading.Thread):
    def __init__(self, address : str, port : int, password: str):
       self.authorizer = DummyAuthorizer()
       self.address = address
       self.port = port
       home = os.path.join(os.path.curdir,'data')
       if(password == ""):
           log("Creating FTP Server in anonymous mode...",2)
           self.authorizer.add_anonymous(homedir=home , perm='elradfmwM')
       else:
           log("Creating FTP Server with deployrunner user and password... ",2)
           self.authorizer.add_user(username='deployrunner', password=password, homedir=home, perm='elradfmwM')
       super(FTPThread, self).__init__()

    def run(self):
       log("Starting FTP Server on port {}...".format(self.port))
       self.handler = FTPHandler
       self.handler.authorizer = self.authorizer
       self.address = (self.address, self.port)
       self.server = FTPServer(self.address, self.handler)
       log("FTP Started !")
       self.server.serve_forever()

    def stopServer(self):
        log("Stopping FTP Server")
        self.server.close_all()
        self.join()

def randomShortStr():
    return datetime.datetime.now().strftime("%y%m%d-%H%M%S")


def readfile(path : str):
    if os.path.isfile(path):
        with open(path, 'r') as f:
            return f.readlines()
    else :
        return None

def reserve_dir(dir : str):
    datapath = os.path.join(os.path.curdir,'data')
    if not os.path.exists(datapath) or not os.path.isdir(datapath):
        # Create folder
        log('Creating Missing Root Data folder : {}'.format(datapath))
        os.makedirs(datapath)

    reserve_path = os.path.join(datapath, dir)
    if os.path.exists(reserve_path):
        log("PATH ALREADY EXISTS : {}, ABORTING".format(reserve_path), 0)
        return False
    else :
        os.makedirs(reserve_path)
        return True

def decodeArgs( argsb64:str):
    return base64.b64decode(argsb64.replace("-","+").replace("_","/").replace(".","=")).decode("utf-8")

run_process : Popen = None

def runBuild(dir:str, executable:str, args:str):
    runlist = []
    runfile = os.path.join(dir, executable)
    log("Trying to run build: {}...".format(runfile),1)
    env = os.environ.copy()
    if(os.path.exists(runfile)):
        if (platform.system() == 'Linux'):
            # Linux : make executable if not already
            log("Linux : Making file executable...".format(executable),2)
            subprocess.run(['chmod +x "{}"'.format(runfile)], shell=True)
            if(executable.endswith(".exe")):
                # Try to run with a Wine
                log("Windows EXE : Running with WINE !",2)
                winepath = "/usr/bin/wine"
                if 'wine-custom' in config:
                    log("Running with Custom WINE : {}".format(config['wine-custom']),2)
                    winepath = config['wine-custom']
                runlist.append(winepath)
                runlist.append(runfile)
                # if wine-prefix is configured in config :
                if 'wine-prefix' in config:
                    log("Using WINEPREFIX={}".format(config['wine-prefix']),2)
                    env['WINEPREFIX']=config['wine-prefix']
            else:
                runlist.append(runfile)

            if 'mangohud' in config and config['mangohud'] == True:
                log("Enabling MangoHUD",2)
                env['MANGOHUD']='1'
        else : # platform.system() == 'Windows"
            runlist.append(runfile)

        global run_process

        ## Process arguments
        if args != "":
            print("Running with arguments : {}".format(args))
            for arg in args :
                runlist.append(arg)

        if loglevel >= 4:
            log("Running Process Arguments :", 4)
            for item in runlist :
                log("     - {}".format(item),4)

        # Finally, run process as POpen
        run_process  = subprocess.Popen(runlist, env=env)


print(pyfiglet.figlet_format('DeployRunner', font='smslant'))

config = None

if not os.path.exists("config.yml"):
    log("WARNING : Config file (config.yml) not found, creating one from default template (config.default)...", 1)
    shutil.copyfile("config.default", "config.yml")

with open("config.yml", encoding='utf-8') as config_file:
    config = yaml.safe_load(config_file)
    log("Config successuflly loaded !", 1)

if "loglevel" in config:
    loglevel = int(config['loglevel'])

print("Log level configured to : {}".format(loglevel))

flask_logger = logging.getLogger('werkzeug')
if(loglevel <= 4):
    flask_logger.setLevel(logging.ERROR);

hostname = socket.gethostname()
ip_address = '127.0.0.1'
if "ip-address" in config :
    ip_address = config['ip-address']
else:
    ip_address = socket.gethostbyname(hostname) #does not work with multiple interfaces

log("Hostname : {}".format(hostname),1)
print(pyfiglet.figlet_format(ip_address, font='moscow').replace('#','█'))
log("(If this is not the IP Address associated to the desired interface,\n please edit the config.yml file to specify the ip-address field)",2)

data = {}
data['hostname'] = hostname
data['ip'] = ip_address
data['system'] = platform.system()

flask_app = Flask(__name__, static_folder='./static', template_folder='./templates')
server = make_server(host=ip_address, port=int(config['http-port']), app=flask_app)

def cleanup():
    root = os.path.join(os.path.curdir,'data')
    for item in os.listdir(root):
        dir = os.path.join(root, item)
        if os.path.isdir(dir):
            shutil.rmtree(dir)

@flask_app.route('/')
def default_route():
    return render_template('main.template.html', config=config, data=data)

@flask_app.route('/html_runinfo')
def htmlruninfo():
    if run_process == None or run_process.poll() is not None :
        return ""
    else:
        exe = run_process.args[0]
        pid = run_process.pid
        return  render_template('runinfo.template.html', exe=exe, pid=pid)

@flask_app.route('/html_refresh')
def htmlrefresh():
    root = os.path.join(os.path.curdir,'data')
    builddata = []
    for item in os.listdir(root):
        item_data = {}
        item_data['name'] = item
        item_data['description'] = '(No Description)'
        descfile = os.path.join(root,item,'.desc')
        if os.path.exists(descfile):
            desc = readfile(descfile)[0]
            item_data['description'] = desc
        runfile = os.path.join(root,item,'.run')
        if os.path.exists(runfile):
            exename = readfile(runfile)[0]
            item_data['executable'] = exename
        builddata.append(item_data)
    return render_template('tables.template.html', builddata=builddata, config=config)

@flask_app.route('/run=<folder>')
def execute(folder : str):
    dir = os.path.join(os.path.curdir, 'data', folder)
    if(os.path.isdir(dir)):
        runfile = os.path.join(dir,'.run')
        log('Trying to find .run file: {}'.format(runfile), 5)
        if(os.path.exists(runfile)):
            exe = readfile(runfile)[0].rstrip()
            runBuild(os.path.abspath(dir), exe, "")
            return "OK!"
    return "ERROR"

@flask_app.route('/run=<folder>&args=<args>')
def executeWithArgs(folder : str, args: str):
    dir = os.path.join(os.path.curdir, 'data', folder)
    if(os.path.isdir(dir)):
        runfile = os.path.join(dir,'.run')
        log('Trying to find .run file: {}'.format(runfile), 5)
        if(os.path.exists(runfile)):
            exe = readfile(runfile)[0].rstrip()
            runBuild(os.path.abspath(dir), exe, decodeArgs(args))
            return "OK!"
    return "ERROR"



@flask_app.route('/builddesc=<folder>')
def builddesc(folder : str):
    dir = os.path.join(os.path.curdir, 'data', folder)
    if(os.path.isdir(dir)):
        descfile = os.path.join(dir,'.desc')
        log('Trying to find .desc file: {}'.format(descfile), 5)
        if(os.path.exists(descfile)):
            desc = readfile(descfile)[0].rstrip()
            return desc
    return "(No Description)"


@flask_app.route('/request=<folder>')
def request(folder : str):
    uufolder = "{}-{}".format(folder, randomShortStr())
    if reserve_dir(uufolder):
        return uufolder
    else:
        return "ERROR"


@flask_app.route("/list")
def buildlist():
    strout = ""
    root = os.path.join(os.path.curdir,'data')
    for item in os.listdir(root):
        strout = strout + "{}\n".format(item)
    return strout

@flask_app.route("/info")
def hostinfo():
    return hostname+ "\n" + ip_address + "\n" + platform.system()

@flask_app.route("/runinfo")
def runinfo():
    if run_process == None or run_process.poll() is not None :
        return "No running process"
    else:
        exe = run_process.args[0]
        pid = run_process.pid
        return  "Process: {}\n{}".format(exe,pid)

@flask_app.route("/kill")
def killrunninginstance():
    if run_process == None or run_process.poll() is not None :
        return "No running process"
    else:
        exe = run_process.args[0]
        pid = run_process.pid
        run_process.kill()
        return  "Killed process : {} (PID:{})".format(exe,pid)

@flask_app.route('/delete=<folder>')
def delete(folder : str):
    root = os.path.join(os.path.curdir,'data')
    dir = os.path.join(root, folder)
    if os.path.isdir(dir):
        shutil.rmtree(dir)
        return "OK"
    else:
        return "ERROR"

def shutdownServer():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is None:
        raise RuntimeError('Not running with the Werkzeug Server')
    time.sleep(120)
    func()

if(__name__ == '__main__'):
    #cleanup()

    argparser = argparse.ArgumentParser()
    argparser.add_argument("--steamdeck", action='store_true')
    args = argparser.parse_args()

    steamdeck = args.steamdeck

    ftppasswd = ''
    if 'ftp-password' in config:
        ftppasswd = config['ftp-password']

    ftp = FTPThread(ip_address, int(config['ftp-port']),ftppasswd)
    ftp.start()

    print("HTTP Server Running, access in browser using http://{}:{}/".format(ip_address, config['http-port']))

    if(steamdeck):
        print("Steam deck detected : Opening browser")
        webbrowser.open("http://{}:{}/".format(ip_address, config['http-port']))

    print("\nIMPORTANT : \n==============\nTo shutdown DeployRunner, please press Ctrl+C (possibly multiple times)\n")
    server.serve_forever()
    print("Server Terminated, Bye !")
    ftp.stopServer()
    ftp.join()
