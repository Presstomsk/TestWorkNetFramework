using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TestWorkNetFramework
{
    public partial class Form1 : Form
    {

        ManualResetEvent _event = new ManualResetEvent(true);
        private DataTable _dt = new DataTable();
        private Thread _thread;
        private Thread _InfoThread;
        private Logger _serilog;

        public Form1()
        {
            try
            {
                InitializeComponent();
                CenterToScreen();

                button1.BackColor = Color.Red;
                button1.Text = "Stop process update";

                this.Text = "Диспетчер задач";

                FormClosing += Form1_FormClosing;

                var appSettings = ConfigurationManager.AppSettings;
                string logFilePath = appSettings["LogFilePath"];
                var delay = Convert.ToInt32(appSettings["ProcessListUpdateTimeSpan"]);

                _serilog = new LoggerConfiguration()
                                  .MinimumLevel.Debug()
                                  .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                                  .CreateLogger();

                _serilog.Information("Application start");

                dataGridView1.RowHeadersVisible = false;
                _dt.Columns.Add("ProcessId", typeof(int));
                _dt.Columns.Add("ProcessName", typeof(string));
                _dt.Columns.Add("Memory, Mb", typeof(double));

                _thread = new Thread(() =>
                {
                    try
                    {
                        do
                        {
                            _event.WaitOne();
                            _dt = GetActualData(_dt);

                            dataGridView1.Invoke((MethodInvoker)delegate
                            {
                                dataGridView1.DataSource = null;
                                dataGridView1.DataSource = _dt;
                                dataGridView1.Columns[0].Width = 100;
                                dataGridView1.Columns[1].Width = 555;
                                dataGridView1.Columns[2].Width = 100;
                            });
                            Thread.Sleep(delay);
                        } while (true);
                    }
                    catch (ThreadAbortException)
                    {

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{ex}");
                        _serilog.Information($"{ex}");
                    }
                });              

                _thread.Start();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"{ex}");
                _serilog.Information($"{ex}");
            }
        }
     
      
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var result = Convert.ToInt32((sender as DataGridView).Rows[e.RowIndex].Cells[0].FormattedValue);

            if (_InfoThread == default || !_InfoThread.IsAlive)
            {
                _InfoThread = new Thread(() =>
                {
                    GetProcessInfo(result);
                });

                _InfoThread.Start();
            }     
        }

        private DataTable GetActualData(DataTable dt)
        {
            try
            {
                Process[] localAll = Process.GetProcesses().OrderByDescending(x => x.PagedMemorySize64).ToArray();

                CheckProcesses(localAll, dt);

                dt.Rows.Clear();

                foreach (Process instance in localAll)
                {
                    dt.Rows.Add(new Object[] { instance.Id, instance.ProcessName, Math.Round((double)instance.PagedMemorySize64 / 1000000, 2) });
                }

                return dt;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void CheckProcesses(Process[] localAll, DataTable dt)
        {
            try
            {
                var processesList = new List<string>();

                var addedProcesses = localAll.Select(x => x.Id).Except(dt.Select().Select(x => (int)x[0]))
                                                             .Join(localAll,
                                                                 a => a,
                                                                 b => b.Id,
                                                                 (a, b) => b)
                                                             .ToList();
                if (addedProcesses.Count() > 0)
                {
                    processesList.Clear();
                    addedProcesses.ForEach(x => processesList.Add($"Id: {x.Id} - ProcessName: {x.ProcessName}"));
                    var str = string.Join(",", processesList);
                    _serilog.Information($"Новые процессы в списке : {str}");
                }

                var deletedProcesses = dt.Select().Select(x => (int)x[0]).Except(localAll.Select(x => x.Id))
                                                            .Join(dt.Select(),
                                                                a => a,
                                                                b => b[0],
                                                                (a, b) => b)
                                                            .ToList();

                if (deletedProcesses.Count() > 0)
                {
                    processesList.Clear();
                    deletedProcesses.ForEach(x => processesList.Add($"Id: {x[0]} - ProcessName: {x[1]}"));
                    var str = string.Join(",", processesList);
                    _serilog.Information($"Удаленные процессы из списка : {str}");
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        private void GetProcessInfo(int id)
        {
            try
            {
                var process = Process.GetProcessById(id);

                var processId = $"Id: {process.Id}\n\n";
                var handle = $"Handle: {process.Handle}\n\n";
                var processName = $"ProcessName: {process.ProcessName}\n\n";
                var startTime = $"StartTime: {process.StartTime}\n\n";
                var pathOfCurrentProcess = $"PathOfCurrentProcess: {process.MainModule.FileName}\n\n";
                MessageBox.Show($"{processId}{handle}{processName}{pathOfCurrentProcess}{startTime}", "ProcessInfo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _serilog.Information($"Запрос пользователем информации о процессе Id: {process.Id} - ProcessName: {process.ProcessName}");
            }
            catch(ArgumentException argEx)           
            {
                MessageBox.Show($"{argEx.Message}\n Дождитесь обновления списка процессов.", "ProcessInfo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _serilog.Information($"{argEx.Message}");
            }
            catch(Exception ex)
            {
                MessageBox.Show($"{ex}");
                _serilog.Information($"{ex}");
            }
        }

        private void Form1_FormClosing(Object sender, FormClosingEventArgs e)
        {
            if (_thread.IsAlive)
            {
                _thread.Abort();
            }            
            _serilog.Information("Application end");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Stop process update")
            {
                _event.Reset();
                button1.BackColor = Color.Green;
                button1.Text = "Start process update";
                _serilog.Information("Process update stopped");
            }
            else if (button1.Text == "Start process update")
            {
                _event.Set();
                button1.BackColor = Color.Red;
                button1.Text = "Stop process update";
                _serilog.Information("Process update started");
            }
        }
    }
}
