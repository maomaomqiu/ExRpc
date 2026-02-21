using System;

namespace UnitTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            TestClient test = new TestClient();
            test.Start();
            Console.ReadLine();
        }
    }
}
