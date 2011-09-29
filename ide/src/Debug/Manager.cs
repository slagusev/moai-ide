﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using MOAI.Tools;
using MOAI.Debug.Messages;
using MOAI.Designers;

namespace MOAI.Debug
{
    public class Manager
    {
        private MOAI.Manager p_Parent = null;
        private OutputTool m_OutputTool = null;
        private bool p_Paused = false;

        private Process m_Process = null;
        private Communicator m_Communicator = null;

        public event EventHandler DebugStart;
        public event EventHandler DebugPause;
        public event EventHandler DebugContinue;
        public event EventHandler DebugStop;

        /// <summary>
        /// Creates a new Manager class for managing debugging.
        /// </summary>
        /// <param name="parent">The main MOAI manager which owns this debugging manager.</param>
        public Manager(MOAI.Manager parent)
        {
            this.p_Parent = parent;
        }

        /// <summary>
        /// Runs the specified project with debugging.
        /// </summary>
        /// <param name="project">The project to run under the debugger.</param>
        public bool Start(MOAI.Management.Project project)
        {
            // Check to see whether we are paused or not.
            if (this.p_Paused)
            {
                // Unpause.
                this.m_Communicator.Send(new ContinueMessage());
                this.p_Paused = false;
                if (this.DebugContinue != null)
                    this.DebugContinue(this, new EventArgs());
            }

            // Otherwise make sure we have no process running.
            if (this.m_Process != null)
            {
                // Can't run.
                return false;
            }
            
            // Fire the event to say that debugging has started.
            if (this.DebugStart != null)
                this.DebugStart(this, new EventArgs());

            // Clear the existing output log.
            this.m_OutputTool = this.p_Parent.ToolsManager.Get(typeof(OutputTool)) as OutputTool;
            if (this.m_OutputTool != null)
                this.m_OutputTool.ClearLog();

            // Start the debug listening service.
            this.m_Communicator = new Communicator(7018);
            this.m_Communicator.MessageArrived += new EventHandler<MessageEventArgs>(m_Communicator_MessageArrived);

            this.m_Process = new Process();
            this.m_Process.StartInfo.FileName = Path.Combine(Program.Manager.Settings["RootPath"], "Engines\\Win32\\Debug\\moai.exe");
            this.m_Process.StartInfo.WorkingDirectory = project.ProjectInfo.Directory.FullName;
            this.m_Process.StartInfo.UseShellExecute = false;
            //this.m_Process.StartInfo.RedirectStandardOutput = true;
            this.m_Process.StartInfo.Arguments = "Main.lua";
            //this.m_Process.OutputDataReceived += new DataReceivedEventHandler(m_Process_OutputDataReceived);
            this.m_Process.EnableRaisingEvents = true;
            this.m_Process.Exited += new EventHandler(m_Process_Exited);

            this.m_Process.Start();
            //this.m_Process.BeginOutputReadLine();

            this.p_Paused = false;
            return true;
        }

        /// <summary>
        /// Runs the specified project without debugging.
        /// </summary>
        /// <param name="project">The project to run without the debugger.</param>
        public bool StartWithout(MOAI.Management.Project project)
        {
            if (this.m_Process != null)
            {
                // Can't run.
                return false;
            }

            // Fire the event to say that debugging has started (even though
            // technically no debug events will be fired).
            if (this.DebugStart != null)
                this.DebugStart(this, new EventArgs());

            // Clear the existing output log.
            this.m_OutputTool = this.p_Parent.ToolsManager.Get(typeof(OutputTool)) as OutputTool;
            if (this.m_OutputTool != null)
                this.m_OutputTool.ClearLog();

            // Start the process.
            this.m_Process = new Process();
            this.m_Process.StartInfo.FileName = Path.Combine(Program.Manager.Settings["RootPath"], "Engines\\Win32\\Debug\\moai.exe");
            this.m_Process.StartInfo.WorkingDirectory = project.ProjectInfo.Directory.FullName;
            this.m_Process.StartInfo.UseShellExecute = false;
            this.m_Process.StartInfo.Arguments = "Main.lua";
            this.m_Process.EnableRaisingEvents = true;
            this.m_Process.Exited += new EventHandler(m_Process_Exited);

            this.m_Process.Start();
            this.p_Paused = false;
            return true;
        }

        /// <summary>
        /// Stops the debugging process that is currently underway.
        /// </summary>
        public void Pause()
        {
            if (this.m_Communicator != null)
                this.m_Communicator.Send(new PauseMessage());
        }

        /// <summary>
        /// Stops the debugging process that is currently underway.
        /// </summary>
        public void Stop()
        {
            if (this.m_Process == null)
                return;
            if (!this.m_Process.HasExited)
                this.m_Process.Kill();
            this.m_Process = null;
            if (this.m_Communicator != null)
                this.m_Communicator.Close();
            this.m_Communicator = null;

            // Fire the event to say that debugging has stopped.
            if (this.DebugStop != null)
                this.DebugStop(this, new EventArgs());
        }

        /// <summary>
        /// This event is raised when the game sends a debugging message to the IDE.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event information.</param>
        private void m_Communicator_MessageArrived(object sender, MessageEventArgs e)
        {
            // Invoke the message handling on the IDE's thread.
            this.p_Parent.IDEWindow.Invoke(new Action(() =>
            {
                if (e.Message is WaitMessage)
                {
                    // This is the game signalling that it is ready to receive
                    // message requests such as setting breakpoints before the
                    // game starts executing.

                    // After we have set breakpoints, we must tell the game to
                    // continue executing.
                    this.m_Communicator.Send(new ContinueMessage());
                }
                else if (e.Message is BreakMessage)
                {
                    // This is the game signalling that it has hit a breakpoint
                    // and is now paused.

                    // Open the designer window for the specified file.
                    Management.File f = this.p_Parent.ActiveProject.GetByPath((e.Message as BreakMessage).FileName);
                    Designer d = this.p_Parent.DesignersManager.OpenDesigner(f);
                    if (d is IDebuggable)
                    {
                        // We can only go to a specific line in the file if the
                        // designer supports it.
                        (d as IDebuggable).Debug(f, (e.Message as BreakMessage).LineNumber);
                    }

                    // Inform the IDE that the game is now paused.
                    this.p_Paused = true;
                    if (this.DebugPause != null)
                        this.DebugPause(this, new EventArgs());
                }
                else if (e.Message is ExcpInternalMessage)
                {
                    ExcpInternalMessage m = e.Message as ExcpInternalMessage;
                    ExceptionDialog d = new ExceptionDialog();
                    d.IDEWindow = this.p_Parent.IDEWindow;
                    d.MessageInternal = m;
                    d.Show();
                    // TODO: Indicate to the UI that the game is now paused.
                }
                else if (e.Message is ExcpUserMessage)
                {
                    ExcpUserMessage m = e.Message as ExcpUserMessage;
                    ExceptionDialog d = new ExceptionDialog();
                    d.IDEWindow = this.p_Parent.IDEWindow;
                    d.MessageUser = m;
                    d.Show();
                    // TODO: Indicate to the UI that the game is now paused.
                }
                else if (e.Message is ResultMessage)
                {
                    ResultMessage m = e.Message as ResultMessage;
                    // TODO: Use a queue to track messages sent to the engine and match them up with the result messages.
                }
                else
                {
                    // Unknown message!
                    // TODO: Handle this properly?
                    MessageBox.Show(e.Message.ID);
                }
            }));
        }

        /// <summary>
        /// This event is raised when the game has exited during debugging.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event information.</param>
        void m_Process_Exited(object sender, EventArgs e)
        {
            this.Stop();
        }

        /// <summary>
        /// The event is raised when the game or engine outputs to standard output
        /// and it's been redirected.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event information.</param>
        void m_Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (this.m_OutputTool != null && e.Data != null)
            {
                MOAI.Manager.VoidLambda lambda = () =>
                    {
                        this.m_OutputTool.AddLogEntry(e.Data);
                    };
                this.m_OutputTool.Invoke(lambda);
            }
        }

        /// <summary>
        /// The main MOAI manager that owns this debugging manager.
        /// </summary>
        public MOAI.Manager Parent
        {
            get
            {
                return this.p_Parent;
            }
        }
        
        /// <summary>
        /// Whether the program is currently paused (only applies if
        /// the program is running).
        /// </summary>
        public bool Paused
        {
            get
            {
                return this.p_Paused;
            }
        }
    }
}
