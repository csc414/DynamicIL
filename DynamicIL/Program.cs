using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace DynamicIL
{
    public interface IFly
    {
        int Fly(string arg);

        int Fly<T>(T a);
    }

    public class Bird : IFly
    {
        public int Fly(string arg)
        {
            //Console.WriteLine("起飞 {0}", arg);
            return 5;
        }

        public int Fly<T>(T a)
        {
            //Console.WriteLine("泛型起飞 {0}", a);
            return 6;
        }
    }

    public class BirdDynamicProxy : DynamicProxy
    {
        protected override object Invoke(object instance, MethodInfo targetMethod, object[] args)
        {
            //Console.WriteLine("起飞前");
            var result = targetMethod.Invoke(instance, args);
            //Console.WriteLine("起飞后");
            return result;
        }
    }

    /// <summary>
    /// 基于接口的代理
    /// </summary>
    public class BirdProxy : BirdDynamicProxy, IFly
    {
        private readonly Bird _bird;

        public BirdProxy(Bird bird)
        {
            _bird = bird;
        }

        public int Fly<T>(T a)
        {
            return default;
        }

        public int Fly(string arg)
        {
            return default;
        }
    }



    class Program
    {
        static void Main(string[] args)
        {
            int[] aa = new int[5];
            aa[1] = 5;

            var utils = new DynamicProxyTypeUtils();
            var type = utils.CreateInterfaceProxyType<IFly, BirdDynamicProxy>();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 1000000; i++)
            {
                var fly = (IFly)Activator.CreateInstance(type, new object[] { new Bird() });
                var a = fly.Fly("什么鬼");
            }
            stopwatch.Stop();
            Console.WriteLine("耗时: {0}", stopwatch.ElapsedMilliseconds);
        }
    }
}
