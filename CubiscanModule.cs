using Manh.WMFW.Config.BL;
using Manh.WMFW.DataAccess;
using Manh.WMFW.General;
using Manh.WMW.Printing.General;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BHS_Cubiscan_Module
{
    public partial class CubiscanModule : Form
    {

        private Session _session = new Session();

        public CubiscanModule()
        {
            InitializeComponent();
        }

        private void btnSubmit_Click(object sender, EventArgs e)
        {
            string contID = txtContainerID.Text;

            try
            {
                int waveNum;
                DataTable intContNumsTable;

                //this sections gets the internal container numbers of the containers we want to get the labels for
                //
                intContNumsTable = GetContainerData(contID);
                
                waveNum = Convert.ToInt32(intContNumsTable.Rows[0]["Launch_Num"]);
                List<int> intContNums = new List<int>();

                foreach (DataRow row in intContNumsTable.Rows)
                {
                    intContNums.Add(Convert.ToInt32(row["Internal_Container_Num"]));
                }

                PushZPLtoDatabase(waveNum, intContNums);

                txtContainerID.Text = "";
                txtContainerID.Focus();
            }
            catch (Exception ex)
            {
                WriteDebug(ex.ToString());
            }
        }

        private DataTable GetContainerData(string contID)
        {
            DataTable values = null;
            using (DataHelper dataHelper = new DataHelper((ISession)this._session))
            {
                IDataParameter[] parmarray = new IDataParameter[] {
                     dataHelper.BuildParameter("@Container_Id", contID)
                };

                values = dataHelper.GetTable(CommandType.StoredProcedure, "BHS_EXP_ReleaseWaveAfter", parmarray);
            }

            if (values == null)
            {
                WriteDebug("NO DATA RETURNED FOR CONTAINER ID " + contID);
                MessageBox.Show("Container ID did not return any values");
            }

            return values;
        }

        private void PushZPLtoDatabase(int waveNum, List<int> intContNums)
        {
            string waveLabelsDirectory = GetWaveLabelDirectory();

            string wldPath;
            bool waveLabelFilesExist = WaveLabelFilesExist(waveNum, waveLabelsDirectory, out wldPath);
            WriteDebug("waveLabelFilesExist: " + waveLabelFilesExist);

            if (waveLabelFilesExist)
            {
                WaveLabelIndex waveLabelIndex = LoadWaveLabelIndex(waveNum, waveLabelsDirectory);

                using (FileStream wldFile = File.OpenRead(wldPath))
                {
                    foreach (WaveLabelPrintDevice printDevice in waveLabelIndex.PrintDevices)
                    {
                        foreach (WaveLabelEntry label in printDevice.Entries)
                        {
                            int internalContainerNum = label.InternalContainerNum;

                            if (intContNums.IndexOf(internalContainerNum) != -1)
                            {
                                //if (documentType == "160") elegler 11/20/2019 removed for other shipping labels to be produced
                                //{ 
                                byte[] buffer = GetLabelData(label, wldFile);
                                //string stFileData = BitConverter.ToString(buffer);
                                //char[] charBuffer = new char[buffer.Length / sizeof(char)];
                                //System.Buffer.BlockCopy(buffer, 0, charBuffer, 0, buffer.Length / sizeof(char));
                                //string stFileData = new string(charBuffer);
                                string stFileData = Encoding.UTF8.GetString(buffer);
                                WriteDebug(stFileData);

                                if (!String.IsNullOrEmpty(stFileData))
                                {
                                    using (DataHelper dataHelper = new DataHelper((ISession)this._session))
                                    {
                                        IDataParameter[] parmarray = new IDataParameter[] {
                                        dataHelper.BuildParameter("@RAWDATA", stFileData),
                                        dataHelper.BuildParameter("@IntContNum", internalContainerNum)
                                    };

                                        var response = dataHelper.Insert(CommandType.StoredProcedure, "BHS_WaveLabels", parmarray);
                                        WriteDebug(string.Format("Result - {0}", response.ToString()));
                                    }
                                }
                            }

                        }
                    }
                }
            }
        }

        private string GetWaveLabelDirectory()
        {
            string documentFolder = SystemConfigRetrieval.GetStringSystemValue((ISession)this._session, "170", "Technical");
            string path = FileManager.AddBackSlash(documentFolder) + "WaveLabels\\";
            WriteDebug("WaveLabels : " + path);

            if (!Directory.Exists(path))
            {
                throw new Exception(String.Format("WaveLabels directory {0} does not exist.", path));
                //Directory.CreateDirectory(path);
            }

            return path;
        }

        private WaveLabelIndex LoadWaveLabelIndex(int waveNum, string waveLabelsDirectory)
        {
            return (WaveLabelIndex)XMLDocManager.Deserialize(typeof(WaveLabelIndex), FileManager.Read(waveLabelsDirectory + (object)waveNum + ".wli"));
        }

        private bool WaveLabelFilesExist(int waveNum, string waveLabelsDirectory, out string wldPath)
        {
            string path1 = waveLabelsDirectory + (object)waveNum + ".wli";
            string path2 = waveLabelsDirectory + (object)waveNum + ".wld";
            wldPath = path2;
            WriteDebug("wldPath: " + path2);

            if (File.Exists(path1))
            {
                return File.Exists(path2);
            }
            return false;
        }

        private byte[] GetLabelData(WaveLabelEntry label, FileStream wldFile)
        {
            switch (label.Type)
            {
                case WaveLabelType.BreakLabel:
                    return (byte[])null; // BHS Added
                case WaveLabelType.Label:
                    wldFile.Position = label.LabelStart;
                    int count = (int)(label.LabelEnd - label.LabelStart + 1L);
                    int byteCount = count * sizeof(byte);

                    WriteDebug(String.Format("Starting Position - {0}", wldFile.Position));

                    byte[] buffer = new byte[count];
                    wldFile.Read(buffer, 0, count);
                    return buffer;
                case WaveLabelType.Error:
                    return (byte[])null;
                default:
                    return (byte[])null;
            }
        }

        private void WriteDebug(string text, string member = "", int line = 0)
        {
        Debug.WriteLine(string.Format("{0} : {1} : {2} : {3}", this.GetType().FullName, member, line, text));
        }
    }
}
