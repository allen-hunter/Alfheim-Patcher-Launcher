using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Timers;

namespace VikingBros
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Patcher m_patcher = new Patcher();
        private static System.Timers.Timer m_IsLaunchedTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            DataContext = m_patcher;
            MOTDWIndow.Source = new Uri(ConfigurationManager.AppSettings.Get("MOTD"));
       
            Task T1 = new Task(m_patcher.Patch);
            T1.Start();
        }

        // Button Handlers

        // Starts Valheim as admin
        private void ButtonLaunch_Click(object sender, RoutedEventArgs e)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "valheim.exe";
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "runas";
            proc.Start();

            // keep from launching again
            m_patcher.ChangeLaunchable("Game Running", false);
            // start timer looking for whether the game is running or not
            SetTimer();
        }

        private void ButtonDiscord_Click(object sender, RoutedEventArgs e)
        {
            string target = ConfigurationManager.AppSettings.Get("DiscordKey");// "https://discord.com/invite/TVfFTbD8?utm_source=Discord%20Widget&utm_medium=Connect&username=null";
            if (null != target)
            {
                System.Diagnostics.Process.Start(target);
            }
        }

        private void ButtonWiki_Click(object sender, RoutedEventArgs e)
        {
            string target = "https://unovalegends.fandom.com/wiki/Alfheim";
            System.Diagnostics.Process.Start(target);
        }

        private void ButtonFiles_Click(object sender, RoutedEventArgs e)
        {
            string sPath = System.AppDomain.CurrentDomain.BaseDirectory + "BepInEx\\plugins\\Optional Mods";
            System.Diagnostics.Process.Start("explorer.exe", sPath);
        }

        private void ButtonPatreon_Click(object sender, RoutedEventArgs e)
        {
            string target = ConfigurationManager.AppSettings.Get("PatreonPage");// "https://discord.com/invite/TVfFTbD8?utm_source=Discord%20Widget&utm_medium=Connect&username=null";
            if (null != target)
            {
                System.Diagnostics.Process.Start(target);
            }
        }

        private void SetTimer()
        {
            // Create a timer with a two second interval.
            m_IsLaunchedTimer = new System.Timers.Timer(2000);
            // Hook up the Elapsed event for the timer. 
            m_IsLaunchedTimer.Elapsed += OnIsLaunchedTimer;
            m_IsLaunchedTimer.AutoReset = true;
            m_IsLaunchedTimer.Enabled = true;
        }

        private  void OnIsLaunchedTimer(Object source, ElapsedEventArgs e)
        {
            Process[] processes = Process.GetProcessesByName("valheim");
            if (processes.Length == 0)
            {
                m_patcher.ChangeLaunchable("Play Game", true);
                m_IsLaunchedTimer.Stop();
                m_IsLaunchedTimer.Dispose();
            }
        }
    }
}