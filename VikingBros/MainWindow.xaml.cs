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

namespace VikingBros
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Patcher m_patcher = new Patcher();
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
            System.Diagnostics.Process.Start("explorer.exe", System.AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}