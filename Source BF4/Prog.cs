using System;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;

namespace PlefVersion
{
    class Prog
    {
        private static Process process;
        private const string GAME_PROC_NAME = "bf4";

        [STAThread]
        static void Main(string[] args)
        {
            Init();

            CmdSpiner spin = new CmdSpiner();

            while (true)
            {
                if (IsGameRunning())
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    ClearCurrentConsoleLine();

                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine(@"################################################################################");
                    Console.WriteLine(@"#########################  //V\\ ||< |[]|  KHAI        #########################");
                    Console.WriteLine(@"################################################################################");
                    Console.WriteLine();

                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine(@"################################################################################");
                    Console.WriteLine(@"######################### STATUS : BF4 LOCKED & LOADED #########################");
                    Console.WriteLine(@"################################################################################");

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(true);

                    Manager.ShowWindow(Manager.GetConsoleWindow(), 0);
                    Thread.Sleep(2000);

                    Application.Run(new Overlay(process));
                    break;
                }
                spin.Turn();
                Thread.Sleep(100);
            }

            Console.ReadKey();
        }

        private static void Init()
        {
            Console.Title = "BF4 TEST " + System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            //Console.SetWindowSize(170, 55);

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(@"################################################################################");
            Console.WriteLine(@"#########################  //V\\ ||< |[]|      KHAI    #########################");
            Console.WriteLine(@"################################################################################");
            Console.WriteLine();
            Thread.Sleep(1000);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"################################################################################");
            Console.WriteLine(@"######################### STATUS : WAITING BF4 STARTUP #########################");
            Console.WriteLine(@"################################################################################");
            Console.WriteLine();
        }

        public static bool IsGameRunning()
        {
            Process[] plist = Process.GetProcessesByName(GAME_PROC_NAME);
            foreach (Process p in plist)
                if (p.ProcessName == GAME_PROC_NAME)
                {
                    process = p;
                    return true;
                }
             return false;
        }

        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("");

            Console.SetCursorPosition(0, currentLineCursor);
        }
    }
}
