
using System;
using System.IO;
using System.Collections.Generic;
using WinSCP;
using System.Linq;
using System.ComponentModel;
using System.Configuration;

public delegate void Notify();

/// <summary>
/// Class that checks the local filesystem against a remote one, updating the local one when the remote one has a more current file
/// We make extensive use of the scp NUGET package here.  Documentation can be found at https://winscp.net/eng/docs/library
/// </summary>
public class Patcher : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    string remotePath = ConfigurationManager.AppSettings.Get("RemoteDirectory");//"/Alfheim";
    public int NumberTotalFiles { get; set; }
    public int FilesProcessed { get; set; } 
    public string CurrentMessage { get; set; }
    public string PlayableStatus { get; set; }
    public bool ReadyToLaunch { get; set; }

    
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler == null) return;
        handler(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Patch()
    {
        PlayableStatus = "Updating...";
        OnPropertyChanged("PlayableStatus");
        try
        {
            using (Session session = new Session())
            {
                CurrentMessage = "starting";
                FilesProcessed = 0;
                NumberTotalFiles = 354;//current modpack
                ReadyToLaunch = false;
                SessionOptions sessionOptions = GetSCPSessionOptions();

                // Will continuously report progress of synchronization
                session.FileTransferred += FileTransferred;

                // Connect
                AnnounceProgress(0, NumberTotalFiles, "Contacting the server...");
                session.Open(sessionOptions);

                // get a count of files to possibly be patched
                AnnounceProgress(0, NumberTotalFiles, "Checking for updates...");
                var opts = WinSCP.EnumerationOptions.EnumerateDirectories |
                       WinSCP.EnumerationOptions.AllDirectories;
                IEnumerable<RemoteFileInfo> fileInfos =
                    session.EnumerateRemoteFiles(remotePath, null, opts);
                int NumberFilesToPatch = 0;
                //iterate through the files and compare dates
                foreach (var fileInfo in fileInfos)
                {
                    if (fileInfo.IsDirectory) continue;
                    string sRemotePathName = fileInfo.FullName.Replace("/"+remotePath+"/", "");
                    FileInfo localFile = new FileInfo(sRemotePathName);
                    if(!localFile.Exists) // we dont have a local copy
                    {
                        NumberFilesToPatch++;
                        continue;
                    }
                    long nRemoteSize =fileInfo.Length;
                    long nLocalSize = localFile.Length;
                    if(nRemoteSize != nLocalSize)
                    {
                        NumberFilesToPatch++;
                    }
                }
                NumberTotalFiles = NumberFilesToPatch;
                // Synchronize files
                if (NumberTotalFiles > 0)
                {
                    AnnounceProgress(0, NumberTotalFiles, "Updating Files...");
                    TransferOptions tOptions = new TransferOptions();
                    // White List
                    tOptions.FileMask = "*|AlfheimLauncher.exe;  WinSCP.exe; WinSCPnet.dll; AlfheimLauncher.pdb; config/; Optional Mods/;";
                    SynchronizationResult synchronizationResult;
                    synchronizationResult =
                        session.SynchronizeDirectories(
                            SynchronizationMode.Local,
                            System.AppDomain.CurrentDomain.BaseDirectory, remotePath, true, false, SynchronizationCriteria.Size,tOptions);

                    // Throw on any error
                    synchronizationResult.Check();
                }
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, "Game up to date");
            }
        }
        catch (Exception ex)
        {
            if (ex.GetType() == typeof(TimeoutException))
            {
                string sMessage = "Connection timed out.  Attempting to recover...";
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, sMessage);
                Patch();
            }
            else
            {
                string sMessage = string.Format("There were errors updating: {0}", ex.Message);
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, sMessage);
            }
        }
        ChangeLaunchable("Play Game", true);
    }

    // we make this public so that the gui can call changes
    public void ChangeLaunchable(string p_sStatus, bool p_bLaunchable)
    {
        PlayableStatus = p_sStatus;
        OnPropertyChanged("PlayableStatus");
        ReadyToLaunch = p_bLaunchable;
        OnPropertyChanged("ReadyToLaunch");
    }
    // we keep our credentials in their own function for ease of maintenance
    protected SessionOptions GetSCPSessionOptions()
    {
        SessionOptions sessionOptions = new SessionOptions
        {
            //This account has read-only access to the latest game files, and no permissions beyond that
            Protocol = Protocol.Sftp,
            HostName = ConfigurationManager.AppSettings.Get("HostName"),//"proftp.drivehq.com",
            UserName = ConfigurationManager.AppSettings.Get("UserName"),//"alfheim_patcher",
            Password = ConfigurationManager.AppSettings.Get("PassWord"),//"@lfh31m",
            SshHostKeyFingerprint = ConfigurationManager.AppSettings.Get("SshHostKeyFingerprint"),//"ssh-rsa 2048 RER9DGvD1QayPkv1mx/bVdoL8TyQzAST3ygYGtRxpt0=",
        };
        return sessionOptions;
    }

    // updates variables that the gui can bind and issues a changed message
    protected void AnnounceProgress(int p_nNumProgress, int p_nNumTotalToProcess, string p_sMessage)
    {
        FilesProcessed = p_nNumProgress;
        OnPropertyChanged("FilesProcessed");
        NumberTotalFiles= p_nNumTotalToProcess;
        OnPropertyChanged("NumberTotalFiles");
        CurrentMessage = p_sMessage;
        OnPropertyChanged("CurrentMessage");
    }

    /// <summary>
    /// Event handler for scp file transferred events
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void FileTransferred(object sender, TransferEventArgs e)
    {
        FilesProcessed++;

        if (e.Error == null)
        {
            string sMessage = string.Format("Updated {0} ({1} of {2})", e.FileName, FilesProcessed, NumberTotalFiles);
            AnnounceProgress(FilesProcessed,NumberTotalFiles,sMessage);
        }
        else
        {
            string sMessage = string.Format("Could not update {0} : {1}", e.FileName, e.Error);
            AnnounceProgress(FilesProcessed, NumberTotalFiles, sMessage);
        }
    }
}
