﻿using Clipple.View;
using Clipple.ViewModel;
using FFmpeg.AutoGen;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using MahApps.Metro.Controls.Dialogs;
using Squirrel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace Clipple
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static RootViewModel ViewModel => (RootViewModel)Current.Resources[nameof(RootViewModel)];

        public static MainWindow Window => (MainWindow)Current.MainWindow;

        public static Player MediaPlayer => ViewModel.VideoPlayerViewModel.MediaPlayer;

        public static bool VideoPlayerVisible
        {
            get => ViewModel.VideoPlayerViewModel.VideoVisibility == Visibility.Visible;
            set => ViewModel.VideoPlayerViewModel.VideoVisibility = value ? Visibility.Visible : Visibility.Hidden;
        }

        public static Timer AutoSaveTimer { get; } = new Timer();

        public static string LibPath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Binaries", Environment.Is64BitProcess ? "64" : "32");
        
        public App()
        {
            // This timer is started when the settings load in the RootViewModel
            AutoSaveTimer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(async () =>
                {
                    if (ViewModel.SettingsViewModel.AutoSave)
                        await ViewModel.Save();
                });
            };

            try
            {
                Engine.Start(new EngineConfig()
                {
                    FFmpegLogLevel = FFmpegLogLevel.Debug,
                    FFmpegPath = LibPath,
                    UIRefresh = true,
                    UIRefreshInterval = 100,
                    UICurTimePerSecond = false,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load dependencies", ex.Message);
                Shutdown(1);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SquirrelAwareApp.HandleEvents(
                onInitialInstall: OnAppInstall,
                onAppUninstall: OnAppUninstall,
                onEveryRun: OnAppRun);
        }

        private static void OnAppInstall(SemanticVersion version, IAppTools tools)
        {
            tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }

        private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
        {
            tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }

        private static void OnAppRun(SemanticVersion version, IAppTools tools, bool firstRun)
        {
            tools.SetProcessAppUserModelId();

            if (firstRun) 
                MessageBox.Show("Installed!");
        }
    }
}