
using System;
using System.IO;
using System.Collections.Generic;
using WinSCP;
using System.Linq;
using System.ComponentModel;
using System.Configuration;
using System.Threading.Tasks;

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
        string sFileMask = "*|AlfheimLauncher.exe;  WinSCP.exe; WinSCPnet.dll; AlfheimLauncher.pdb; config/; Optional Mods/; cache/; LogOutput.log; patchlogs/;"; // white listed files we ignore

        CurrentMessage = "starting";
        FilesProcessed = 0;
        NumberTotalFiles = 354;//current modpack
        ReadyToLaunch = false;

        // one of the optional mods requires exceptions for valheim_Data
        if (UseCustomAssets())
        {
            AnnounceProgress(0, NumberTotalFiles, "Custom valheim_Data detected, installing...");
            sFileMask += " valheim_Data/;";
            CopyLatestAssets();
        }
 
            
        try
        {
            using (Session session = new Session())
            {
                Directory.CreateDirectory("patchlogs");
                session.DebugLogPath = "patchlogs\\patchdebug.txt";
                session.XmlLogPath = "patchlogs\\xmllog.xml";//System.AppDomain.CurrentDomain.BaseDirectory;
                //session.XmlLogPreserve = true;
                session.SessionLogPath = "patchlogs\\patchlog.txt";
               
                SessionOptions sessionOptions = GetSCPSessionOptions();

                // Will continuously report progress of synchronization
                session.FileTransferred += FileTransferred;
                session.OutputDataReceived += OutputDataReceived;

                // Connect
                AnnounceProgress(0, NumberTotalFiles, "Contacting the server...");
                session.Open(sessionOptions);

                // get a count of files to possibly be patched
                AnnounceProgress(0, NumberTotalFiles, "Checking for updates (this can take a while)...");

                TransferOptions tOptions = new TransferOptions();
                // White List
                tOptions.FileMask = sFileMask;

                ComparisonDifferenceCollection diffs = session.CompareDirectories(SynchronizationMode.Local, System.AppDomain.CurrentDomain.BaseDirectory, remotePath, true, false, SynchronizationCriteria.Size, tOptions);
                NumberTotalFiles = diffs.Count;

                // Synchronize files
                if (NumberTotalFiles > 0)
                {
                    AnnounceProgress(0, NumberTotalFiles, "Updating Files...");
                    foreach(ComparisonDifference diff in diffs)
                    {
                        diff.Resolve(session, tOptions);
                    }
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
            else if(ex.GetType() == typeof(InvalidOperationException))
            {
                string sMessage = string.Format("There were errors connecting to the server: {0}", ex.Message);
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, sMessage);
            }
            else if (ex.GetType() == typeof(ArgumentException))
            {
                string sMessage = string.Format("There were errors executing the whitelist: {0}", ex.Message);
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, sMessage);
            }
            else if (ex.GetType() == typeof(ArgumentOutOfRangeException))
            {
                string sMessage = string.Format("There were errors executing the whitelist (argument out of range): {0}", ex.Message);
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, sMessage);
            }
            else if (ex.GetType() == typeof(SessionLocalException))
            {
                string sMessage = string.Format("There were errors communicating with winscp.com (argument out of range): {0}", ex.Message);
                AnnounceProgress(NumberTotalFiles, NumberTotalFiles, sMessage);
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

    // look to see if the user is using custom valheim data
    private bool UseCustomAssets()
    {
        return Directory.Exists("BepInEx/plugins/Optional Mods/valheim_Data");
    }

    // copy valheim_data assets that are newer
    private void CopyLatestAssets()
    {
        string sourcePath = "BepInEx/plugins/Optional Mods/valheim_Data";
        string destinationPath = "valheim_Data";

        string[] directories = System.IO.Directory.GetDirectories(sourcePath, "*.*", SearchOption.AllDirectories);
        Parallel.ForEach(directories, dirPath =>
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
        });

        string[] files = System.IO.Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);

        Parallel.ForEach(files, newPath =>
        {
            FileInfo oldFile = new FileInfo(newPath);
            FileInfo newFile = new FileInfo(newPath.Replace(sourcePath, destinationPath));
            if(newFile.Exists)
            { 
                if(oldFile.LastWriteTime > newFile.LastWriteTime)
                { 
                    File.Copy(newPath, newPath.Replace(sourcePath, destinationPath),true);
                }
            }
            else
            {
                File.Copy(newPath, newPath.Replace(sourcePath, destinationPath));
            }
        });

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

    protected void OutputDataReceived(object sender, OutputDataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            string sMessage = e.Data;
            //AnnounceProgress(FilesProcessed, NumberTotalFiles, sMessage);
        }
    }
}

