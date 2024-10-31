import os, io, sys, yaml, threading, subprocess, time, webbrowser
import shutil
import uuid

from flask import Flask, Response, request, render_template
from werkzeug.serving import make_server


from pyftpdlib.authorizers import DummyAuthorizer
from pyftpdlib.handlers import FTPHandler
from pyftpdlib.servers import FTPServer

class FTPThread(threading.Thread):
    def __init__(self, port : int):
       self.authorizer = DummyAuthorizer()
       self.port = port
       home = os.path.join(os.path.curdir,'data')
       self.authorizer.add_anonymous(homedir=home , perm='elradfmwM')
       super(FTPThread, self).__init__()

    def run(self):
       print("Starting FTP Server on port {}...".format(self.port))
       self.handler = FTPHandler
       self.handler.authorizer = self.authorizer
       self.address = ('localhost', self.port)
       self.server = FTPServer(self.address, self.handler)
       print("FTP Started !")
       self.server.serve_forever()

    def stopSever(self):
        print("Stopping FTP Server")
        self.server.close_all()

def randomShortStr():
    return str(uuid.uuid4()).split("-")[0]

def randomLongStr():
    return str(uuid.uuid4())

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
        print('Creating Missing Root Data folder : {}'.format(datapath))
        os.makedirs(datapath)

    reserve_path = os.path.join(datapath, dir)
    if os.path.exists(reserve_path):
        print("PATH ALREADY EXISTS : {}, ABORTING".format(reserve_path))
        return False
    else :
        os.makedirs(reserve_path)
        return True

process = None

def runBuild(dir:str, executable:str):
    path = os.path.join(dir, executable)
    print("Trying to run build: {}".format(path))
    if (os.path.isfile(path)):
        process = subprocess.Popen([path])
    return

config = None
with open("config.yml", encoding='utf-8') as config_file:
    config = yaml.safe_load(config_file)

flask_app = Flask(__name__, static_folder='./static', template_folder='./templates')
server = make_server(host='127.0.0.1', port=int(config['http-port']), app=flask_app)

def cleanup():
    root = os.path.join(os.path.curdir,'data')
    for item in os.listdir(root):
        dir = os.path.join(root, item)
        if os.path.isdir(dir):
            shutil.rmtree(dir)

@flask_app.route('/')
def default_route():
    return render_template('main.template.html', config=config)

@flask_app.route('/refresh')
def refresh():
    root = os.path.join(os.path.curdir,'data')
    data = list()
    for item in os.listdir(root):
        item_data = {}
        item_data['name'] = item
        item_data['description'] = '(No Description)'
        runfile = os.path.join(root,item,'.run')
        if os.path.exists(runfile):
            exename = readfile(runfile)[0]
            item_data['executable'] = exename
        data.append(item_data)
    return render_template('tables.template.html', data=data, config=config)

@flask_app.route('/execute=<folder>')
def execute(folder : str):
    dir = os.path.join(os.path.curdir, 'data', folder)
    if(os.path.isdir(dir)):
        runfile = os.path.join(dir,'.run')
        if(os.path.isfile(runfile)):
            exe = readfile(runfile)[0]
            runBuild(dir, exe)
    return


@flask_app.route('/request=<folder>')
def request(folder : str):
    if reserve_dir(folder):
        return "OK"
    else:
        return "ERROR"

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

    ftp = FTPThread(int(config['ftp-port']))
    ftp.start()

    webbrowser.open("http://127.0.0.1:{}".format(config['http-port']))
    print("Running HTTP Server....")
    server.serve_forever()
    print("Server Terminated, Bye !")
    ftp.stopServer()
    ftp.join()
