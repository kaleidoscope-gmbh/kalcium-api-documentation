using System;
using System.Threading.Tasks;

namespace Kaleidoscope.Kalcium.TestClient
{
    class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Tests started");
            try
            {
                var kalcTest = new KalcTest("http://localhost:42000");
                Task.Run(() => kalcTest.RunTests("test", "test", "TestTermbase")).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            Console.WriteLine("Tests have been finished");
            Console.ReadLine();
        }

    }

}
