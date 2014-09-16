using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ConsoleControlAPI;

namespace ConsoleControl.WPF
{
    /// <summary>
    /// Interaction logic for ConsoleControl.xaml
    /// </summary>
    public partial class ConsoleControl : UserControl
    {
        private List<string> _enteredCommands = new List<string>();
        private int _currentEnteredCommand = 0;
        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleControl"/> class.
        /// </summary>
        public ConsoleControl()
        {
            InitializeComponent();
            
            //  Handle process events.
            _processInterace.OnProcessOutput += processInterace_OnProcessOutput;
            _processInterace.OnProcessError += processInterace_OnProcessError;
            _processInterace.OnProcessInput += processInterace_OnProcessInput;
            _processInterace.OnProcessExit += processInterace_OnProcessExit;
            
            InputConsole.KeyUp += InputConsoleKeyDown;
            InputConsole.PreviewKeyUp += InputConsole_PreviewKeyUp;
        }



        /// <summary>
        /// Handles the OnProcessError event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessError(object sender, ProcessEventArgs args)
        {
            //  Write the output, in red
            WriteOutput(args.Content, Colors.Red);

            //  Fire the output event.
            FireProcessOutputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessOutput event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessOutput(object sender, ProcessEventArgs args)
        {
            //  Write the output, in white
            WriteOutput(args.Content, Colors.White);

            //  Fire the output event.
            FireProcessOutputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessInput event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessInput(object sender, ProcessEventArgs args)
        {
            FireProcessInputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessExit event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        void processInterace_OnProcessExit(object sender, ProcessEventArgs args)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                WriteOutput(Environment.NewLine + _processInterace.ProcessFileName + " exited.", Color.FromArgb(255, 0, 255, 0));
            }

            IsProcessRunning = false;
        }

        /// <summary>
        /// Handles the KeyDown event of the richTextBoxConsole control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.KeyEventArgs" /> instance containing the event data.</param>
        void InputConsoleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return) return;

            var input = InputConsole.Text;
            AddCommand(input);
            InputConsole.Text = string.Empty;
            WriteInput(input, Colors.White, false);
        }

        void InputConsole_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up ||
                e.Key == Key.Down)
            {
                SetPreviousCommand(e.Key);
            }
        }

        private void SetPreviousCommand(Key key)
        {
            if (_enteredCommands.Count == 0)
                return;

            if (key == Key.Up)
                _currentEnteredCommand--;
            if (key == Key.Down)
                _currentEnteredCommand++;

            if (_currentEnteredCommand > _enteredCommands.Count - 1)
                _currentEnteredCommand = _enteredCommands.Count - 1;

            if (_currentEnteredCommand < 0)
                _currentEnteredCommand = 0;

            InputConsole.Text = _enteredCommands[_currentEnteredCommand];
        }

        private void AddCommand(string input)
        {
            if (_enteredCommands.LastOrDefault() == input) return;
            
            _enteredCommands.Add(input);
            _currentEnteredCommand = _enteredCommands.Count - 1;
        }

        /// <summary>
        /// Writes the output to the console control.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="color">The color.</param>
        public void WriteOutput(string output, Color color)
        {
            if (string.IsNullOrEmpty(_lastInput) == false &&
                (output == _lastInput || output.Replace("\r\n", "") == _lastInput))
                return;

            RunOnUiDespatcher(() =>
                {
                    //  Write the output.
                    RichTextBoxConsole.Selection.ApplyPropertyValue(TextBlock.ForegroundProperty, new SolidColorBrush(color));
                    RichTextBoxConsole.AppendText(output);
                    RichTextBoxConsole.ScrollToEnd();
                });
        }

        /// <summary>
        /// Clears the output.
        /// </summary>
        public void ClearOutput()
        {
           //todo richTextBoxConsole.Clear();
        }

        /// <summary>
        /// Writes the input to the console control.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="color">The color.</param>
        /// <param name="echo">if set to <c>true</c> echo the input.</param>
        public void WriteInput(string input, Color color, bool echo)
        {
            RunOnUiDespatcher(() =>
                {
                    //  Are we echoing?
                    if (echo)
                    {
                        RichTextBoxConsole.Selection.ApplyPropertyValue(TextBlock.ForegroundProperty, new SolidColorBrush(color));
                        RichTextBoxConsole.AppendText(input);
                    }

                    _lastInput = input;

                    //  Write the input.
                    _processInterace.WriteInput(input);

                    //  Fire the event.
                    FireProcessInputEvent(new ProcessEventArgs(input));
                });
        }

        /// <summary>
        /// Runs the on UI despatcher.
        /// </summary>
        /// <param name="action">The action.</param>
        private void RunOnUiDespatcher(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                //  Invoke the action.
                action();
            }
            else
            {
                Dispatcher.BeginInvoke(action, null);
            }
        }


        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="arguments">The arguments.</param>
        public void StartProcess(string fileName, string arguments)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                WriteOutput("Preparing to run " + fileName, Color.FromArgb(255, 0, 255, 0));
                if (!string.IsNullOrEmpty(arguments))
                    WriteOutput(" with arguments " + arguments + "." + Environment.NewLine, Color.FromArgb(255, 0, 255, 0));
                else
                    WriteOutput("." + Environment.NewLine, Color.FromArgb(255, 0, 255, 0));
            }

            //  Start the process.
            _processInterace.StartProcess(fileName, arguments);

            //  We're now running.
            IsProcessRunning = true;
        }

        /// <summary>
        /// Stops the process.
        /// </summary>
        public void StopProcess()
        {
            //  Stop the interface.
            _processInterace.StopProcess();
        }

        /// <summary>
        /// Fires the console output event.
        /// </summary>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void FireProcessOutputEvent(ProcessEventArgs args)
        {
            //  Get the event.
            var theEvent = OnProcessOutput;
            if (theEvent != null)
                theEvent(this, args);
        }

        /// <summary>
        /// Fires the console input event.
        /// </summary>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void FireProcessInputEvent(ProcessEventArgs args)
        {
            //  Get the event.
            var theEvent = OnProcessInput;
            if (theEvent != null)
                theEvent(this, args);
        }

        /// <summary>
        /// The internal process interface used to interface with the process.
        /// </summary>
        private readonly ProcessInterface _processInterace = new ProcessInterface();

        /// <summary>
        /// The last input string (used so that we can make sure we don't echo input twice).
        /// </summary>
        private string _lastInput;
        
        /// <summary>
        /// Occurs when console output is produced.
        /// </summary>
        public event ProcessEventHanlder OnProcessOutput;

        /// <summary>
        /// Occurs when console input is produced.
        /// </summary>
        public event ProcessEventHanlder OnProcessInput;
          
        private static readonly DependencyProperty ShowDiagnosticsProperty = 
          DependencyProperty.Register("ShowDiagnostics", typeof(bool), typeof(ConsoleControl),
          new PropertyMetadata(false, OnShowDiagnosticsChanged));

        /// <summary>
        /// Gets or sets a value indicating whether to show diagnostics.
        /// </summary>
        /// <value>
        ///   <c>true</c> if show diagnostics; otherwise, <c>false</c>.
        /// </value>
        public bool ShowDiagnostics
        {
          get { return (bool)GetValue(ShowDiagnosticsProperty); }
          set { SetValue(ShowDiagnosticsProperty, value); }
        }
        
        private static void OnShowDiagnosticsChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
        }
        
        
        private static readonly DependencyProperty IsInputEnabledProperty = 
          DependencyProperty.Register("IsInputEnabled", typeof(bool), typeof(ConsoleControl),
          new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets a value indicating whether this instance has input enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has input enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsInputEnabled
        {
          get { return (bool)GetValue(IsInputEnabledProperty); }
          set { SetValue(IsInputEnabledProperty, value); }
        }
        
        internal static readonly DependencyPropertyKey IsProcessRunningPropertyKey =
          DependencyProperty.RegisterReadOnly("IsProcessRunning", typeof(bool), typeof(ConsoleControl),
          new PropertyMetadata(false));

        private static readonly DependencyProperty IsProcessRunningProperty = IsProcessRunningPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a value indicating whether this instance has a process running.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has a process running; otherwise, <c>false</c>.
        /// </value>
        public bool IsProcessRunning
        {
            get { return (bool)GetValue(IsProcessRunningProperty); }
            private set { SetValue(IsProcessRunningPropertyKey, value); }
        }
    }
}
