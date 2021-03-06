//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;

using Timers = System.Timers;

namespace CmisSync.Lib
{

    public enum SyncStatus
    {
        Idle,
        SyncUp,
        SyncDown,
        Error,
        Suspend
    }


    public abstract class RepoBase
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(RepoBase));

        public abstract void HasUnsyncedChanges();
        public abstract double Size { get; }

        public event SyncStatusChangedEventHandler SyncStatusChanged = delegate { };
        public delegate void SyncStatusChangedEventHandler(SyncStatus new_status);

        public event ProgressChangedEventHandler ProgressChanged = delegate { };
        public delegate void ProgressChangedEventHandler(double percentage, string speed);

        public event NewChangeSetEventHandler NewChangeSet = delegate { };
        public delegate void NewChangeSetEventHandler(ChangeSet change_set);

        public event Action ConflictResolved = delegate { };
        public event Action ChangesDetected = delegate { };


        public readonly string LocalPath;
        public readonly string Name;
        public readonly Uri RemoteUrl;
        public List<ChangeSet> ChangeSets { get; protected set; }
        public SyncStatus Status { get; private set; }
        public bool ServerOnline { get; private set; }
        public bool IsBuffering { get; private set; }
        public double ProgressPercentage { get; private set; }
        public string ProgressSpeed { get; private set; }

        public string Identifier
        {
            get
            {
                if (this.identifier != null)
                    return this.identifier;

                string id_path = Path.Combine(LocalPath, ".CmisSync");

                if (File.Exists(id_path))
                    this.identifier = File.ReadAllText(id_path).Trim();

                if (!string.IsNullOrEmpty(this.identifier))
                {
                    return this.identifier;

                }
                else
                {
                    string config_identifier = this.local_repoInfo.Identifier;

                    if (!string.IsNullOrEmpty(config_identifier))
                        this.identifier = config_identifier;
                    else
                        this.identifier = FetcherBase.CreateIdentifier();

                    // File.WriteAllText (id_path, this.identifier);
                    // File.SetAttributes (id_path, FileAttributes.Hidden);

                    Logger.Info("Local | " + Name + " | Assigned identifier: " + this.identifier);

                    return this.identifier;
                }
            }
        }

        public virtual string[] UnsyncedFilePaths
        {
            get
            {
                return new string[0];
            }
        }

        public void Resume()
        {
            Status = SyncStatus.Idle;
        }

        public void Suspend()
        {
            Status = SyncStatus.Suspend;
        }

        protected RepoInfo local_repoInfo;


        private string identifier;
        private ListenerBase listener;
        private Watcher watcher;
        private TimeSpan poll_interval = PollInterval.Short;
        private DateTime last_poll = DateTime.Now;
        private DateTime progress_last_change = DateTime.Now;
        private TimeSpan progress_change_interval = new TimeSpan(0, 0, 0, 1);
        // private Timers.Timer remote_timer = new Timers.Timer() { Interval = 5000 };
        private Timers.Timer remote_timer = new Timers.Timer();

        private bool is_syncing
        {
            get
            {
                return (Status == SyncStatus.SyncUp || Status == SyncStatus.SyncDown || IsBuffering);
            }
        }

        private static class PollInterval
        {
            public static readonly TimeSpan Short = new TimeSpan(0, 0, 5, 0);
            public static readonly TimeSpan Long = new TimeSpan(0, 0, 15, 0);
        }


        public RepoBase(RepoInfo repoInfo)
        {
            this.local_repoInfo = repoInfo;
            LocalPath = repoInfo.TargetDirectory;
            Name = Path.GetFileName(LocalPath);
            RemoteUrl = repoInfo.Address;
            IsBuffering = false;
            ServerOnline = true;
            this.identifier = Identifier;

            Logger.Info(String.Format("Repo [{0}] - Set poll interval to {1} ms", repoInfo.Name, repoInfo.PollInterval));
            this.remote_timer.Interval = repoInfo.PollInterval;

            SyncStatusChanged += delegate(SyncStatus status)
            {
                Status = status;
            };

            this.watcher = new Watcher(LocalPath);

            this.remote_timer.Elapsed += delegate
            {
                if (this.is_syncing || IsBuffering)
                    return;

                int time_comparison = DateTime.Compare(this.last_poll, DateTime.Now.Subtract(this.poll_interval));
                bool time_to_poll = (time_comparison < 0);

                if (time_to_poll && !is_syncing)
                {
                    this.last_poll = DateTime.Now;

                }

                // In the unlikely case that we haven't synced up our
                // changes or the server was down, sync up again
                HasUnsyncedChanges();
            };
        }


        public void Initialize()
        {
            this.watcher.ChangeEvent += OnFileActivity;

            // Sync up everything that changed
            // since we've been offline
            HasUnsyncedChanges();

            this.remote_timer.Start();
        }


        public void OnFileActivity(FileSystemEventArgs args)
        {
            if (IsBuffering)
                return;

            ChangesDetected();
            string relative_path = args.FullPath.Replace(LocalPath, "");

            IsBuffering = true;
            this.watcher.Disable();

            Logger.Info("Local | " + Name + " | Activity detected, waiting for it to settle...");

            List<double> size_buffer = new List<double>();

            do
            {
                if (size_buffer.Count >= 4)
                    size_buffer.RemoveAt(0);

                DirectoryInfo info = new DirectoryInfo(LocalPath);
                size_buffer.Add(CalculateSize(info));

                if (size_buffer.Count >= 4 &&
                    size_buffer[0].Equals(size_buffer[1]) &&
                    size_buffer[1].Equals(size_buffer[2]) &&
                    size_buffer[2].Equals(size_buffer[3]))
                {

                    Logger.Info("Local | " + Name + " | Activity has settled");
                    IsBuffering = false;
                }
                else
                {
                    Thread.Sleep(500);
                }

            } while (IsBuffering);

            this.watcher.Enable();
        }


        protected internal void OnConflictResolved()
        {
            ConflictResolved();
        }


        protected void OnProgressChanged(double progress_percentage, string progress_speed)
        {
            // Only trigger the ProgressChanged event once per second
            if (DateTime.Compare(this.progress_last_change, DateTime.Now.Subtract(this.progress_change_interval)) >= 0)
                return;

            if (progress_percentage == 100.0)
                progress_percentage = 99.0;

            ProgressPercentage = progress_percentage;
            ProgressSpeed = progress_speed;
            this.progress_last_change = DateTime.Now;

            ProgressChanged(progress_percentage, progress_speed);
        }


        // Recursively gets a folder's size in bytes
        private double CalculateSize(DirectoryInfo parent)
        {
            if (!Directory.Exists(parent.ToString()))
                return 0;

            double size = 0;

            try
            {
                foreach (FileInfo file in parent.GetFiles())
                {
                    if (!file.Exists)
                        return 0;

                    size += file.Length;
                }

                foreach (DirectoryInfo directory in parent.GetDirectories())
                    size += CalculateSize(directory);

            }
            catch (Exception)
            {
                return 0;
            }

            return size;
        }


        public void Dispose()
        {
            this.remote_timer.Stop();
            this.remote_timer.Dispose();

            this.watcher.Dispose();
        }
    }
}
