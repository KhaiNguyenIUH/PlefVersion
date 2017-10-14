using System;

namespace PlefVersion
{
    class CmdSpiner
    {
        int counter;
        public CmdSpiner()
        {
            counter = 0;
        }
		void hbjhbj()
		{

		}
        public void Turn()
        {
            counter++;
            switch (counter % 4)
            {
                case 0: Console.Write("/"); break;
                case 1: Console.Write("-"); break;
                case 2: Console.Write("\\"); break;
                case 3: Console.Write("|"); break;
            }
            Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            Console.CursorVisible = false;
        }
    }
}
