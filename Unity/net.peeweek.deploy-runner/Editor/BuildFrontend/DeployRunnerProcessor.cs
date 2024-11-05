#if BUILD_FRONTEND
using UnityEngine;
using BuildFrontend;


[CreateAssetMenu(fileName = "DeployRunner Processor", menuName = "DeployRunner Processor")]
public class DeployRunnerProcessor : BuildProcessor
{
    public DeployRunner.HostInfo hostInfo = new DeployRunner.HostInfo()
    {
        Host = "192.168.0.100",
        FTPPort = 8021,
        HTTPPort = 8017,
    };



    DeployRunner runner;

    public override bool OnPostProcess(BuildTemplate template, bool wantRun)
    {
        this.runner = new DeployRunner(this.hostInfo);

        string buildName = template.ExecutableName;

        if (runner.Request(buildName) == string.Empty)
            return false;

        if (!this.runner.CreateRunFile(template.buildFullPath, template.ExecutableName))
            return false;

        if(!this.runner.UploadBuildDirectory(template.buildFullPath))
            return false;

        // If WantRun, run remotely
        if(wantRun)
        {
            return this.runner.Run();
        }

        return true;
    }

    public override bool OnPreProcess(BuildTemplate template, bool wantRun)
    {
        return true;
    }

}
#endif