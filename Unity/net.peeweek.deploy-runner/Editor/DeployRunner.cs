using UnityEngine;
using System;
using System.IO;
using System.Net;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Linq;
using System.Text.RegularExpressions;

public class DeployRunner
{
    [Serializable]
    public struct HostInfo
    {
        public string HostIP;
        public int HTTPPort;
        public int FTPPort;
        public string Password;
    }

    HostInfo hostInfo;

    public int DefaultTimeout = 1000;

    public string HostName { get; private set; }
    public string System { get; private set; }
    public bool Reachable { get; private set; }

    public bool IsBuildRunning { get; private set; }
    public string BuildRunningExecutable { get; private set; }
    public int BuildRunningPID { get; private set; }

    public string LastTemporaryUUID => temporaryUUID;
    string temporaryUUID = string.Empty;

    /// <summary>
    /// Creates a DeployRunner Instance, can be used several times to upload to the same
    /// directory, but can have shorter lifetimes.
    /// Every Time Request is called, the temporaryUUID will change for uploading.
    /// </summary>
    /// <param name="hostInfo"></param>
    public DeployRunner(HostInfo hostInfo)
    {
        this.hostInfo = hostInfo;
    }

    /// <summary>
    /// Creates a .run file into the build directory
    /// </summary>
    /// <param name="path"></param>
    /// <param name="executableName"></param>
    /// <returns></returns>
    public bool CreateRunFile(string path, string executableName)
    {
        // Create .run file and upload it
        string runFilePath = Path.Combine(path, ".run");


        return WriteFileWithContents(runFilePath, executableName);
    }

    public bool CreateDescFile(string path, string description)
    {
        // Create .desc file and upload it
        string descFilePath = Path.Combine(path, ".desc");

        return WriteFileWithContents(descFilePath, description);
    }


    /// <summary>
    /// Internal : Writes a text file with string contents
    /// </summary>
    /// <param name="file"></param>
    /// <param name="contents"></param>
    /// <returns>whether it succeeded</returns>
    bool WriteFileWithContents(string file, string contents)
    {
        try
        {
            var sw = File.CreateText(file);
            sw.WriteLine(contents);
            sw.Close();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }

        return true;
    }


    /// <summary>
    /// Uploads (FTP) the given directory to remote DeployRunner instance.
    /// Handles gathering of files, and directories
    /// </summary>
    /// <param name="buildPath">the path to upload</param>
    /// <returns>Whether the operation was successful</returns>
    public bool UploadBuildDirectory(string buildPath)
    {
        try
        {
            // Gather All Files within build directory
            List<string> files = new List<string>();
            foreach (string file in Directory.EnumerateFiles(buildPath, "*.*", SearchOption.AllDirectories))
            {
                files.Add(file);
            }

            List<string> dirs = new List<string>();
            foreach (string dir in Directory.EnumerateDirectories(buildPath, "*", SearchOption.AllDirectories))
            {
                dirs.Add(dir);
            }

            // Upload Build
            string user = "anonymous";
            string password = $"anonymous@{SystemInfo.deviceName}";

            if (!string.IsNullOrEmpty(this.hostInfo.Password))
            {
                user = "deployrunner";
                password = this.hostInfo.Password;
            }

            var ftp = new FTP($"{this.hostInfo.HostIP}:{this.hostInfo.FTPPort}", user, password);

            EditorUtility.DisplayProgressBar("DeployRunner", $"Create Directory Structure...", 0f);

            // Create directory Structure
            foreach (string dir in dirs)
            {
                var path = dir.Replace(buildPath, "");
                ftp.CreateDirectory($"{temporaryUUID}/{path}");
            }

            //Then upload files
            for (int i = 0; i < files.Count; i++)
            {
                float t = (float)i / files.Count;
                string file = files[i];
                string remotefilename = file.Replace(buildPath, "");
                EditorUtility.DisplayProgressBar("DeployRunner", $"Uploading {remotefilename}...", t);
                ftp.Upload($"{temporaryUUID}/{remotefilename}", file);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.ClearProgressBar();
            return false;
        }

        EditorUtility.ClearProgressBar();
        return true;
    }

    /// <summary>
    /// Deletes Given build from the instance
    /// </summary>
    /// <param name="build"></param>
    /// <returns></returns>
    public bool Delete(string build)
    {
        try
        {
            string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/delete={build}";
            var result = GetHTTPRequest(uri);
            if (result != "OK")
            {
                throw new DeployRunnerException(this.hostInfo, uri, result);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.ClearProgressBar();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Runs a build remotely
    /// </summary>
    /// <returns></returns>
    public bool Run(string build = "")
    {
        try
        {
            if (string.IsNullOrEmpty(build))
                build = temporaryUUID;

            string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/run={build}";
            var result = GetHTTPRequest(uri);
            if (result != "OK!")
            {
                throw new DeployRunnerException(this.hostInfo,uri, result);
            }
        }
        catch (Exception e) {
            Debug.LogException(e);
            EditorUtility.ClearProgressBar();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns (and caches) whether a build is running on the instance
    /// </summary>
    /// <returns></returns>
    public bool UpdateIsRunningBuild()
    {
        BuildRunningExecutable = string.Empty;
        BuildRunningPID = -1;
        string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/runinfo";
        var result = GetHTTPRequest(uri);
        if(result != "No running process")
        {
            var split = result.Split("\n");
            BuildRunningExecutable = split[0];
            BuildRunningPID = int.Parse(split[1]);
            IsBuildRunning = true;
        }
        else
        {
            IsBuildRunning = false;
        }

        return IsBuildRunning;  
    }

    /// <summary>
    /// Kills running instance, if any running
    /// </summary>
    public void KillRunningProcess()
    {
        if(IsBuildRunning)
        {
            string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/kill";
            var result = GetHTTPRequest(uri);
        }
    }


    /// <summary>
    /// Queries the available builds on the Host
    /// </summary>
    /// <returns></returns>
    public string[] ListBuilds()
    {
        string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/list";
        var result = GetHTTPRequest(uri);
        var split = result.Split('\n').ToList();
        split.RemoveAll(o => string.IsNullOrEmpty(o));
        return split.ToArray();
    }


    /// <summary>
    /// Requests to the BuildInfo Server a unique directory to upload
    /// </summary>
    /// <param name="name">Name as prefix of the requested folder</param>
    /// <returns>UUID (name of the folder) Formatted as [name]-[YYMMDD-HHMMSS] or string.empty if unsuccessful</returns>
    /// <exception cref="DeployRunnerException"></exception>
    public string Request(string name)
    {
        this.temporaryUUID = string.Empty;

        try
        {
            name = ReplaceInvalidFileNameCharacters(name, "_");
            string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/request={name}";
            EditorUtility.DisplayProgressBar("DeployRunner", $"Trying to Communicate with {uri} ...", 0.1f);
            var result = GetHTTPRequest(uri);

            if (result == "ERROR" || result == string.Empty)
            {
                throw new DeployRunnerException(this.hostInfo, uri, result);
            }

            this.temporaryUUID = result;
        }
        catch (Exception e)
        { 
            Debug.LogException (e);
        }

        return this.temporaryUUID;
    }


    /// <summary>
    /// Queries the server about build description
    /// </summary>
    /// <param name="uuid"></param>
    /// <returns></returns>
    public string GetDescription(string uuid)
    {
        string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/builddesc={uuid}";
        var result = GetHTTPRequest(uri);
        return result;
    }


    private static Regex k_InvalidRegEx = new(string.Format(@"([{0}]*\.+$)|([{0}]+)", Regex.Escape(new string(Path.GetInvalidFileNameChars()))), RegexOptions.Compiled);
    public static string ReplaceInvalidFileNameCharacters(string input, string replacement = "_") => k_InvalidRegEx.Replace(input, replacement);



    /// <summary>
    /// Fetches information about host
    /// </summary>
    public void UpdateHostInfo()
    {
        string uri = $"http://{this.hostInfo.HostIP}:{this.hostInfo.HTTPPort}/info";
        try
        {
            var response = GetHTTPRequest(uri, timeout: 500).Split("\n");
            if (response.Length == 3)
            {
                HostName =  response[0].ToUpper();
                System = response[2];
                Reachable = true;
            }
            else if (response[0].Contains("timed out"))
            {
                Reachable = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Reachable = false;
        }
    }


    /// <summary>
    /// Utility Function : Fetches an HTTP URL (GET) and returns the string.
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    string GetHTTPRequest(string uri, int timeout=-1)
    {
        //Debug.Log($"GetHTTPRequest: {uri} ...");
        try
        {
            ServicePointManager.DefaultConnectionLimit = 32;
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";

            if(timeout < 0)
                timeout = DefaultTimeout;
            request.Timeout = timeout;
            var response = (HttpWebResponse)request.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (WebException e)
        {
            Debug.LogException(e);
            return e.Message;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return e.Message;
        }
    }

    /// <summary>
    /// Handles Errors for BuildRunner
    /// </summary>
    public class DeployRunnerException : Exception
    {
        public HostInfo hostInfo;
        public string uri;
        public string message;

        public DeployRunnerException(HostInfo hostInfo, string uri, string message)
            :base()
        {
            this.hostInfo = hostInfo;
            this.uri = uri;
            this.message = message;
        }
    }


    /// <summary>
    /// Simple C# FTP Client Class, modified, fixed, and stripped for the use case.
    /// Only used to MKDIR and STOR
    /// 
    /// https://www.codeproject.com/Tips/443588/Simple-Csharp-FTP-Class
    /// http://www.codeproject.com/info/cpol10.aspx
    /// </summary>
    class FTP
    {
        private string host = null;
        private string user = null;
        private string pass = null;
        private FtpWebRequest ftpRequest = null;
        private FtpWebResponse ftpResponse = null;
        private Stream ftpStream = null;
        private int bufferSize = 2048;

        /* Construct Object */
        public FTP(string hostIP, string userName, string password) { host = hostIP; user = userName; pass = password; }

        /* Upload File */
        public void Upload(string remoteFile, string localFile)
        {
            /* Create an FTP Request */
            ftpRequest = (FtpWebRequest)FtpWebRequest.Create("ftp://" + host + "/" + remoteFile);
            /* Log in to the FTP Server with the User Name and Password Provided */
            ftpRequest.Credentials = new NetworkCredential(user, pass);
            /* When in doubt, use these options */
            ftpRequest.UseBinary = true;
            ftpRequest.UsePassive = true;
            ftpRequest.KeepAlive = true;
            /* Specify the Type of FTP Request */
            ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;
            /* Establish Return Communication with the FTP Server */
            ftpStream = ftpRequest.GetRequestStream();
            /* Open a File Stream to Read the File for Upload */
            FileStream localFileStream = new FileStream(localFile, FileMode.Open);
            /* Buffer for the Downloaded Data */
            byte[] byteBuffer = new byte[bufferSize];
            int bytesSent = localFileStream.Read(byteBuffer, 0, bufferSize);
            /* Upload the File by Sending the Buffered Data Until the Transfer is Complete */
            try
            {
                while (bytesSent != 0)
                {
                    ftpStream.Write(byteBuffer, 0, bytesSent);
                    bytesSent = localFileStream.Read(byteBuffer, 0, bufferSize);
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }
            /* Resource Cleanup */
            localFileStream.Close();
            ftpStream.Close();
            ftpRequest = null;
        }

        /* Create a New Directory on the FTP Server */
        public void CreateDirectory(string newDirectory)
        {
            /* Create an FTP Request */
            ftpRequest = (FtpWebRequest)WebRequest.Create("ftp://"+ host + "/" + newDirectory);
            /* Log in to the FTP Server with the User Name and Password Provided */
            ftpRequest.Credentials = new NetworkCredential(user, pass);
            /* When in doubt, use these options */
            ftpRequest.UseBinary = true;
            ftpRequest.UsePassive = true;
            ftpRequest.KeepAlive = true;
            /* Specify the Type of FTP Request */
            ftpRequest.Method = WebRequestMethods.Ftp.MakeDirectory;
            /* Establish Return Communication with the FTP Server */
            ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
            /* Resource Cleanup */
            ftpResponse.Close();
            ftpRequest = null;

        }

        /* List Directory Contents File/Folder Name Only */
        public string[] DirectoryListSimple(string directory)
        {
            try
            {
                /* Create an FTP Request */
                ftpRequest = (FtpWebRequest)FtpWebRequest.Create(host + "/" + directory);
                /* Log in to the FTP Server with the User Name and Password Provided */
                ftpRequest.Credentials = new NetworkCredential(user, pass);
                /* When in doubt, use these options */
                ftpRequest.UseBinary = true;
                ftpRequest.UsePassive = true;
                ftpRequest.KeepAlive = true;
                /* Specify the Type of FTP Request */
                ftpRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                /* Establish Return Communication with the FTP Server */
                ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
                /* Establish Return Communication with the FTP Server */
                ftpStream = ftpResponse.GetResponseStream();
                /* Get the FTP Server's Response Stream */
                StreamReader ftpReader = new StreamReader(ftpStream);
                /* Store the Raw Response */
                string directoryRaw = null;
                /* Read Each Line of the Response and Append a Pipe to Each Line for Easy Parsing */
                try { while (ftpReader.Peek() != -1) { directoryRaw += ftpReader.ReadLine() + "|"; } }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
                /* Resource Cleanup */
                ftpReader.Close();
                ftpStream.Close();
                ftpResponse.Close();
                ftpRequest = null;
                /* Return the Directory Listing as a string Array by Parsing 'directoryRaw' with the Delimiter you Append (I use | in This Example) */
                try { string[] directoryList = directoryRaw.Split("|".ToCharArray()); return directoryList; }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            /* Return an Empty string Array if an Exception Occurs */
            return new string[] { "" };
        }
    }
}
