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
        int a { get; set; }

        int Fly(string arg);

        int Fly<T>(T a) where T : struct;
    }
    
    public interface IRun
    {
        void Run();
    }

    public class Bird : IFly, IRun
    {
        public int a { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int Fly(string arg)
        {
            //Console.WriteLine("起飞 {0}", arg);
            return 5;
        }

        public int Fly<T>(T a) where T : struct
        {
            //Console.WriteLine("泛型起飞 {0}", a);
            return 6;
        }

        public void Run()
        {
            Console.WriteLine("跑步");
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

    public class BirdProxy : BirdDynamicProxy, IFly
    {
        private readonly Bird _bird;

        public BirdProxy(Bird bird)
        {
            _bird = bird;
        }

        public int a { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int Fly<T>(T a) where T : struct
        {
            return default;
        }

        public int Fly(string arg)
        {
            return _bird.Fly(arg);
        }

        public void Run()
        {
            throw new NotImplementedException();
        }
    }



    class Program
    {
        static void Main(string[] args)
        {
            var utils = new DynamicProxyTypeUtils();
            var type = utils.CreateInterfaceProxyType(typeof(IFly), typeof(Bird));
            var fly = (IFly)Activator.CreateInstance(type, new object[] { new Bird() });
            var amethods = typeof(IFly).GetMethods();
            var properties = typeof(IFly).GetTypeInfo().DeclaredProperties;
            var bmethods = type.GetTypeInfo().DeclaredMethods.ToArray();

            var c = amethods[1].ToString().Equals(bmethods[1].ToString());
            var a = fly.Fly(5);
            var run = (IRun)fly;
            run.Run();
            //fly.Run();
            //var type = utils.CreateInterfaceProxyType<IFly, BirdDynamicProxy>();
            //var fly = (IFly)Activator.CreateInstance(type, new object[] { new Bird() });
            //fly.Run();
            //var stopwatch = Stopwatch.StartNew();
            //for (int i = 0; i < 1000000; i++)
            //{
            //    var fly = (IFly)Activator.CreateInstance(type, new object[] { new Bird() });
            //    var a = fly.Fly("什么鬼");
            //}
            //stopwatch.Stop();
            //Console.WriteLine("耗时: {0}", stopwatch.ElapsedMilliseconds);
        }
    }
}
