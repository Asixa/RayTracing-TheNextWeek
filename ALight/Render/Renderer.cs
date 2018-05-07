using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using AcDx;
using ALight.Render.Components;
using ALight.Render.Materials;
using ALight.Render.Mathematics;
using ALight.Render.Primitives;
using Random = ALight.Render.Mathematics.Random;

namespace ALight.Render
{
    public class Preview : DxWindow
    {
        public override void Update()
        {
            for (var i = 0; i < Buff.Length; i++)
                Buff[i] = (byte) Mathf.Range(Renderer.main.buff[i] * 255 / Renderer.main.Changes[i / 4] + 0.5f, 0,255f);
        }
    }
    public class Renderer
    {
        public static Renderer main;
        private readonly Mode mode = Mode.Diffusing;
        public bool SkyColor = true;
        private readonly HitableList world = new HitableList();
        public int Samples = 10240,MAX_SCATTER_TIME = 4;
        private int width = 512, height =512;
        private readonly Preview preview = new Preview();
        public float[] buff;
        public int[] Changes;

        public Camera camera;
        private float recip_width, recip_height;
        private int NowSample = 0;

        public void Init()
        {
            main = this;
            buff = new float[width * height * 4];
            Changes = new int[width * height];
            InitScene();


            Start();
            preview.Run(new DxConfiguration("Preview", width, height));
        }

        private void InitScene()
        {
            recip_width = 1f / width;
            recip_height = 1f / height;

            //Demo场景
           //Demo(); 
            //康奈尔盒子
           CornellBox();
        }


        void Demo()
        {
            var lens_radius = 0;
            var forcus_dis = 1;
            camera = new Camera(new Vector3(0, 1, 2), new Vector3(0, 0, 0), new Vector3(0, 1, 0), 90, (float)width / (float)height, lens_radius, forcus_dis, 0, 1);
            world.list.Add(new Sphere(new Vector3(-2, 10, 2), 4f, new DiffuseLight(new ConstantTexture(new Color32(1, 1, 1, 1)), 2)));//sun
            world.list.Add(new PlaneXZ(-5, 5, -5, 5, -0.5f, new Metal(new CheckerTexture(new ConstantTexture(new Color32(0.5f, 0.5f, 0.5f)), new ConstantTexture(Color32.white)), 0.2f)));
            world.list.Add(new Sphere(new Vector3(0,0,0),0.5f,new Metal(new ConstantTexture(Color32.white), 0.1f) ));
            world.list.Add(new Sphere(new Vector3(1,0,0),0.5f,new Lambertian(new ConstantTexture(Color32.red)) ));
            world.list.Add(new Sphere(new Vector3(-1,0,0),0.5f,new Lambertian(new CheckerTexture(new ConstantTexture(new Color32(0.5f,0.5f,0.5f)), new ConstantTexture(Color32.white)))) );
        
        }
        void CornellBox()
        {
            SkyColor = false;
            camera = new Camera(new Vector3(278, 278, -800), new Vector3(278, 278, 0), new Vector3(0, 1, 0), 40, (float)width / (float)height);
            var list=new List<Hitable>();
            list.Add(new FilpNormals(new PlaneYZ(0, 555, 0, 555, 555, new Lambertian(new ConstantTexture(Color32.green)))));
            list.Add(new PlaneYZ(0, 555, 0, 555, 0, new Lambertian(new ConstantTexture(Color32.red))));
            list.Add(new PlaneXZ(213, 343, 227, 332, 554,new DiffuseLight(new ConstantTexture(new Color32(1, 1, 1, 1)),15)));
            list.Add(new FilpNormals(new PlaneXZ(0, 555, 0, 555, 555, new Lambertian(new ConstantTexture(Color32.white)))));
            list.Add(new PlaneXZ(0, 555, 0, 555, 0, new Lambertian(new ConstantTexture(Color32.white))));
            list.Add(new FilpNormals(new PlaneXY(0, 555, 0, 555, 555, new Lambertian(new ConstantTexture(Color32.white)))));
            
           
            world.list.Add(new BVHNode(list.ToArray(), list.Count, 0, 1));
            world.list.Add(new Translate(new RotateY(
                    new Cube(
                        new Vector3(0, 0, 0),
                        new Vector3(165, 330, 165),
                        new Lambertian(new ConstantTexture(Color32.white))), 15)
                , new Vector3(265, 0, 295)));
            world.list.Add(new Translate(new RotateY(
                    new Cube(
                        new Vector3(0, 0, 0),
                        new Vector3(165, 165, 165),
                        new Lambertian(new ConstantTexture(Color32.white))), -18),
                new Vector3(130, 0, 65)));

        }

        private class ScannerConfig
        {
            public readonly int w,h;

            public ScannerConfig( int h, int w)
            {
                this.h = h;
                this.w = w;
            }
        }

        private async void Start()
        {
            ThreadPool.SetMaxThreads(16, 16);
            await Task.Factory.StartNew(delegate { LinearScanner(new ScannerConfig(height, width)); });
            for (var i = 1; i < Samples; i++)
                ThreadPool.QueueUserWorkItem(LinearScanner, new ScannerConfig(height, width));
        }

        private void LinearScanner(object o)
        {
            var config = (ScannerConfig)o;
            for (var j = config.h - 1; j >= 0; j--)
                for (var i = 0; i < config.w; i++)
                {
                    var color = mode == Mode.Diffusing
                        ? Diffusing(camera.CreateRay(
                            (i + Random.Get()) * recip_width,
                            (j + Random.Get()) * recip_height), world, 0)
                        : NormalMap(camera.CreateRay(
                            (i + Random.Get()) * recip_width,
                            (j + Random.Get()) * recip_height), world);
                    SetPixel(config.w-i-1, config.h - j-1,color);
                }
            Form1.main.BeginInvoke(new Action(() => { Form1.main.SetSPP();}));
        }

        private void SetPixel(int x, int y, Color32 c32)
        { 
            var i = width * 4 * y + x * 4;
            Changes[width * y + x]++;
            buff[i] += c32.r;
            buff[i + 1] += c32.g;
            buff[i + 2] += c32.b;
            buff[i + 3] += c32.a;
        }

        private Color32 NormalMap(Ray ray, HitableList hitableList)
        {
            var record = new HitRecord();
            if (hitableList.Hit(ray, 0f, float.MaxValue, ref record))
                return 0.5f * new Color32(record.normal.x + 1, record.normal.y + 1, record.normal.z + 1, 2f);
            var t = 0.5f * ray.normalDirection.y + 1f;
            return (1 - t) * new Color32(1, 1, 1) + t * new Color32(0.5f, 0.7f, 1);
        }

        private Color32 Diffusing(Ray ray, HitableList hitableList, int depth)
        {
            var record = new HitRecord();
            if (hitableList.Hit(ray, 0.0001f, float.MaxValue, ref record))
            {
                var r = new Ray(Vector3.zero, Vector3.zero);
                var attenuation = Color32.black;
                var emitted = record.material.emitted(record.u, record.v, record.p);
                if (depth >= MAX_SCATTER_TIME || !record.material.scatter(ray, record, ref attenuation, ref r))
                    return emitted;
                var c = Diffusing(r, hitableList, depth + 1);
                return new Color32(c.r * attenuation.r, c.g * attenuation.g, c.b * attenuation.b)+emitted;
            }
            var t = 0.5f * ray.normalDirection.y + 1f;
            return  SkyColor? (1 - t) * new Color32(2, 2, 2) + t * new Color32(0.5f, 0.7f, 1):Color32.black;
        }

        private enum Mode
        {
            NormalMap,
            Diffusing
        };

        public void Save()
        {
            var pic_buff = preview.Buff;
            var pic = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (var i = 0; i < pic_buff.Length; i+=4)
            {
                var c = Color.FromArgb(pic_buff[i+3], pic_buff[i], pic_buff[i+1], pic_buff[i+2]);
                pic.SetPixel(i % (width*4)/4, i / (width*4), c);
            }
            pic.Save("a.png");
        }
    }
}
