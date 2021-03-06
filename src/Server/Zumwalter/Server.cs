﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RowAC
{
    internal class Listener
    {
        RLog R = null;
        public void StartListening()
        {
            R = RowacCore.R;
            R.Log("[Listener] Starting TCP listener...");
            TcpListener listener = new TcpListener(IPAddress.Any, 28165);
            listener.Start();

            while (true)
            {
                try
                {
                    var client = listener.AcceptSocket();
#if DEBUG
                    R.Log("[Listener] Connection accepted.");
#endif

                    var childSocketThread = new Thread(() =>
                    {
                        byte[] data = new byte[1048576]; // for screenshots and tasklists
                        int size = 0;
                        while (client.Available != 0)
                            size += client.Receive(data, size, 256, SocketFlags.None); // TODO: increase reading rate from 256?
                        client.Close();

                        string request = Encoding.ASCII.GetString(data, 0, size);
#if DEBUG
                        R.Log(string.Format("Received [{0}]: {1}", size, request));
#endif
                        ParseRequest(request);
                    });
                    childSocketThread.Start();
                }
                catch (Exception ex) { R.LogEx("ListenerLoop", ex); }
            }
        }

        enum Header
        {
            ID = 0,
            Ping = 1,
            Guid = 2,
            Tasklist = 3,
            Screenshot = 4
        }

        // TODO: fix id parsing
        // TODO: use guid
        private void ParseRequest(string data)
        {
#if !DEBUG
            data = Encoding.ASCII.GetString(Convert.FromBase64String(data));
            string[] split = data.Split('|'); // 0 - AES; 1 - data
            string AESData = RSA.Decrypt(split[0], RSA.privKey);
            string key = AESData.Split('|')[0];
            string iv = AESData.Split('|')[1];
            data = split[1]; // encryped client data
            data = AES.Decrypt(data, key, iv);
#endif
            string[] commands = data.Split('&'); // list of "header=arg"
            ulong ID = 0;
            string GUID = "";

            foreach (string command in commands)
            {
                try
                {
#if DEBUG
                    R.Log(command);
#endif
                    string[] segment = command.Split(new char[] { '=' }, 2);
                    if (segment.Length != 2) // header and argument
                        continue;
                    Header header = (Header)(int.Parse(segment[0]));
                    string argument = segment[1];

                    switch (header)
                    {
                        case Header.ID:
                            ID = ulong.Parse(argument);
                            break;

                        case Header.Ping:
                            ParsePing(argument, ID);
                            break;

                        case Header.Guid:
                            GUID = ParseGuid(argument, ID);
                            break;

                        case Header.Screenshot:
                            ParseScreenshot(argument, ID);
                            break;

                        case Header.Tasklist:
                            ParseTasklist(argument, ID);
                            break;

                        default:
                            R.Log(string.Format("[Header] Not valid header: {0}. Data: {1}", header, argument));
                            break;
                    }

                }
                catch (Exception ex) { R.LogEx("ParseRequest", string.Format("\"{0}\"; {1}", command, ex.ToString())); }
            }
        }
        
        // TODO: remove ping and guid on user disconnect
        private void ParsePing(string ping, ulong steamID)
        {
            try
            {
#if DEBUG
                R.Log("Ping: " + ping);
#endif

                string[] data = ping.Split('|'); // [1] - GUID; [2] - ShortTime
                string guid = data[1];

                //if (RowAnticheat.userGuids[steamID] == guid)
                //RustAPI.KickUser(RustAPI.FindByUserID(steamID), NetError.ApprovalDenied, true);

                RowacCore.pingTimeTable[steamID] = RowacCore.GetTimeInSeconds();
                R.Log(string.Format("[Ping] {0} ({1}) - {2}", steamID, guid, RowacCore.pingTimeTable[steamID]));
            }
            catch (Exception ex) { R.LogEx("Ping", ex); }
        }

        private string ParseGuid(string guid, ulong steamID)
        {
            try
            {
                R.Log(string.Format("[GUID] {0} New: {1}; Ping: {2}",
                    steamID, guid, (RowacCore.pingTimeTable.ContainsKey(steamID) ? RowacCore.pingTimeTable[steamID] : -1)));
                RowacCore.userGuids[steamID] = guid;
                return guid;
            }
            catch (Exception ex) { R.LogEx("Guid", ex); }
            return null;
        }

        private void ParseScreenshot(string imageBase64, ulong steamID)
        {
            try
            {
                byte[] image = Convert.FromBase64String(imageBase64);
                string CurrentUserACFolder = Path.Combine(RowacCore.screenshotsFolderPath, steamID.ToString());

                if (!Directory.Exists(CurrentUserACFolder))
                    Directory.CreateDirectory(CurrentUserACFolder);

                using (FileStream stream = new FileStream(Path.Combine(CurrentUserACFolder, DateTime.Now.ToString("yyyy MM dd HH-mm-ss") + ".jpg"), FileMode.CreateNew))
                using (BinaryWriter writer = new BinaryWriter(stream))
                    writer.Write(image);
            }
            catch (Exception ex) { R.LogEx("Screenshot", ex); }
        }

        private void ParseTasklist(string base64ProcessList, ulong steamID)
        {
            try
            {
                // TODO: better to save with guid?
                string CurrentUserACFolder = Path.Combine(RowacCore.taskListsFolderPath, steamID.ToString());

                if (!Directory.Exists(CurrentUserACFolder))
                    Directory.CreateDirectory(CurrentUserACFolder);

                using (StreamWriter writer = new StreamWriter(Path.Combine(CurrentUserACFolder,
                                                                DateTime.Now.ToString("yyyy MM dd HH-mm-ss") + ".txt")))
                    writer.WriteLine(Encoding.UTF8.GetString(Convert.FromBase64String(base64ProcessList)));
            }
            catch (Exception ex) { R.LogEx("Tasklist", ex); }
        }
    }
}
