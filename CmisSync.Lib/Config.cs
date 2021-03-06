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
//   along with this program.  If not, see <http://www.gnu.org/licenses/>.


using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

namespace CmisSync.Lib
{

    public class Config
    {
        private XmlDocument configXml = new XmlDocument();
        public string FullPath;
        public string TmpPath;
        // public string LogFilePath;
        private string configpath;

        public string ConfigPath { get { return configpath; } }

        public string HomePath
        {
            get
            {
                if (Backend.Platform == PlatformID.Win32NT)
                    return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                else
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            }
        }


        public string FoldersPath
        {
            get
            {
                return Path.Combine(HomePath, "CmisSync");
            }
        }

        public bool DebugMode
        {
            get
            {
                try
                {
                    XmlNode debugNode = configXml.SelectSingleNode(@"/CmisSync/debug");
                    bool debug = false;
                    bool.TryParse(debugNode.InnerText, out debug);
                    return debug;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Config(string config_path, string config_file_name)
        {
            configpath = config_path;
            FullPath = Path.Combine(config_path, config_file_name);
            Console.WriteLine("FullPath:" + FullPath);
            //LogFilePath = Path.Combine(config_path, "debug_log.txt");

            //if (File.Exists(LogFilePath))
            //{
            //    try
            //    {
            //        File.Delete(LogFilePath);

            //    }
            //    catch (Exception)
            //    {
            //        // Don't delete the debug.log if, for example, 'tail' is reading it
            //    }
            //}

            if (!Directory.Exists(config_path))
                Directory.CreateDirectory(config_path);

            if (!File.Exists(FullPath))
                CreateInitialConfig();

            try
            {
                configXml.Load(FullPath);
            }
            catch (TypeInitializationException)
            {
                CreateInitialConfig();
            }
            catch (FileNotFoundException)
            {
                CreateInitialConfig();
            }
            catch (XmlException)
            {
                FileInfo file = new FileInfo(FullPath);

                if (file.Length == 0)
                {
                    File.Delete(FullPath);
                    CreateInitialConfig();
                }
                else
                {
                    throw new XmlException(FullPath + " does not contain a valid config XML structure.");
                }

            }
            finally
            {
                configXml.Load(FullPath);
                //TmpPath = Path.Combine (FoldersPath, ".tmp");
                //Directory.CreateDirectory (TmpPath);
            }
        }


        private void CreateInitialConfig()
        {
            string user_name = "Unknown";

            if (Backend.Platform == PlatformID.Unix ||
                Backend.Platform == PlatformID.MacOSX)
            {

                user_name = Environment.UserName;
                if (string.IsNullOrEmpty(user_name))
                    user_name = "";
                else
                    user_name = user_name.TrimEnd(",".ToCharArray());

            }
            else
            {
                user_name = Environment.UserName;
            }

            if (string.IsNullOrEmpty(user_name))
                user_name = "Unknown";

            configXml.AppendChild(configXml.CreateXmlDeclaration("1.0", "UTF-8", "yes"));
            configXml.AppendChild(configXml.CreateElement("CmisSync"));
            XmlNode user = configXml.CreateElement("user");
            XmlNode username = configXml.CreateElement("name");
            username.InnerText = user_name;
            XmlNode email = configXml.CreateElement("email");
            email.InnerText = "Unknown";

            user.AppendChild(username);
            user.AppendChild(email);

            XmlNode folders = configXml.CreateElement("folders");
            configXml.DocumentElement.AppendChild(user);
            CreateLog4NetDefaultConfig();
            configXml.DocumentElement.AppendChild(folders);
            Save();
        }

        public XmlElement GetLog4NetConfig()
        {
            return (XmlElement)configXml.SelectSingleNode("/CmisSync/log4net");
        }

        private void CreateLog4NetDefaultConfig()
        {
            XmlNode log4net = configXml.CreateElement("log4net");

            XmlNode appender = configXml.CreateElement("appender");
            XmlAttribute name = configXml.CreateAttribute("name");
            name.Value = "CmisSyncFileAppender";
            appender.Attributes.Append(name);

            XmlAttribute type = configXml.CreateAttribute("type");
            type.Value = "log4net.Appender.RollingFileAppender";
            appender.Attributes.Append(type);

            XmlNode file = configXml.CreateElement("file");
            XmlAttribute filevalue = configXml.CreateAttribute("value");
            filevalue.Value = Path.Combine(configpath, "debug_log.txt").ToString();
            file.Attributes.Append(filevalue);
            appender.AppendChild(file);

            XmlNode appendToFile = configXml.CreateElement("appendToFile");
            XmlAttribute appendtofileValue = configXml.CreateAttribute("value");
            appendtofileValue.Value = "true";
            appendToFile.Attributes.Append(appendtofileValue);
            appender.AppendChild(appendToFile);

            XmlNode rollingStyle = configXml.CreateElement("rollingStyle");
            XmlAttribute rollingStyleValue = configXml.CreateAttribute("value");
            rollingStyleValue.Value = "Size";
            rollingStyle.Attributes.Append(rollingStyleValue);
            appender.AppendChild(rollingStyle);

            XmlNode maxSizeRollBackups = configXml.CreateElement("maxSizeRollBackups");
            XmlAttribute maxSizeRollBackupsValue = configXml.CreateAttribute("value");
            maxSizeRollBackupsValue.Value = "10";
            maxSizeRollBackups.Attributes.Append(maxSizeRollBackupsValue);
            appender.AppendChild(maxSizeRollBackups);

            XmlNode maximumFileSize = configXml.CreateElement("maximumFileSize");
            XmlAttribute maximumFileSizeValue = configXml.CreateAttribute("value");
            maximumFileSizeValue.Value = "5MB";
            maximumFileSize.Attributes.Append(maximumFileSizeValue);
            appender.AppendChild(maximumFileSize);

            XmlNode staticLogFileName = configXml.CreateElement("staticLogFileName");
            XmlAttribute staticLogFileNameValue = configXml.CreateAttribute("value");
            staticLogFileNameValue.Value = "true";
            staticLogFileName.Attributes.Append(staticLogFileNameValue);
            appender.AppendChild(staticLogFileName);

            XmlNode layout = configXml.CreateElement("layout");
            XmlAttribute layouttype = configXml.CreateAttribute("type");
            layouttype.Value = "log4net.Layout.PatternLayout";
            layout.Attributes.Append(layouttype);

            XmlNode conversionPattern = configXml.CreateElement("conversionPattern");
            XmlAttribute conversionPatternValue = configXml.CreateAttribute("value");
            conversionPatternValue.Value = "%date [%thread] %-5level %logger [%property{NDC}] - %message%newline";
            conversionPattern.Attributes.Append(conversionPatternValue);
            layout.AppendChild(conversionPattern);
            appender.AppendChild(layout);
            log4net.AppendChild(appender);

            XmlNode root = configXml.CreateElement("root");
            XmlNode level = configXml.CreateElement("level");
            XmlAttribute levelvalue = configXml.CreateAttribute("value");
            levelvalue.Value = "DEBUG";
            level.Attributes.Append(levelvalue);
            root.AppendChild(level);

            XmlNode appenderref = configXml.CreateElement("appender-ref");
            XmlAttribute appenderrefvalue = configXml.CreateAttribute("ref");
            appenderrefvalue.Value = "CmisSyncFileAppender";
            appenderref.Attributes.Append(appenderrefvalue);
            root.AppendChild(appenderref);

            log4net.AppendChild(root);

            configXml.DocumentElement.AppendChild(log4net);
        }


        public User User
        {
            get
            {
                XmlNode name_node = configXml.SelectSingleNode("/CmisSync/user/name/text()");
                string name = name_node.Value;

                XmlNode email_node = configXml.SelectSingleNode("/CmisSync/user/email/text()");
                string email = email_node.Value;

                string pubkey_file_path = Path.Combine(
                    Path.GetDirectoryName(FullPath), "CmisSync." + email + ".key.pub");

                User user = new User(name, email);

                if (File.Exists(pubkey_file_path))
                    user.PublicKey = File.ReadAllText(pubkey_file_path);

                return user;
            }

            set
            {
                User user = (User)value;

                XmlNode name_node = configXml.SelectSingleNode("/CmisSync/user/name/text()");
                name_node.InnerText = user.Name;

                XmlNode email_node = configXml.SelectSingleNode("/CmisSync/user/email/text()");
                email_node.InnerText = user.Email;

                Save();
            }
        }


        public List<string> Folders
        {
            get
            {
                List<string> folders = new List<string>();

                foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
                    folders.Add(node_folder["name"].InnerText);

                return folders;
            }
        }

        public void AddFolder(RepoInfo repoInfo)
        {
            this.AddFolder(repoInfo.Name, repoInfo.TargetDirectory, repoInfo.Identifier, repoInfo.Address.ToString(), repoInfo.Backend, repoInfo.RepoID, repoInfo.RemotePath, repoInfo.User, repoInfo.Password, repoInfo.PollInterval);
        }

        public void AddFolder(string name, string path, string identifier, string url, string backend,
            string repository, string remoteFolder, string user, string password, double pollinterval)
        {
            XmlNode node_name = configXml.CreateElement("name");
            XmlNode node_path = configXml.CreateElement("path");
            XmlNode node_identifier = configXml.CreateElement("identifier");
            XmlNode node_url = configXml.CreateElement("url");
            XmlNode node_backend = configXml.CreateElement("backend");
            XmlNode node_repository = configXml.CreateElement("repository");
            XmlNode node_remoteFolder = configXml.CreateElement("remoteFolder");
            XmlNode node_user = configXml.CreateElement("user");
            XmlNode node_password = configXml.CreateElement("password");
            XmlNode node_pollinterval = configXml.CreateElement("pollinterval");

            node_name.InnerText = name;
            node_path.InnerText = path;
            node_identifier.InnerText = identifier;
            node_url.InnerText = url;
            node_backend.InnerText = backend;
            node_repository.InnerText = repository;
            node_remoteFolder.InnerText = remoteFolder;
            node_user.InnerText = user;
            node_password.InnerText = password;
            node_pollinterval.InnerText = pollinterval.ToString();

            XmlNode node_folder = configXml.CreateNode(XmlNodeType.Element, "folder", null);

            node_folder.AppendChild(node_name);
            node_folder.AppendChild(node_path);
            node_folder.AppendChild(node_identifier);
            node_folder.AppendChild(node_url);
            node_folder.AppendChild(node_backend);
            node_folder.AppendChild(node_repository);
            node_folder.AppendChild(node_remoteFolder);
            node_folder.AppendChild(node_user);
            node_folder.AppendChild(node_password);
            node_folder.AppendChild(node_pollinterval);

            XmlNode node_root = configXml.SelectSingleNode("/CmisSync/folders");
            node_root.AppendChild(node_folder);

            Save();
        }


        public void RemoveFolder(string name)
        {
            foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
            {
                if (node_folder["name"].InnerText.Equals(name))
                    configXml.SelectSingleNode("/CmisSync/folders").RemoveChild(node_folder);
            }

            Save();
        }


        public void RenameFolder(string identifier, string name)
        {
            XmlNode node_folder = configXml.SelectSingleNode(
                string.Format("/CmisSync/folders/folder[identifier=\"{0}\"]", identifier));

            node_folder["name"].InnerText = name;
            Save();
        }


        public string GetBackendForFolder(string name)
        {
            return "Cmis"; // TODO GetFolderValue (name, "backend");
        }


        public string GetIdentifierForFolder(string name)
        {
            return GetFolderValue(name, "identifier");
        }


        public string GetUrlForFolder(string name)
        {
            return GetFolderValue(name, "url");
        }


        public bool IdentifierExists(string identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException();

            foreach (XmlNode node_folder in configXml.SelectNodes("/CmisSync/folders/folder"))
            {
                XmlElement folder_id = node_folder["identifier"];

                if (folder_id != null && identifier.Equals(folder_id.InnerText))
                    return true;
            }

            return false;
        }


        public bool SetFolderOptionalAttribute(string folder_name, string key, string value)
        {
            XmlNode folder = GetFolder(folder_name);

            if (folder == null)
                return false;

            if (folder[key] != null)
            {
                folder[key].InnerText = value;

            }
            else
            {
                XmlNode new_node = configXml.CreateElement(key);
                new_node.InnerText = value;
                folder.AppendChild(new_node);
            }

            Save();

            return true;
        }


        public string GetFolderOptionalAttribute(string folder_name, string key)
        {
            XmlNode folder = GetFolder(folder_name);

            if (folder != null)
            {
                if (folder[key] != null)
                    return folder[key].InnerText;
                else
                    return null;

            }
            else
            {
                return null;
            }
        }

        public RepoInfo GetRepoInfo(string FolderName)
        {
            RepoInfo repoInfo = new RepoInfo(FolderName, ConfigPath);

            repoInfo.User = GetFolderOptionalAttribute(FolderName, "user");
            repoInfo.Password = GetFolderOptionalAttribute(FolderName, "password");
            repoInfo.Address = new Uri(GetUrlForFolder(FolderName));
            repoInfo.RepoID = GetFolderOptionalAttribute(FolderName, "repository");
            repoInfo.RemotePath = GetFolderOptionalAttribute(FolderName, "remoteFolder");
            repoInfo.TargetDirectory = GetFolderOptionalAttribute(FolderName, "path");
            
            double pollinterval = 0;
            double.TryParse(GetFolderOptionalAttribute(FolderName, "pollinterval"), out pollinterval);
            if (pollinterval == 0) pollinterval = 5000;
            repoInfo.PollInterval = pollinterval;

            if (String.IsNullOrEmpty(repoInfo.TargetDirectory))
            {
                repoInfo.TargetDirectory = Path.Combine(FoldersPath, FolderName);
            }

            return repoInfo;
        }

        private XmlNode GetFolder(string name)
        {
            return configXml.SelectSingleNode(string.Format("/CmisSync/folders/folder[name=\"{0}\"]", name));
        }


        private string GetFolderValue(string name, string key)
        {
            XmlNode folder = GetFolder(name);

            if ((folder != null) && (folder[key] != null))
                return folder[key].InnerText;
            else
                return null;
        }


        public string GetConfigOption(string name)
        {
            XmlNode node = configXml.SelectSingleNode("/CmisSync/" + name);

            if (node != null)
                return node.InnerText;
            else
                return null;
        }


        public void SetConfigOption(string name, string content)
        {
            XmlNode node = configXml.SelectSingleNode("/CmisSync/" + name);

            if (node != null)
            {
                node.InnerText = content;

            }
            else
            {
                node = configXml.CreateElement(name);
                node.InnerText = content;

                XmlNode node_root = configXml.SelectSingleNode("/CmisSync");
                node_root.AppendChild(node);
            }

            Save();
        }


        private void Save()
        {
            //if (!File.Exists(FullPath))
            //    throw new FileNotFoundException(FullPath + " does not exist");

            configXml.Save(FullPath);
        }
    }
}
