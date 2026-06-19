using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CornellBoxRay
{
    // =========================================================================
    // 1. МАТЕМАТИЧЕСКОЕ ЯДРО
    // =========================================================================

    public struct Vector3
    {
        public double X, Y, Z;

        public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.X, -a.Y, -a.Z);
        public static Vector3 operator *(Vector3 v, double s) => new Vector3(v.X * s, v.Y * s, v.Z * s);
        public static Vector3 operator *(double s, Vector3 v) => new Vector3(v.X * s, v.Y * s, v.Z * s);
        public static Vector3 operator *(Vector3 a, Vector3 b) => new Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        public static Vector3 operator /(Vector3 v, double s) => new Vector3(v.X / s, v.Y / s, v.Z / s);

        public double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector3 Normalize() { double l = Length(); return l > 1e-9 ? this / l : this; }
        public static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3 Reflect(Vector3 i, Vector3 n) => i - 2 * Dot(i, n) * n;

        public static Vector3 Zero => new Vector3(0, 0, 0);
    }

    public struct Ray
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public Ray(Vector3 o, Vector3 d) { Origin = o; Direction = d; }
    }

    // =========================================================================
    // 2. ОБЪЕКТЫ СЦЕНЫ
    // =========================================================================

    public class Material
    {
        public Vector3 Color;
        public double Diffuse = 0.7;
        public double Specular = 0.3;
        public double Shininess = 50.0;
        public double Reflection = 0.0;
        public double Transparency = 0.0;
        public double RefractiveIndex = 1.0;
        public Material(Vector3 color, double refl = 0, double transp = 0, double ior = 1.0)
        {
            Color = color;
            Reflection = refl;
            Transparency = transp;
            RefractiveIndex = ior;
        }
    }

    public abstract class SceneObject
    {
        public Material Material;
        public abstract double Intersect(Ray ray, out Vector3 hitPoint, out Vector3 normal);
    }

    public class Sphere : SceneObject
    {
        public Vector3 Center;
        public double Radius;

        public Sphere(Vector3 center, double radius, Material mat)
        {
            Center = center; Radius = radius; Material = mat;
        }

        public override double Intersect(Ray ray, out Vector3 hitPoint, out Vector3 normal)
        {
            hitPoint = Vector3.Zero; normal = Vector3.Zero;
            Vector3 oc = ray.Origin - Center;
            double a = Vector3.Dot(ray.Direction, ray.Direction);
            double b = 2.0 * Vector3.Dot(oc, ray.Direction);
            double c = Vector3.Dot(oc, oc) - Radius * Radius;
            double discriminant = b * b - 4 * a * c;

            if (discriminant < 0) return -1;

            double t = (-b - Math.Sqrt(discriminant)) / (2.0 * a);
            if (t < 0.001) t = (-b + Math.Sqrt(discriminant)) / (2.0 * a);
            if (t < 0.001) return -1;

            hitPoint = ray.Origin + ray.Direction * t;
            normal = (hitPoint - Center).Normalize();
            return t;
        }
    }

    public class Box : SceneObject
    {
        public Vector3 Min;
        public Vector3 Max;

        public Box(Vector3 min, Vector3 max, Material mat)
        {
            Min = min; Max = max; Material = mat;
        }

        public override double Intersect(Ray ray, out Vector3 hitPoint, out Vector3 normal)
        {
            hitPoint = Vector3.Zero; normal = Vector3.Zero;

            double t1 = (Min.X - ray.Origin.X) / ray.Direction.X;
            double t2 = (Max.X - ray.Origin.X) / ray.Direction.X;
            double t3 = (Min.Y - ray.Origin.Y) / ray.Direction.Y;
            double t4 = (Max.Y - ray.Origin.Y) / ray.Direction.Y;
            double t5 = (Min.Z - ray.Origin.Z) / ray.Direction.Z;
            double t6 = (Max.Z - ray.Origin.Z) / ray.Direction.Z;

            double tmin = Math.Max(Math.Max(Math.Min(t1, t2), Math.Min(t3, t4)), Math.Min(t5, t6));
            double tmax = Math.Min(Math.Min(Math.Max(t1, t2), Math.Max(t3, t4)), Math.Max(t5, t6));

            if (tmax < 0 || tmin > tmax) return -1;

            double t = tmin;
            if (t < 0.001) t = tmax;
            if (t < 0.001) return -1;

            hitPoint = ray.Origin + ray.Direction * t;

            Vector3 p = hitPoint;
            double bias = 0.0001;

            if (Math.Abs(p.X - Min.X) < bias) normal = new Vector3(-1, 0, 0);
            else if (Math.Abs(p.X - Max.X) < bias) normal = new Vector3(1, 0, 0);
            else if (Math.Abs(p.Y - Min.Y) < bias) normal = new Vector3(0, -1, 0);
            else if (Math.Abs(p.Y - Max.Y) < bias) normal = new Vector3(0, 1, 0);
            else if (Math.Abs(p.Z - Min.Z) < bias) normal = new Vector3(0, 0, -1);
            else if (Math.Abs(p.Z - Max.Z) < bias) normal = new Vector3(0, 0, 1);

            return t;
        }
    }

    public class Light
    {
        public Vector3 Position;
        public Vector3 Color;
        public double Intensity;
        public Light(Vector3 p, Vector3 c, double i) { Position = p; Color = c; Intensity = i; }
    }

    // =========================================================================
    // 3. ТРАССИРОВЩИК
    // =========================================================================

    public class RayTracer
    {
        public List<SceneObject> Objects = new List<SceneObject>();
        public List<Light> Lights = new List<Light>();
        public int MaxRecursion = 4;

        public Vector3 Trace(Ray ray, int depth)
        {
            if (depth <= 0) return Vector3.Zero;

            double closestT = double.MaxValue;
            SceneObject closestObj = null;
            Vector3 hitPoint = Vector3.Zero;
            Vector3 normal = Vector3.Zero;

            // Поиск ближайшего пересечения
            foreach (var obj in Objects)
            {
                Vector3 tempHit, tempNorm;
                double t = obj.Intersect(ray, out tempHit, out tempNorm);
                if (t > 0.001 && t < closestT)
                {
                    closestT = t;
                    closestObj = obj;
                    hitPoint = tempHit;
                    normal = tempNorm;
                }
            }

            if (closestObj == null) return Vector3.Zero;

            double transparency = closestObj.Material.Transparency;
            double reflection = closestObj.Material.Reflection;
            bool isTransparent = transparency > 0.0;

            Vector3 viewDir = (ray.Origin - hitPoint).Normalize();

            // Для непрозрачных – обычный ambient, для стекла – без ambient
            Vector3 finalColor = isTransparent ? Vector3.Zero : closestObj.Material.Color * 0.2;

            // ------------------------
            // ОСВЕЩЕНИЕ + МЯГКИЕ ТЕНИ
            // ------------------------
            foreach (var light in Lights)
            {
                Vector3 lightDir = (light.Position - hitPoint);
                double distToLight = lightDir.Length();
                lightDir = lightDir.Normalize();

                double lightFactor = 1.0;
                Ray shadowRay = new Ray(hitPoint + normal * 0.001, lightDir);

                foreach (var obj in Objects)
                {
                    Vector3 sHit, sNorm;
                    double t = obj.Intersect(shadowRay, out sHit, out sNorm);
                    if (t > 0.001 && t < distToLight)
                    {
                        double tr = obj.Material.Transparency;

                        if (tr <= 0.0)
                        {
                            lightFactor = 0.0;
                            break;
                        }
                        else
                        {
                            lightFactor *= tr;
                            if (lightFactor < 0.01)
                            {
                                lightFactor = 0.0;
                                break;
                            }
                        }
                    }
                }

                if (lightFactor > 0.0 && !isTransparent)
                {
                    double diff = Math.Max(0, Vector3.Dot(normal, lightDir));
                    Vector3 diffuse = closestObj.Material.Color * diff *
                                      light.Color * light.Intensity * lightFactor;

                    Vector3 reflectDirL = Vector3.Reflect(-lightDir, normal);
                    double spec = Math.Pow(Math.Max(0, Vector3.Dot(viewDir, reflectDirL)),
                                           closestObj.Material.Shininess);
                    Vector3 specular = light.Color * spec * light.Intensity *
                                       closestObj.Material.Specular * lightFactor;

                    finalColor += diffuse + specular;
                }
            }

            // Запоминаем «локальный» цвет (от освещения)
            Vector3 localColor = finalColor;


            // ------------------------
            // ПРОЗРАЧНОСТЬ (ПРЕЛОМЛЕНИЕ) + ФРЕНЕЛЬ
            // ------------------------
            if (transparency > 0.0)
            {
                Vector3 I = ray.Direction;            // падающий луч (нормализован)
                Vector3 N = normal;                   // нормаль в точке
                double n1 = 1.0;                      // воздух снаружи
                double n2 = closestObj.Material.RefractiveIndex <= 0
                            ? 1.0
                            : closestObj.Material.RefractiveIndex;

                // Нормаль должна смотреть ПРОТИВ направления луча
                if (Vector3.Dot(I, N) > 0)
                {
                    N = -N;
                    double tmp = n1; n1 = n2; n2 = tmp;
                }

                double eta = n1 / n2;
                double cosi = -Vector3.Dot(N, I);     // cos(theta_i) >= 0
                double sin2t = eta * eta * (1.0 - cosi * cosi);

                // Направление отражённого луча (есть всегда)
                Vector3 reflectDir = Vector3.Reflect(I, normal).Normalize();
                Vector3 reflectionColor = Trace(new Ray(hitPoint + normal * 0.001, reflectDir), depth - 1);

                if (sin2t > 1.0)
                {
                    // Полное внутреннее отражение: только отражённый свет
                    finalColor = localColor * (1.0 - transparency) +
                                 reflectionColor * transparency;
                }
                else
                {
                    double cost = Math.Sqrt(1.0 - sin2t);
                    Vector3 refractDir = (eta * I + (eta * cosi - cost) * N).Normalize();

                    Vector3 refractionColor = Trace(new Ray(hitPoint - N * 0.001, refractDir), depth - 1);

                    // Окрашиваем прошедший свет цветом материала (тонированное стекло)
                    refractionColor = refractionColor * closestObj.Material.Color;

                    // Френель (приближение Шлика)
                    // Френель (приближение Шлика)
                    double R0 = Math.Pow((n1 - n2) / (n1 + n2), 2.0);
                    double fresnel = R0 + (1.0 - R0) * Math.Pow(1.0 - cosi, 5.0);

                    // Используем Material.Reflection как "усиление" отражения
                    // 0 -> обычное стекло, 1 -> максимально зеркальное стекло
                    double fresnelEff = fresnel + reflection * (1.0 - fresnel);
                    fresnelEff = Math.Max(0.0, Math.Min(1.0, fresnelEff)); // clamp на всякий случай

                    double localWeight = 1.0 - transparency;
                    double reflWeight = transparency * fresnelEff;
                    double refrWeight = transparency * (1.0 - fresnelEff);

                    finalColor = localColor * localWeight +
                                 reflectionColor * reflWeight +
                                 refractionColor * refrWeight;
                }
            }
            // ------------------------
            // ОТРАЖЕНИЕ ДЛЯ НЕПРОЗРАЧНЫХ ОБЪЕКТОВ
            // ------------------------
            else if (reflection > 0.0)
            {
                Vector3 reflectDir = Vector3.Reflect(ray.Direction, normal).Normalize();
                Ray reflectRay = new Ray(hitPoint + normal * 0.001, reflectDir);
                Vector3 reflectedColor = Trace(reflectRay, depth - 1);

                finalColor = localColor * (1.0 - reflection) + reflectedColor * reflection;
            }

            return finalColor;
        }
    }

    // =========================================================================
    // 4. ИНТЕРФЕЙС (MAIN FORM)
    // =========================================================================

    public partial class MainForm : Form
    {
        private RayTracer tracer;
        private PictureBox canvas;
        private Panel controlsPanel;

        // UI Элементы
        private CheckBox cbMirrorLeft, cbMirrorRight, cbMirrorBack, cbMirrorFront, cbMirrorFloor, cbMirrorCeil;
        private CheckBox cbObjSphereMirror, cbObjSphereTransp;
        private CheckBox cbObjCubeBlueMirror, cbObjCubeBlueTransp;
        private CheckBox cbObjCubeTallMirror, cbObjCubeTallTransp;
        private TrackBar tbLightX, tbLightY, tbLightZ;
        private Button btnRender;
        private Label lblStatus;

        public MainForm()
        {
            InitializeComponent();
            tracer = new RayTracer();
        }

        private void InitializeComponent()
        {
            this.Text = "Cornell Box Ray Tracer (Corrected View)";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. Сначала добавляем ПАНЕЛЬ УПРАВЛЕНИЯ СПРАВА
            controlsPanel = new Panel { Dock = DockStyle.Right, Width = 280, BackColor = Color.WhiteSmoke, AutoScroll = true };
            this.Controls.Add(controlsPanel);

            // 2. Затем добавляем КАНВАС (Заполняет оставшееся пространство)
            // Важно: Порядок добавления в Controls.Add влияет на Docking. 
            // PictureBox теперь займет только то место, которое осталось слева от панели.
            canvas = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Normal };
            this.Controls.Add(canvas);

            // Чтобы убедиться, что канвас не перекрыт, выводим его на передний план в его контейнере
            canvas.BringToFront();

            SetupControls();
        }

        private void SetupControls()
        {
            int y = 10;
            int x = 10;

            btnRender = new Button { Text = "РЕНДЕР", Location = new Point(x, y), Width = 240, Height = 40, BackColor = Color.SteelBlue, ForeColor = Color.White, Font = new Font("Arial", 12, FontStyle.Bold) };
            btnRender.Click += (s, e) => Render();
            controlsPanel.Controls.Add(btnRender);
            y += 50;

            lblStatus = new Label { Text = "Нажмите Рендер", Location = new Point(x, y), AutoSize = true, ForeColor = Color.DarkGray };
            controlsPanel.Controls.Add(lblStatus);
            y += 20;

            GroupBox gbWalls = new GroupBox { Text = "Стены (Зеркальность)", Location = new Point(x, y), Size = new Size(240, 160) };
            controlsPanel.Controls.Add(gbWalls);
            cbMirrorLeft = AddCheck(gbWalls, "Левая (Красная)", 20, 20);
            cbMirrorRight = AddCheck(gbWalls, "Правая (Синяя)", 20, 40);
            cbMirrorBack = AddCheck(gbWalls, "Задняя (Дальняя)", 20, 60);
            cbMirrorFront = AddCheck(gbWalls, "Передняя (За камерой)", 20, 80);
            cbMirrorFloor = AddCheck(gbWalls, "Пол", 20, 100);
            cbMirrorCeil = AddCheck(gbWalls, "Потолок", 20, 120);
            y += 170;

            GroupBox gbObjs = new GroupBox { Text = "Объекты", Location = new Point(x, y), Size = new Size(240, 220) };
            controlsPanel.Controls.Add(gbObjs);
            int oy = 20;
            AddLabel(gbObjs, "Зеленый Шар:", 10, oy); oy += 20;
            cbObjSphereMirror = AddCheck(gbObjs, "Зеркало", 20, oy);
            cbObjSphereTransp = AddCheck(gbObjs, "Прозрачность", 120, oy); oy += 30;

            AddLabel(gbObjs, "Синий Куб (низкий):", 10, oy); oy += 20;
            cbObjCubeBlueMirror = AddCheck(gbObjs, "Зеркало", 20, oy);
            cbObjCubeBlueTransp = AddCheck(gbObjs, "Прозрачность", 120, oy); oy += 30;

            AddLabel(gbObjs, "Желтый Куб (высокий):", 10, oy); oy += 20;
            cbObjCubeTallMirror = AddCheck(gbObjs, "Зеркало", 20, oy);
            cbObjCubeTallTransp = AddCheck(gbObjs, "Прозрачность", 120, oy); oy += 30;
            y += 230;

            GroupBox gbLight = new GroupBox { Text = "Доп. Свет (Положение)", Location = new Point(x, y), Size = new Size(240, 150) };
            controlsPanel.Controls.Add(gbLight);
            AddLabel(gbLight, "X:", 10, 25);
            tbLightX = AddTrack(gbLight, 30, 20, -40, 40, -10);
            AddLabel(gbLight, "Y:", 10, 65);
            tbLightY = AddTrack(gbLight, 30, 60, -40, 40, 0);
            AddLabel(gbLight, "Z:", 10, 105);
            tbLightZ = AddTrack(gbLight, 30, 100, -40, 40, -10);
        }

        private CheckBox AddCheck(Control parent, string text, int x, int y)
        {
            CheckBox cb = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true };
            parent.Controls.Add(cb);
            return cb;
        }
        private void AddLabel(Control parent, string text, int x, int y)
        {
            Label l = new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Arial", 8, FontStyle.Bold) };
            parent.Controls.Add(l);
        }
        private TrackBar AddTrack(Control parent, int x, int y, int min, int max, int val)
        {
            TrackBar tb = new TrackBar { Location = new Point(x, y), Size = new Size(200, 30), Minimum = min, Maximum = max, Value = val, TickStyle = TickStyle.None };
            parent.Controls.Add(tb);
            return tb;
        }

        // =========================================================================
        // ПОСТРОЕНИЕ СЦЕНЫ
        // =========================================================================

        private void BuildScene()
        {
            tracer.Objects.Clear();
            tracer.Lights.Clear();

            double s2 = 5.0;

            // Материалы
            Material mLeft = cbMirrorLeft.Checked ? new Material(new Vector3(0.9, 0.9, 0.9), 0.9) : new Material(new Vector3(0.8, 0.1, 0.1));
            Material mRight = cbMirrorRight.Checked ? new Material(new Vector3(0.9, 0.9, 0.9), 0.9) : new Material(new Vector3(0.1, 0.1, 0.8));
            Material mFloor = cbMirrorFloor.Checked ? new Material(new Vector3(0.9, 0.9, 0.9), 0.9) : new Material(new Vector3(0.9, 0.9, 0.9));
            Material mCeil = cbMirrorCeil.Checked ? new Material(new Vector3(0.9, 0.9, 0.9), 0.9) : new Material(new Vector3(0.9, 0.9, 0.9));
            Material mBack = cbMirrorBack.Checked ? new Material(new Vector3(0.9, 0.9, 0.9), 0.9) : new Material(new Vector3(0.9, 0.9, 0.9));
            Material mFront = cbMirrorFront.Checked ? new Material(new Vector3(0.9, 0.9, 0.9), 0.9) : new Material(new Vector3(0.9, 0.9, 0.9));

            // Геометрия Стен (Z растянут до -20)
            tracer.Objects.Add(new Box(new Vector3(-s2 - 0.1, -s2, -20.0), new Vector3(-s2, s2, s2), mLeft)); // Левая
            tracer.Objects.Add(new Box(new Vector3(s2, -s2, -20.0), new Vector3(s2 + 0.1, s2, s2), mRight)); // Правая
            tracer.Objects.Add(new Box(new Vector3(-s2, -s2 - 0.1, -20.0), new Vector3(s2, -s2, s2), mFloor)); // Пол
            tracer.Objects.Add(new Box(new Vector3(-s2, s2, -20.0), new Vector3(s2, s2 + 0.1, s2), mCeil)); // Потолок
            tracer.Objects.Add(new Box(new Vector3(-s2, -s2, s2), new Vector3(s2, s2, s2 + 0.1), mBack)); // Задняя
            tracer.Objects.Add(new Box(new Vector3(-s2, -s2, -20.1), new Vector3(s2, s2, -20.0), mFront)); // Передняя (за камерой)

            // Объекты
            Material mBlueCube = new Material(new Vector3(0.2, 0.2, 0.9));
            if (cbObjCubeBlueMirror.Checked)
            {
                mBlueCube.Color = new Vector3(0.1, 0.1, 0.1);
                mBlueCube.Reflection = 0.8;
            }
            if (cbObjCubeBlueTransp.Checked)
            {
                mBlueCube.Color = new Vector3(0.9, 0.9, 1.0);
                mBlueCube.Transparency = 0.8;
                mBlueCube.RefractiveIndex = 1.5; // стекло
            }
            tracer.Objects.Add(new Box(new Vector3(-4.0, -5.0, -1.5), new Vector3(-1.0, -2.0, 1.5), mBlueCube));

            Material mSphere = new Material(new Vector3(0.1, 0.8, 0.1));
            if (cbObjSphereMirror.Checked)
            {
                mSphere.Color = new Vector3(0.1, 0.1, 0.1);
                mSphere.Reflection = 0.85;
            }
            if (cbObjSphereTransp.Checked)
            {
                mSphere.Color = new Vector3(0.8, 1.0, 0.8);
                mSphere.Transparency = 0.85;
                mSphere.RefractiveIndex = 1.5; // стекло
            }
            tracer.Objects.Add(new Sphere(new Vector3(-2.5, -0.7, 0.0), 1.3, mSphere));

            Material mYellowCube = new Material(new Vector3(0.9, 0.8, 0.1));
            if (cbObjCubeTallMirror.Checked)
            {
                mYellowCube.Color = new Vector3(0.1, 0.1, 0.1);
                mYellowCube.Reflection = 0.8;
            }
            if (cbObjCubeTallTransp.Checked)
            {
                mYellowCube.Color = new Vector3(1.0, 1.0, 0.8);
                mYellowCube.Transparency = 0.8;
                mYellowCube.RefractiveIndex = 1.5; // стекло
            }
            tracer.Objects.Add(new Box(new Vector3(1.5, -5.0, -2.0), new Vector3(4.5, 2.0, 1.0), mYellowCube));

            // Свет
            tracer.Objects.Add(new Box(new Vector3(-1.5, 4.95, -1.5), new Vector3(1.5, 5.0, 1.5), new Material(new Vector3(1, 1, 1)) { Specular = 0 }));
            tracer.Lights.Add(new Light(new Vector3(0, 4.0, 0), new Vector3(1, 1, 1), 0.7));
            Vector3 extraPos = new Vector3(tbLightX.Value / 10.0, tbLightY.Value / 10.0, tbLightZ.Value / 10.0);
            tracer.Lights.Add(new Light(extraPos, new Vector3(1.0, 0.8, 0.5), 0.5));
        }

        private async void Render()
        {
            btnRender.Enabled = false;
            lblStatus.Text = "Рендеринг...";
            lblStatus.ForeColor = Color.Red;

            BuildScene();

            // ВАЖНО: Берем размеры КАНВАСА, а не всей формы. 
            // Канвас занимает только то место, которое осталось после Панели.
            int w = canvas.Width;
            int h = canvas.Height;

            // Если слишком маленькое окно
            if (w < 10) w = 10;
            if (h < 10) h = 10;

            // Ограничение разрешения для производительности (опционально)
            if (w > 1200) { double r = (double)w / h; w = 1200; h = (int)(w / r); }

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);

            await Task.Run(() =>
            {
                Rectangle rect = new Rectangle(0, 0, w, h);
                BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
                int bytesPerPixel = 3;
                int stride = bmpData.Stride;
                IntPtr ptr = bmpData.Scan0;
                int totalBytes = stride * h;
                byte[] rgbValues = new byte[totalBytes];

                // --- НАСТРОЙКИ КАМЕРЫ (МЕНЯТЬ ЗДЕСЬ) ---
                // Camera Shift X: Положительное число сдвигает камеру ВПРАВО, Отрицательное - ВЛЕВО
                double cameraShiftX = 0.0; // Попробуйте 0.0, если все равно криво - ставьте 0.5

                double camZ = -14.0;
                double roomZ = -5.0;
                double dist = Math.Abs(roomZ - camZ); // 9.0

                // Множитель поля зрения (FOV)
                double fovScaleY = 5.0 / dist;

                // Позиция камеры (с учетом сдвига)
                Vector3 cameraPos = new Vector3(cameraShiftX, 0, camZ);

                // Аспект ратио канваса (отношение ширины к высоте)
                double aspect = (double)w / h;

                Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Нормализация координат экрана
                        // Центр (0,0) будет ровно в центре PictureBox
                        double u = (2.0 * (x + 0.5) / w - 1.0) * aspect;
                        double v = (1.0 - 2.0 * (y + 0.5) / h);

                        // Направление луча. 
                        // Если вы сдвинули камеру (cameraPos.X), то лучи все равно летят параллельно оси Z, 
                        // создавая эффект сдвига "головы" вправо.
                        Vector3 dir = new Vector3(u * fovScaleY, v * fovScaleY, 1.0).Normalize();

                        Ray ray = new Ray(cameraPos, dir);
                        Vector3 col = tracer.Trace(ray, tracer.MaxRecursion);

                        col = new Vector3(Math.Min(1, col.X), Math.Min(1, col.Y), Math.Min(1, col.Z));

                        int index = y * stride + x * bytesPerPixel;
                        rgbValues[index] = (byte)(col.Z * 255);
                        rgbValues[index + 1] = (byte)(col.Y * 255);
                        rgbValues[index + 2] = (byte)(col.X * 255);
                    }
                });

                System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, totalBytes);
                bmp.UnlockBits(bmpData);
            });

            canvas.Image = bmp;
            lblStatus.Text = "Готово!";
            lblStatus.ForeColor = Color.Green;
            btnRender.Enabled = true;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}