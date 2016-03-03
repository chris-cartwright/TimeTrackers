using System;
using System.Threading;
using System.Windows;
using log4net;
using log4net.Appender;
using log4net.Repository;

namespace TimeTrackers {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App {
        private static readonly Mutex Mutex = new Mutex(true, "{36513326-FA2D-4005-A042-26B1DF37EAFB}");
        private static readonly EventWaitHandle ShowApp = new EventWaitHandle(false, EventResetMode.AutoReset, "{3439C7F4-CABA-4528-9ABC-9DDC6FDEACCE}");
        private static volatile bool _exitForegroundListener;
        private static MainWindow _mainWindow;
        private static Thread _foregroundListenerThread;

        private readonly ILog _logger = LogManager.GetLogger(typeof(App));

        private static void ForegroundListener() {
            while (!_exitForegroundListener) {
                if (ShowApp.WaitOne(100)) {
                    Current.Dispatcher.Invoke(() => _mainWindow.WindowState = WindowState.Normal);
                }
            }
        }

        public void FlushBuffers() {
            ILoggerRepository rep = LogManager.GetRepository();
            foreach (IAppender appender in rep.GetAppenders()) {
                (appender as BufferingAppenderSkeleton)?.Flush();
            }
        }

        public App() {
            InitializeComponent();

            _logger.Debug("Application started");

            DispatcherUnhandledException += (sender, args) => {
                _logger.Fatal(args.Exception);
                FlushBuffers();
            };
        }

        protected override void OnExit(ExitEventArgs e) {
            base.OnExit(e);

            _exitForegroundListener = true;
            if (!_foregroundListenerThread.Join(1000)) {
                _foregroundListenerThread.Abort();
            }
        }

        [STAThread]
        public static void Main() {
            if (!Mutex.WaitOne(TimeSpan.Zero, true)) {
                ShowApp.Set();
                return;
            }

            log4net.Config.XmlConfigurator.Configure();

            App app = new App();
            _mainWindow = new MainWindow();

            _foregroundListenerThread = new Thread(ForegroundListener);
            _foregroundListenerThread.Start();

            app.Run(_mainWindow);
            Mutex.ReleaseMutex();
        }
    }
}
