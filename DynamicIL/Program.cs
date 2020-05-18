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
        int Property { get; set; }

        int Fly(string arg);

        int Fly<T>(T a) where T : struct;
    }
    
    public interface IRun
    {
        void Run();
    }

    public class Bird : IFly, IRun
    {
        public int Property { get; set; } = 10;

        public int Fly(string arg)
        {
            Console.WriteLine("起飞 {0}", arg);
            return 5;
        }

        public int Fly<T>(T a) where T : struct
        {
            Console.WriteLine("泛型起飞 {0}", a);
            return 6;
        }

        public void Run()
        {
            Console.WriteLine("跑步");
        }
    }

    //public class BirdProxy : IFly
    //{
    //    private readonly Bird _bird;

    //    public BirdProxy(Bird bird)
    //    {
    //        _bird = bird;
    //    }

    //    public int a { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    //    public int Fly<T>(T a) where T : struct
    //    {
    //        return default;
    //    }

    //    public int Fly(string arg)
    //    {
    //        return _bird.Fly(arg);
    //    }

    //    public void Run()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    class Program
    {
        static void Main(string[] args)
        {
            var utils = new DynamicProxyTypeUtils();
            var type = utils.CreateInterfaceProxyType(typeof(IFly), typeof(Bird));
            var fly = (IFly)Activator.CreateInstance(type, new object[] { new Bird() });
            fly.Fly("Hello");
            fly.Fly(5);
            var run = (IRun)fly;
            run.Run();
            Console.WriteLine("Property: {0}", fly.Property);
            if(fly is IProxyInstance proxyInstance)
            {
                var instance = proxyInstance.GetInstance();
                Console.WriteLine(instance);
            }
        }
    }
}
