using SharpPcap;
using SharpPcap.Packets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace RiftShark
{
    public partial class MainForm : Form
    {
        private bool mClosing = false;

        public MainForm(string[] pArgs)
        {
            InitializeComponent();
            Text = "RiftShark " + Program.AssemblyVersion;
            foreach (string fileName in pArgs) OpenFile(fileName);
        }

        private void MainForm_FormClosing(object pSender, FormClosingEventArgs pArgs)
        {
            mClosing = true;
        }

        private SessionForm NewSession()
        {
            SessionForm session = new SessionForm();
            session.Show(mDockPanel, DockState.Document);
            return session;
        }

        private void mFileImportMenu_Click(object pSender, EventArgs pArgs)
        {
            if (mImportKEYSDialog.ShowDialog(this) != DialogResult.OK) return;
            byte[] characterKey = null;
            byte[] worldKey = null;
            using (BinaryReader reader = new BinaryReader(new FileStream(mImportKEYSDialog.FileName, FileMode.Open)))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    bool unknown = false;
                    switch (reader.ReadByte())
                    {
                        case 0x00:
                            characterKey = reader.ReadBytes(128);
                            break;
                        case 0x01:
                            worldKey = reader.ReadBytes(128);
                            break;
                        default: unknown = true; break;
                    }
                    if (unknown) break;
                }
            }
            if (characterKey == null && worldKey == null)
            {
                MessageBox.Show(this, "You have selected an invalid keys file.", "Invalid Keys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (characterKey != null) Array.Reverse(characterKey);
            if (worldKey != null) Array.Reverse(worldKey);
            Dictionary<bool, byte[]> keys = new Dictionary<bool, byte[]>();
            keys[true] = characterKey;
            keys[false] = worldKey;

            if (mImportPCAPDialog.ShowDialog(this) != DialogResult.OK) return;
            string pcapFileName = mImportPCAPDialog.FileName;
            PcapOfflineDevice device = new PcapOfflineDevice(pcapFileName);
            device.Open();

            Packet packet = null;
            SessionForm session = null;
            Dictionary<SessionForm, int> totalSessionTCPPackets = new Dictionary<SessionForm, int>();
            HashSet<SessionForm> terminatedSessions = new HashSet<SessionForm>();
            while ((packet = device.GetNextPacket()) != null)
            {
                TCPPacket tcpPacket = packet as TCPPacket;
                if (tcpPacket == null) continue;
                session = Array.Find(MdiChildren, f => (f as SessionForm).MatchTCPPacket(tcpPacket)) as SessionForm;
                if (session == null && tcpPacket.Syn && !tcpPacket.Ack)
                {
                    session = NewSession();
                    session.ImportClientPrivateKeys(keys);
                    session.BufferTCPPacket(tcpPacket);
                    totalSessionTCPPackets[session] = 0;
                    Application.DoEvents();
                }
                if (session != null && !terminatedSessions.Contains(session)) totalSessionTCPPackets[session] += 1;
                if (session != null && (tcpPacket.Fin || tcpPacket.Rst)) terminatedSessions.Add(session);
            }
            device.Close();
            

            device = new PcapOfflineDevice(pcapFileName);
            device.Open();

            Dictionary<SessionForm, int> processedSessionTCPPackets = new Dictionary<SessionForm, int>();
            Dictionary<SessionForm, int> processedSessionPercents = new Dictionary<SessionForm, int>();
            while ((packet = device.GetNextPacket()) != null)
            {
                if (mClosing) break;
                TCPPacket tcpPacket = packet as TCPPacket;
                if (tcpPacket == null) continue;
                session = Array.Find(MdiChildren, f => (f as SessionForm).MatchTCPPacket(tcpPacket)) as SessionForm;
                if (session != null)
                {
                    if (mDockPanel.ActiveDocument != session) session.Activate();
                    if (!processedSessionTCPPackets.ContainsKey(session)) processedSessionTCPPackets[session] = 0;
                    if (!processedSessionPercents.ContainsKey(session)) processedSessionPercents[session] = 0;
                    processedSessionTCPPackets[session] += 1;
                    session.BufferTCPPacket(tcpPacket);

                    int newPercent = (processedSessionTCPPackets[session] * 100) / totalSessionTCPPackets[session];
                    while (newPercent > processedSessionPercents[session])
                    {
                        processedSessionPercents[session] += 1;
                        session.ProgressUpdate();
                    }
                }
            }
        }

        private void mFileOpenMenu_Click(object pSender, EventArgs pArgs)
        {
            if (mOpenDialog.ShowDialog(this) == DialogResult.OK) foreach (string fileName in mOpenDialog.FileNames) OpenFile(fileName);
        }
        private void OpenFile(string pFileName)
        {
            SessionForm session = NewSession();
            session.OpenFile(pFileName);
            GC.Collect();
        }

        private void mFileQuit_Click(object pSender, EventArgs pArgs)
        {
            Close();
        }
    }
}
