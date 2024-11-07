﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static DeployRunner;

public class DeployRunnerEditorWindow : EditorWindow
{
    [MenuItem("File/Deploy Runner", priority =220)]
    static void Open()
    {
        GetWindow<DeployRunnerEditorWindow>();
    }

    private void OnEnable()
    {
        LoadHostInfos();
        this.titleContent = Contents.title;
        RefreshAll(false);
        EditorApplication.update += PeriodicUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= PeriodicUpdate;
    }

    double nextRefreshTime;

    void PeriodicUpdate()
    {
        double t = EditorApplication.timeSinceStartup;
        if (nextRefreshTime == 0.0 || t > nextRefreshTime)
        {
            nextRefreshTime = t + 12.0;
            RefreshAll();
        }
    }

    void RefreshWithDelay(double delay)
    {
        nextRefreshTime = EditorApplication.timeSinceStartup + delay;
    }

    int selected = 0;
    string addNewHostIP = "192.168.0.1";
    int addNewHostHTTPPort = 8017;
    int addNewHostFTPPort = 8021;

    string description = "(No Description)";

    void RefreshAll(bool force = false)
    {
        for(int i = 0; i < m_HostInfos.Count; i++)
        {
            Refresh(i, force);
        }
    }

    void Refresh(int index, bool force = false)
    {
        var host = m_HostInfos[index].HostIP;
        if (m_CachedRunners.ContainsKey(host) && m_CachedRunners[host].Reachable || force)
        {
            QueryHostAlive(m_HostInfos[index]);

            if (m_SelectedRunnerBuilds != null)
            {
                m_SelectedRunnerBuilds = m_CachedRunners[m_HostInfos[selected].HostIP].ListBuilds();
            }
        }
    }

    void ConnectProfiler(string ip)
    {
        ProfilerDriver.DirectIPConnect(ip);
        ProfilerDriver.enabled = true;
    }



    private void OnGUI()
    {
        using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                RefreshAll(true);

            GUILayout.FlexibleSpace();
            GUILayout.Button(EditorGUIUtility.IconContent("Profiler.NetworkMessages"), EditorStyles.toolbarButton);
        }
        using (new GUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
            using (new GUILayout.VerticalScope(GUILayout.Width(280), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label("Hosts", Styles.Header);

                for (int i = 0; i < m_HostInfos.Count; i++)
                {
                    var hostInfo = m_HostInfos[i];
                    string hostIP = hostInfo.HostIP;

                    GUIContent label = new GUIContent($"({hostIP}) UNKNOWN", Contents.iconDisabled.image);

                    if(m_CachedRunners.ContainsKey(hostInfo.HostIP))
                    {
                        var runner = m_CachedRunners[hostInfo.HostIP];
                        if(runner.Reachable)
                        {
                            label.text = $"{runner.HostName} ({hostIP}) ONLINE";
                            label.image = Contents.iconConnected.image;
                        }
                        else
                        {
                            label.text = $"{runner.HostName} ({hostIP}) OFFLINE";
                            label.image = Contents.iconDisconnected.image;

                        }
                    }

                    if (GUILayout.Button(label, Styles.FlatButton))
                    {
                        selected = i;
                        addNewHostIP = hostInfo.HostIP;
                        addNewHostHTTPPort = hostInfo.HTTPPort;
                        addNewHostFTPPort = hostInfo.FTPPort;

                        Refresh(i, false);
                    }
                    if (i == selected)
                    {
                        float gray = EditorGUIUtility.isProSkin ? 1 : 0;
                        EditorGUI.DrawRect(GUILayoutUtility.GetLastRect(), new Color(gray, gray, gray, 0.1f));
                    }
                }

                GUILayout.FlexibleSpace();
                Rect rect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(rect, Color.red);

                using(new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUIUtility.labelWidth = 80;
                    GUILayout.Label("Add new Host:", EditorStyles.boldLabel);
                    this.addNewHostIP = EditorGUILayout.TextField("IP", addNewHostIP);
                    this.addNewHostHTTPPort = EditorGUILayout.IntField("HTTP Port", addNewHostHTTPPort);
                    this.addNewHostFTPPort = EditorGUILayout.IntField("FTP Port", addNewHostFTPPort);

                    
                    if (GUILayout.Button("Add New"))
                    {
                        this.m_HostInfos.Add(new DeployRunner.HostInfo()
                        {
                            HostIP = this.addNewHostIP,
                            HTTPPort = addNewHostHTTPPort,
                            FTPPort = addNewHostFTPPort,
                        });
                        this.addNewHostIP = "192.168.0.1";
                        this.addNewHostHTTPPort = 8017;
                        this.addNewHostFTPPort = 8021;
                        SaveHostInfos();
                    }

                    EditorGUI.BeginDisabledGroup(selected == -1);
                    if (GUILayout.Button("Edit Selected"))
                    {
                        var hostinfo = m_HostInfos[selected];
                        hostinfo.HostIP = this.addNewHostIP;
                        hostinfo.HTTPPort = addNewHostHTTPPort;
                        hostinfo.FTPPort = addNewHostFTPPort;
                        m_HostInfos[selected] = hostinfo;
                        SaveHostInfos();
                    }
                    if (GUILayout.Button("Delete Selected"))
                    {
                        m_HostInfos.RemoveAt(selected);
                        selected = m_HostInfos.Count == 0 ? -1 : Mathf.Clamp(selected -1, 0, m_HostInfos.Count-1);
                        SaveHostInfos();
                    }

                    EditorGUI.EndDisabledGroup();
                }
            }
            Rect r = GUILayoutUtility.GetLastRect();
            r.xMin = r.xMax-1;
            EditorGUI.DrawRect(r, new Color(0, 0, 0, 1));

            using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                var hostInfo = m_HostInfos[selected];

                if (!m_CachedRunners.ContainsKey(hostInfo.HostIP))
                {
                    GUILayout.Label(new GUIContent(hostInfo.HostIP, Contents.iconBigDisabled.image), Styles.H1);
                    GUILayout.Label("Host Status UNKNOWN (Not reached yet?)\nPlease click Retry to attempt connection anew.");
                    if (GUILayout.Button("Retry Connection", GUILayout.Width(200)))
                    {
                        Refresh(selected, true);
                    }
                }     
                else if(m_CachedRunners[hostInfo.HostIP].Reachable == false)
                {
                    GUILayout.Label(new GUIContent(hostInfo.HostIP, Contents.iconBigDisconnected.image), Styles.H1);
                    GUILayout.Label("HOST OFFLINE : Check connectivity, then click Refresh to retry manually");
                    if(GUILayout.Button("Refresh", GUILayout.Width(200)))
                    {
                        Refresh(selected, true);
                    }
                }
                else // Runner is Alive
                {

                    var runner = m_CachedRunners[hostInfo.HostIP];

                    using(new GUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                    {
                        using(new GUILayout.VerticalScope())
                        {
                            GUILayout.Label(new GUIContent(runner.HostName, Contents.iconBigConnected.image), Styles.H1);
                            GUILayout.Label($"{hostInfo.HostIP} ({runner.System})");
                            if (GUILayout.Button($"http://{hostInfo.HostIP}:{hostInfo.HTTPPort}", GUILayout.ExpandWidth(false)))
                            {
                                Application.OpenURL($"http://{hostInfo.HostIP}:{hostInfo.HTTPPort}");
                            }
                        }
                        GUILayout.FlexibleSpace();
                        using(new GUILayout.VerticalScope(GUILayout.Width(190)))
                        {
                            if(GUILayout.Button("Upload", GUILayout.Height(40)))
                            {
                                string path = EditorUtility.OpenFilePanelWithFilters("Select your executable file.", Application.dataPath, new string[] { "Executable Files", "exe,run,x86_64", "Any File", "*"});
                                if(File.Exists(path))
                                {
                                    var folder = Path.GetDirectoryName(path);
                                    var exe = Path.GetFileName(path);
                                    var exewe = Path.GetFileNameWithoutExtension(path);
                                    var uuid = runner.Request(exewe);
                                    runner.CreateRunFile(folder, exe);
                                    runner.CreateDescFile(folder, description);
                                    runner.UploadBuildDirectory(folder);
                                    RefreshWithDelay(0.1);
                                }
                            }
                            GUILayout.Space(12);
                            GUILayout.Label("Description:");
                            this.description = GUILayout.TextField(this.description);
                        }
                    }

                    GUILayout.Space(24);

                    if (m_SelectedRunnerBuilds.Length > 0)
                    {
                        if(runner.IsBuildRunning)
                        {
                            GUILayout.Label("Currently Running Build", Styles.H2);
                            GUILayout.Label($"{runner.BuildRunningExecutable} (PID:{runner.BuildRunningPID})");
                            using (new GUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Kill Process", GUILayout.Width(120)))
                                {
                                    if (EditorUtility.DisplayDialog("Kill Remote Process?", $"Do you want to kill the running instance on {runner.HostName}? ", "Yes", "No"))
                                    {
                                        runner.KillRunningProcess();
                                        RefreshWithDelay(0.2);
                                    }
                                }
                                if (GUILayout.Button("Connect Profiler", GUILayout.Width(120)))
                                {
                                    ConnectProfiler(hostInfo.HostIP);
                                }

                            }

                            GUILayout.Space(8);
                        }

                        GUILayout.Label("Available Builds", Styles.H2);
                        GUILayout.Space(8);

                        using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true), GUILayout.Height(24)))
                        {
                            GUILayout.Label("Build Name", Styles.Header, GUILayout.ExpandWidth(true));
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Run", Styles.Header, GUILayout.Width(64));
                            GUILayout.Label("Delete", Styles.Header, GUILayout.Width(64));
                        }

                        r = GUILayoutUtility.GetLastRect();
                        r.height = 1;
                        EditorGUI.DrawRect(r, new Color(0, 0, 0, 1));

                        foreach (var build in m_SelectedRunnerBuilds)
                        {
                            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                            {
                                GUILayout.Label(build, Styles.ToolbarLabel, GUILayout.ExpandWidth(false));
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(64)))
                                {
                                    runner.Run(build);
                                    RefreshWithDelay(0.1);
                                }
                                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(64)))
                                {
                                    if (EditorUtility.DisplayDialog("Deploy Runner", $"Are you sure you want to delete the build on host {runner.HostName} ({hostInfo.HostIP}) ? \n\n Build Name :\n {build} \n\nThis operation cannot be undone.", "Yes, Proceed with Delete", "No"))
                                        runner.Delete(build);


                                }
                            }
                        }
                    }
                }
            }
        }
    }


    Dictionary<string, DeployRunner> m_CachedRunners = new Dictionary<string, DeployRunner>();
    string[] m_SelectedRunnerBuilds = new string[0];

    bool QueryHostAlive(DeployRunner.HostInfo info)
    {
        try
        {
            EditorUtility.DisplayProgressBar("Deploy Runner", $"Getting information on Host :{info.HostIP} ...", 0.2f);

            if (!m_CachedRunners.ContainsKey(info.HostIP))
            {
                var dr = new DeployRunner(info);
                dr.DefaultTimeout = 400;
                m_CachedRunners.Add(info.HostIP, dr);
            }

            m_CachedRunners[info.HostIP].UpdateHostInfo();
            m_CachedRunners[info.HostIP].UpdateIsRunningBuild();
        }
        catch(Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        return m_CachedRunners[info.HostIP].Reachable;
    }


    #region HOST INFORMATION

    List<DeployRunner.HostInfo> m_HostInfos;

    public bool TryGetHostInfo(string ip, out DeployRunner.HostInfo info)
    {
        info = new DeployRunner.HostInfo();
        foreach(var i in m_HostInfos)
        {
            if(i.HostIP == ip)
            {
                info = i;
                return true;
            }
        }
        return false;
    }

    public void LoadHostInfos()
    {
        if(m_HostInfos == null)
            m_HostInfos = new List<DeployRunner.HostInfo>();
        else
            m_HostInfos.Clear();

        m_HostInfos = UnpackPrefString(EditorPrefs.GetString("DeployRunner.HostInfoList", GetDefaultPrefString()));
    }

    public void SaveHostInfos()
    {
        EditorPrefs.SetString("DeployRunner.HostInfoList", PackPrefString(m_HostInfos));
    }

    List<DeployRunner.HostInfo> UnpackPrefString(string prefString)
    {
        List<DeployRunner.HostInfo> hostInfos = new List<DeployRunner.HostInfo>();

        var items = prefString.Split('|');
        foreach(var item in items)
        {
            var values = item.Split(';');
            var hostInfo = new DeployRunner.HostInfo()
            {
                HostIP = values[0],
                HTTPPort = int.Parse(values[1]),
                FTPPort = int.Parse(values[2]),
            };
            hostInfos.Add(hostInfo);
        }
        return hostInfos;
    }

    string PackPrefString(List<DeployRunner.HostInfo> hostInfos)
    {
        List<string> items = new List<string>();
        foreach(var hostInfo in hostInfos)
        {
            items.Add($"{hostInfo.HostIP};{hostInfo.HTTPPort};{hostInfo.FTPPort}");
        }
        return string.Join("|", items.ToArray());
    }

    string GetDefaultPrefString()
    {
        var list = new List<DeployRunner.HostInfo>();
        list.Add(new DeployRunner.HostInfo()
        {
            HostIP = "127.0.0.1",
            HTTPPort = 8017,
            FTPPort = 8021
        });

        return PackPrefString(list);
    }


    #endregion

    static class Contents
    {
        public static GUIContent title;

        public static GUIContent iconConnected;
        public static GUIContent iconDisconnected;
        public static GUIContent iconDisabled;

        public static GUIContent iconBigConnected;
        public static GUIContent iconBigDisconnected;
        public static GUIContent iconBigDisabled;

        static Contents()
        {
            var icon = EditorGUIUtility.IconContent("Profiler.NetworkMessages");
            title = new GUIContent("Deploy Runner", icon.image);

            iconConnected = EditorGUIUtility.IconContent("CacheServerConnected");
            iconDisconnected = EditorGUIUtility.IconContent("CacheServerDisconnected");
            iconDisabled = EditorGUIUtility.IconContent("CacheServerDisabled");

            iconBigConnected = EditorGUIUtility.IconContent("CacheServerConnected@2x");
            iconBigDisconnected = EditorGUIUtility.IconContent("CacheServerDisconnected@2x");
            iconBigDisabled = EditorGUIUtility.IconContent("CacheServerDisabled@2x");
        }
    }

    static class Styles
    {
        public static GUIStyle FlatButton;
        public static GUIStyle Header;
        public static GUIStyle ToolbarLabel;
        public static GUIStyle H1;
        public static GUIStyle H2;

        static Styles()
        {

            FlatButton = new GUIStyle(EditorStyles.foldoutHeader);
            FlatButton.padding = new RectOffset(16, 16, 0, 0);
            FlatButton.margin = new RectOffset(0, 0, 0, 0);
            FlatButton.fixedHeight = 22;
            FlatButton.fontStyle = FontStyle.Normal;

            Header = new GUIStyle(EditorStyles.toolbar);
            Header.stretchWidth = true; 
            Header.fixedHeight = 22;
            Header.alignment = TextAnchor.MiddleCenter;
            Header.fontSize = 12;
            Header.fontStyle = FontStyle.Bold;

            ToolbarLabel = new GUIStyle(EditorStyles.toolbar);
            ToolbarLabel.stretchWidth = true;
            ToolbarLabel.fixedHeight = 22;
            ToolbarLabel.alignment = TextAnchor.MiddleLeft;
            ToolbarLabel.fontSize = 12;

            H1 = new GUIStyle(EditorStyles.boldLabel);
            H1.fontSize = 22;
            H2 = new GUIStyle(EditorStyles.boldLabel);
            H2.fontSize = 16;

        }
    }
}
