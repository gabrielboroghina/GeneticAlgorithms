using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNet.SignalR;
using System.Net.NetworkInformation;

namespace WebApplication1.Controllers
{
    // Delegate definition used for events to receive notification
    // of text read from stdout or stderr of a running process.
    public delegate void StringReadEventHandler(string text);

    public class ProcessIoManager
    {
        #region Private_Fields
        // Command line process that is executing and being
        // monitored by this class for stdout/stderr output.
        private Process runningProcess;

        // Thread to monitor and read standard output (stdout)
        private Thread stdoutThread;

        // Thread to monitor and read standard error (stderr)
        private Thread stderrThread;

        // Buffer to hold characters read from either stdout, or stderr streams
        private StringBuilder streambuffer;
        #endregion

        #region Public_Properties_And_Events
        /// <summary>
        /// Gets the process being monitored
        /// </summary>
        /// <value>The running process.</value>
        public Process RunningProcess
        {
            get { return runningProcess; }
        }

        // Event to notify of a string read from stdout stream
        public event StringReadEventHandler StdoutTextRead;

        // Event to notify of a string read from stderr stream
        public event StringReadEventHandler StderrTextRead;

        #endregion

        #region Constructor_And_Initialization
        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessIoManager"/> class.        
        /// </summary>
        /// <param name="process">The process.</param>
        /// <remarks>
        /// Does not automatically start listening for stdout/stderr.
        /// Call StartProcessOutputRead() to begin listening for process output.
        /// </remarks>
        /// <seealso cref="StartProcessOutputRead"/>
        public ProcessIoManager(Process process)
        {
            if (process == null)
                throw new Exception("ProcessIoManager unable to set running process - null value is supplied");

            if (process.HasExited == true)
                throw new Exception("ProcessIoManager unable to set running process - the process has already existed.");

            this.runningProcess = process;
            this.streambuffer = new StringBuilder(256);
        }
        #endregion

        #region Public_Methods
        /// <summary>
        /// Starts the background threads reading any output produced (standard output, standard error)
        /// that is produces by the running process.
        /// </summary>
        public void StartProcessOutputRead()
        {
            // Just to make sure there aren't previous threads running.
            StopMonitoringProcessOutput();

            // Make sure we have a valid, running process
            CheckForValidProcess("Unable to start monitoring process output.", true);

            // If the stdout is redirected for the process, then start
            // the stdout thread, which will manage the reading of stdout from the
            // running process, and report text read via supplied events.
            if (runningProcess.StartInfo.RedirectStandardOutput == true)
            {
                stdoutThread = new Thread(new ThreadStart(ReadStandardOutputThreadMethod));
                // Make thread a background thread - if it was foreground, then
                // the thread could hang up the process from exiting. Background
                // threads will be forced to stop on main thread termination.
                stdoutThread.IsBackground = true;
                stdoutThread.Start();
            }

            // If the stderr  is redirected for the process, then start
            // the stderr thread, which will manage the reading of stderr from the
            // running process, and report text read via supplied events.
            if (runningProcess.StartInfo.RedirectStandardError == true)
            {
                stderrThread = new Thread(new ThreadStart(ReadStandardErrorThreadMethod));
                stderrThread.IsBackground = true;
                stderrThread.Start();
            }
        }

        /// <summary>
        /// Writes the supplied text string to the standard input (stdin) of the running process
        /// </summary>
        /// <remarks>In order to be able to write to the Process, the StartInfo.RedirectStandardInput must be set to true.</remarks>
        /// <param name="text">The text to write to running process input stream.</param>
        public void WriteStdin(string text)
        {
            // Make sure we have a valid, running process
            CheckForValidProcess("Unable to write to process standard input.", true);
            if (runningProcess.StartInfo.RedirectStandardInput == true)
                runningProcess.StandardInput.WriteLine(text);
        }
        #endregion

        #region Private_Methods
        /// <summary>
        /// Checks for valid (non-null Process), and optionally check to see if the process has exited.
        /// Throws Exception if process is null, or if process has existed and checkForHasExited is true.
        /// </summary>
        /// <param name="errorMessageText">The error message text to display if an exception is thrown.</param>
        /// <param name="checkForHasExited">if set to <c>true</c> [check if process has exited].</param>
        private void CheckForValidProcess(string errorMessageText, bool checkForHasExited)
        {
            errorMessageText = (errorMessageText == null ? "" : errorMessageText.Trim());
            if (runningProcess == null)
                throw new Exception(errorMessageText + " (Running process must be available)");

            if (checkForHasExited && runningProcess.HasExited)
                throw new Exception(errorMessageText + " (Process has exited)");
        }

        /// <summary>
        /// Read characters from the supplied stream, and accumulate them in the
        /// 'streambuffer' variable until there are no more characters to read.
        /// </summary>
        /// <param name="firstCharRead">The first character that has already been read.</param>
        /// <param name="streamReader">The stream reader to read text from.</param>
        /// <param name="isstdout">if set to <c>true</c> the stream is assumed to be standard output, otherwise assumed to be standard error.</param>
        private void ReadStream(int firstCharRead, StreamReader streamReader, bool isstdout)
        {
            // One of the streams (stdout, stderr) has characters ready to be written
            // Flush the supplied stream until no more characters remain to be read.
            // The synchronized/ locked section of code to prevent the other thread from
            // reading its stream at the same time, producing intermixed stderr/stdout results. 
            // If the threads were not synchronized, the threads
            // could read from both stream simultaneously, and jumble up the text with
            // stderr and stdout text intermixed.
            lock (this)
            {
                // Single character read from either stdout or stderr
                int ch;
                // Clear the stream buffer to hold the text to be read
                streambuffer.Length = 0;

                //Console.WriteLine("CHAR=" + firstCharRead);
                streambuffer.Append((char)firstCharRead);

                // While there are more characters to be read
                while (streamReader.Peek() > -1)
                {
                    // Read the character in the queue
                    ch = streamReader.Read();

                    // Accumulate the characters read in the stream buffer
                    streambuffer.Append((char)ch);

                    // Send text one line at a time - much more efficient than
                    // one character at a time
                    if (ch == '\n')
                        NotifyAndFlushBufferText(streambuffer, isstdout);
                }
                // Flush any remaining text in the buffer
                NotifyAndFlushBufferText(streambuffer, isstdout);
            } // End lock()
        }

        /// <summary>
        /// Invokes the OnStdoutTextRead (if isstdout==true)/ OnStderrTextRead events
        /// with the supplied streambuilder 'textbuffer', then clears
        /// textbuffer after event is invoked.
        /// </summary>
        /// <param name="textbuffer">The textbuffer containing the text string to pass to events.</param>
        /// <param name="isstdout">if set to true, the stdout event is invoked, otherwise stedrr event is invoked.</param>
        private void NotifyAndFlushBufferText(StringBuilder textbuffer, bool isstdout)
        {
            if (textbuffer.Length > 0)
            {
                if (isstdout == true && StdoutTextRead != null)
                {   // Send notificatin of text read from stdout
                    StdoutTextRead(textbuffer.ToString());
                }
                else if (isstdout == false && StderrTextRead != null)
                {   // Send notificatin of text read from stderr
                    StderrTextRead(textbuffer.ToString());
                }
                // 'Clear' the text buffer
                textbuffer.Length = 0;
            }
        }

        /// <summary>
        /// Method started in a background thread (stdoutThread) to manage the reading and reporting of
        /// standard output text produced by the running process.
        /// </summary>
        private void ReadStandardOutputThreadMethod()
        {
            // Main entry point for thread - make sure the main entry method
            // is surrounded with try catch, so an uncaught exception won't
            // screw up the entire application
            try
            {
                // character read from stdout
                int ch;

                // The Read() method will block until something is available
                while (runningProcess != null && (ch = runningProcess.StandardOutput.Read()) > -1)
                {
                    // a character has become available for reading
                    // block the other thread and process this stream's input.
                    ReadStream(ch, runningProcess.StandardOutput, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProcessIoManager.ReadStandardOutputThreadMethod()- Exception caught:" +
                    ex.Message + "\nStack Trace:" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Method started in a background thread (stderrThread) to manage the reading and reporting of
        /// standard error text produced by the running process.
        /// </summary>
        private void ReadStandardErrorThreadMethod()
        {
            try
            {
                // Character read from stderr
                int ch;
                // The Read() method will block until something is available
                while (runningProcess != null && (ch = runningProcess.StandardError.Read()) > -1)
                    ReadStream(ch, runningProcess.StandardError, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProcessIoManager.ReadStandardErrorThreadMethod()- Exception caught:" +
                     ex.Message + "\nStack Trace:" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Stops both the standard input and stardard error background reader threads (via the Abort() method)        
        /// </summary>
        public void StopMonitoringProcessOutput()
        {
            // Stop the stdout reader thread
            try
            {
                if (stdoutThread != null)
                    stdoutThread.Abort();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProcessIoManager.StopReadThreads()-Exception caught on stopping stdout thread.\n" +
                    "Exception Message:\n" + ex.Message + "\nStack Trace:\n" + ex.StackTrace);
            }

            // Stop the stderr reader thread
            try
            {
                if (stderrThread != null)
                    stderrThread.Abort();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProcessIoManager.StopReadThreads()-Exception caught on stopping stderr thread.\n" +
                    "Exception Message:\n" + ex.Message + "\nStack Trace:\n" + ex.StackTrace);
            }
            stdoutThread = null;
            stderrThread = null;
        }
        #endregion
    }

    public class HomeController : Controller
    {
        static bool sw,on;
        static string w; //console output, errors
        static ProcessStartInfo psi;
        static Process proc;
        static StreamWriter sIn;
        ProcessIoManager procmgr;
        static string compilare, exec;
        static string user;

        public ActionResult Index()
        {
            return View();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ C++
        public ActionResult cpp(bool sw, string fisier)
        {
            string folder = Server.MapPath("/Content");
            string folderd = Server.MapPath("/Content/documente");
            ViewBag.fisier = fisier;

            // Create the ProcessInfo object
            psi = new ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = folderd;
            proc = new Process();
            proc.StartInfo = psi;

            //compiling and execution commands
            compilare = folder + "/MinGW/bin/mingw32-g++.exe -Wall -fexceptions -g -c " + folder + "/documente/main.cpp" + " -o " + folder + "/documente/main.o";
            exec = folder + "/MinGW/bin/mingw32-g++.exe -o " + folder + "/documente/main.exe " + folder + "/documente/main.o";

            return View();
        }
        public async Task<string> compile(string cppval, string id)
        {
            string folderd = Server.MapPath("/Content/documente/");
            string sursa = null;
            string err;
            string aux=null;

            user = id;   //GET the connection ID

            //Write data in files
            cppval.Replace("+", "%2B");
            sursa = Uri.UnescapeDataString(cppval);
            ViewBag.fisier = "main.cpp";
            StreamWriter flux = new StreamWriter(folderd + "main.cpp");
            flux.Write(sursa); flux.Close();

            //START - COMPILE
            err = null;
            if (on) End();
            proc.Start();
            on = true;
            
            // Attach the in for writing
            sIn = proc.StandardInput;
            await sIn.WriteLineAsync(@compilare);
            
            sIn.WriteLine("exit");
            err = proc.StandardError.ReadToEnd();
            proc.Close();

            if (err.Length >= 56)
            {
                for (int i = 57; i < err.Length; i++) aux += err[i];
                err = aux;
            }
            if (err == null || err.Length < 3) return "Successful compiling!";
            else
            {
                aux = null;
                for (int i = 0; i < err.Length; i++)
                    if (err[i] != '\n') aux += err[i];
                    else aux += "<br/>";
                return aux;
            }
        }

        public async Task<string> run(string cppval, string datainput)
        {
            string folder = Server.MapPath("/Content");

            sw = false; w = null;
            if (on) End();
            proc.Start();                              //START cmd.exe
            procmgr = new ProcessIoManager(proc);
            procmgr.StdoutTextRead += new StringReadEventHandler(OnOutputReceived);
            procmgr.StartProcessOutputRead();
            on = true;

            // Attach the in for writing
            sIn = proc.StandardInput;
            
            //RUN executable
            await sIn.WriteLineAsync(@exec);
            await sIn.WriteLineAsync(@"cd " + folder + "\\documente");
            await sIn.WriteLineAsync(@"main.exe");

            if (w.Contains("documente>\n")) End();
            return w;
        }
        public void OnOutputReceived(string text)     //send text from cmd.exe to client -------------------------signalR
        {
            Thread.Sleep(5);
            if (text.Contains("e>main.exe")|| text.Contains("documente main")) { sw = true; text = ""; }
            if (sw)
            {
                var hubContext = GlobalHost.ConnectionManager.GetHubContext<GlassBall.MyHub>();
                hubContext.Clients.Client(user).broadcastMessage(text);
            }
        }
        public void End()
        {
            proc.Close();
            on = false;
        }
        public async Task<string> interaction(string data)
        {
            await sIn.WriteLineAsync(@data+"\n");

            if (w.Contains("documente>")) End();
            return w;
        }
        public string afis_sursa(string fisier)
        {
            string buffer = "";
            char c;
            string folder = Server.MapPath("/Content/documente/");
            StreamReader fin = new StreamReader(folder + fisier);
            while (fin.Peek() >= 0)
            {
                c = (char)fin.Read();
                if (c == '<') buffer += "&#60;";
                else if (c == '>') buffer += "&#62;";
                else buffer += c;
            }
            fin.Close();
            return buffer;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ JAVA
        public ActionResult java(bool sw, string fisier)
        {
            string folderd = Server.MapPath("/Content/documente");
            string jf = Server.MapPath("/Content/Java/jdk1.8.0_51/bin");
            ViewBag.fisier = fisier;

            // Create the ProcessInfo object
            psi = new ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = jf;
            proc = new Process();
            proc.StartInfo = psi;

            compilare = "javac " + folderd + "/main.java";

            return View();
        }
        public async Task<string> jcompile(string cppval, string id)
        {
            string folderd = Server.MapPath("/Content/documente/");
            
            string sursa = null;
            string err;
            string aux = null;

            user = id;

            //Write data in files
            string algorithm;
            using (StreamReader f = new StreamReader(folderd + "genetic.txt"))
                algorithm = f.ReadToEnd();

            cppval.Replace("+", "%2B");
            sursa = Uri.UnescapeDataString(cppval);
            ViewBag.fisier = "main.java";
            StreamWriter flux = new StreamWriter(folderd + "main.java");
            flux.Write(algorithm+sursa+"}"); flux.Close();

            //START - COMPILE
            err = null;
            if (on) End();
            proc.Start();
            on = true;

            // Attach the in for writing
            sIn = proc.StandardInput;
            await sIn.WriteLineAsync(@compilare);

            sIn.WriteLine("exit");
            err = proc.StandardError.ReadToEnd();
            proc.Close();

            if (err.Length >= 56)
            {
                for (int i = 57; i < err.Length; i++) aux += err[i];
                err = aux;
            }
            if (err == null || err.Length < 3) return "Successful compiling!";
            else
            {
                aux = null;
                for (int i = 0; i < err.Length; i++)
                    if (err[i] != '\n') aux += err[i];
                    else aux += "<br/>";
                return aux;
            }
        }

        public async Task<string> jrun(string cppval, string datainput)
        {
            string folderd = Server.MapPath("/Content/documente");
            sw = false; w = null;
            if (on) End();
            proc.Start();
            procmgr = new ProcessIoManager(proc);
            procmgr.StdoutTextRead += new StringReadEventHandler(OnOutputReceived);
            procmgr.StartProcessOutputRead();
            on = true;

            // Attach the in for writing
            sIn = proc.StandardInput;
            //RUN executable
            await sIn.WriteLineAsync(@"java -cp " + folderd + " main");
            
            if (w.Contains("bin>\n")) End();
            return w;
        }

        public async Task<string> jinteraction(string data)
        {
            await sIn.WriteLineAsync(@data + "\n");

            if (w.Contains("bin>")) End();
            return w;
        }


        //FILE PROCESSING
        public void writefile(string path, string buffer)
        {
            string files_folder = Server.MapPath("/Content/documente/");
            StreamWriter fout = new StreamWriter(files_folder+path);
            fout.Write(buffer);
            fout.Close();
        }
        public string readfile(string path)
        {
            string files_folder = Server.MapPath("/Content/documente/");
            StreamReader fin = new StreamReader(files_folder + path);
            string rez=fin.ReadToEnd();
            fin.Close();
            return rez;
        }
        public void jwritefile(string path, string buffer)
        {
            string files_folder = Server.MapPath("/Content/Java/jdk1.8.0_51/bin/");
            StreamWriter fout = new StreamWriter(files_folder + path);
            fout.Write(buffer);
            fout.Close();
        }
        public string jreadfile(string path)
        {
            string files_folder = Server.MapPath("/Content/Java/jdk1.8.0_51/bin/");
            StreamReader fin = new StreamReader(files_folder + path);
            string rez = fin.ReadToEnd();
            fin.Close();
            return rez;
        }


        public ActionResult comis_voiajor(bool sw, string fisier)
        {
            string folderd = Server.MapPath("/Content/documente");
            string jf = Server.MapPath("/Content/Java/jdk1.8.0_51/bin");
            ViewBag.fisier = fisier;

            // Create the ProcessInfo object
            psi = new ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = jf;
            proc = new Process();
            proc.StartInfo = psi;

            compilare = "javac " + folderd + "/main.java";
            string algorithm;
            using (StreamReader f = new StreamReader(folderd + "/cv_a.txt"))
                algorithm = f.ReadToEnd();
            using (StreamWriter f = new StreamWriter(folderd + "/genetic.txt"))
                f.Write(algorithm);
            return View();
        }
        public ActionResult permutari(bool sw, string fisier)
        {
            string folderd = Server.MapPath("/Content/documente");
            string jf = Server.MapPath("/Content/Java/jdk1.8.0_51/bin");
            ViewBag.fisier = fisier;

            // Create the ProcessInfo object
            psi = new ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = jf;
            proc = new Process();
            proc.StartInfo = psi;

            compilare = "javac " + folderd + "/main.java";
            string algorithm;
            using (StreamReader f = new StreamReader(folderd + "/perm_a.txt"))
                algorithm = f.ReadToEnd();
            using (StreamWriter f = new StreamWriter(folderd + "/genetic.txt"))
                f.Write(algorithm);
            return View();
        }
        public ActionResult suma(bool sw, string fisier)
        {
            string folderd = Server.MapPath("/Content/documente");
            string jf = Server.MapPath("/Content/Java/jdk1.8.0_51/bin");
            ViewBag.fisier = fisier;

            // Create the ProcessInfo object
            psi = new ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.WorkingDirectory = jf;
            proc = new Process();
            proc.StartInfo = psi;

            compilare = "javac " + folderd + "/main.java";
            string algorithm;
            using (StreamReader f = new StreamReader(folderd + "/suma_a.txt"))
                algorithm = f.ReadToEnd();
            using (StreamWriter f = new StreamWriter(folderd + "/genetic.txt"))
                f.Write(algorithm);
            return View();
        }
    }
}