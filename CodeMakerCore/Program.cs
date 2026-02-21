using System;

namespace CodeMaker
{
    class Program
    {
        static void Main(string[] args)
        {
            //args = new string[] { "QuestSvc.Protocol", "SvtQuest","e:\\"};

            if (args == null || args.Length < 3)
            {
                Console.WriteLine("Usage:\r\ncodemaker dllFile servantName savePath");
                return;
            }

            string dllFile = args[0].Trim();
            string servant = args[1].Trim();
            string savePath = args[2].Trim();

            ProxyCodeMaker maker = new ProxyCodeMaker();
            maker.Start(dllFile, servant, savePath);
        }
    }
}
