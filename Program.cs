using System.Runtime.InteropServices;
using System.Globalization;
using System.Diagnostics;

class Program
{
    const int TILE = 64;

    const int TILE_SIZE = 64;

    static int tilesX = (WIDTH + TILE - 1) / TILE;
    static int tilesY = (HEIGHT + TILE - 1) / TILE;

    // --- размеры ---
    static int WIDTH = 800;
    static int HEIGHT = 600;
    const float SCALE = 100;
    static float FOV = 90f;
    const int WM_SIZE = 0x0005;
    static float light_int = 1f;

    static List<ScreenVertex> ProjectAll(List<Vector3> verts)
    {
        var result = new List<ScreenVertex>(verts.Count);
        float f = GetFocalLength();

        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];

            float x = v.X * f / v.Z + WIDTH / 2f;
            float y = -v.Y * f / v.Z + HEIGHT / 2f;

            result.Add(new ScreenVertex(x, y, v.Z));
        }

        return result;
    }

    //static public List<Vector3> verts = new List<Vector3>();

    static Dictionary<string, Material> materials = new();
    static string currentMaterial = "default";
    static List<string> triangleMaterials = new();
    static List<Vector3> originalVertices = new();
    static List<(int, int)> edges = new();
    static List<(int, int, int)> triangles = new();

    // --- UI ---
    static Slider posX = new Slider { X = 20, Y = 40, W = 200, H = 10, Min = -10, Max = 10 };
    static Slider posY = new Slider { X = 20, Y = 60, W = 200, H = 10, Min = -10, Max = 10 };
    static Slider posZ = new Slider { X = 20, Y = 80, W = 200, H = 10, Min = -10, Max = 10 };

    static Slider rotX = new Slider { X = 20, Y = 120, W = 200, H = 10, Min = -3.14f, Max = 3.14f };
    static Slider rotY = new Slider { X = 20, Y = 140, W = 200, H = 10, Min = -3.14f, Max = 3.14f };
    static Slider rotZ = new Slider { X = 20, Y = 160, W = 200, H = 10, Min = -3.14f, Max = 3.14f };

    static Slider scaleX = new Slider { X = 20, Y = 340, W = 200, H = 10, Min = 0.1f, Max = 5f, Value = 1f };
    static Slider scaleY = new Slider { X = 20, Y = 360, W = 200, H = 10, Min = 0.1f, Max = 5f, Value = 1f };
    static Slider scaleZ = new Slider { X = 20, Y = 380, W = 200, H = 10, Min = 0.1f, Max = 5f, Value = 1f };

    static Checkbox wireframeToggle = new Checkbox { X = 20, Y = 180, Size = 16 };
    static Checkbox noFillToggle = new Checkbox { X = 20, Y = 210, Size = 16 };

    static Button loadObjBtn = new Button
    {
        X = 20,
        Y = 260,
        W = 120,
        H = 30,
        Text = "LOAD OBJ",
        OnClick = () =>
        {
            string path = OpenFile("OBJ Files\0*.obj\0");

            if (path != null)
            {
                Parse(path, out originalVertices, out edges, out triangles);
            }
        }
    };

    static Button loadTexBtn = new Button
    {
        X = 20,
        Y = 300,
        W = 120,
        H = 30,
        Text = "LOAD TEX",
        OnClick = () =>
        {
            string path = OpenFile("Image Files\0*.bmp;*.png\0");

            if (!File.Exists(path))
            {
                Console.WriteLine($"Texture not found: {path}");
                return;
            }

            if (path != null)
            {
                LoadTexture(path);
            }
        }
    };

    static Button resetOffsetBtn = new Button
    {
        X = WIDTH - 140,
        Y = 40,
        W = 120,
        H = 30,
        Text = "RESET POS",
        OnClick = () =>
        {
            posX.Value = 0;
            posY.Value = 0;
            posZ.Value = 0;
        }
    };

    static Button resetRotBtn = new Button
    {
        X = WIDTH - 140,
        Y = 80,
        W = 120,
        H = 30,
        Text = "RESET ROT",
        OnClick = () =>
        {
            rotX.Value = 0;
            rotY.Value = 0;
            rotZ.Value = 0;

            rotX_accum = 0;
            rotY_accum = 0;
            rotZ_accum = 0;
        }
    };

    static Button resetTexBtn = new Button
    {
        X = WIDTH - 140,
        Y = 120,
        W = 120,
        H = 30,
        Text = "RESET TEX",
        OnClick = () =>
        {
            texture = null;
            texW = texH = 0;

            foreach (var m in materials.Values)
            {
                m.Texture = null;
            }
        }
    };

    static Dictionary<char, byte[]> font = new Dictionary<char, byte[]>
    {
        [' '] = new byte[] { 0, 0, 0, 0, 0 },

        ['0'] = new byte[] { 0x3E, 0x51, 0x49, 0x45, 0x3E },
        ['1'] = new byte[] { 0x00, 0x42, 0x7F, 0x40, 0x00 },
        ['2'] = new byte[] { 0x62, 0x51, 0x49, 0x49, 0x46 },
        ['3'] = new byte[] { 0x22, 0x49, 0x49, 0x49, 0x36 },
        ['4'] = new byte[] { 0x18, 0x14, 0x12, 0x7F, 0x10 },
        ['5'] = new byte[] { 0x2F, 0x49, 0x49, 0x49, 0x31 },
        ['6'] = new byte[] { 0x3E, 0x49, 0x49, 0x49, 0x32 },
        ['7'] = new byte[] { 0x01, 0x71, 0x09, 0x05, 0x03 },
        ['8'] = new byte[] { 0x36, 0x49, 0x49, 0x49, 0x36 },
        ['9'] = new byte[] { 0x26, 0x49, 0x49, 0x49, 0x3E },

        ['A'] = new byte[] { 0x7E, 0x11, 0x11, 0x11, 0x7E },
        ['B'] = new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x36 },
        ['C'] = new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x22 },
        ['D'] = new byte[] { 0x7F, 0x41, 0x41, 0x22, 0x1C },
        ['E'] = new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x41 },
        ['F'] = new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x01 },
        ['G'] = new byte[] { 0x3E, 0x41, 0x49, 0x49, 0x7A },
        ['H'] = new byte[] { 0x7F, 0x08, 0x08, 0x08, 0x7F },
        ['I'] = new byte[] { 0x00, 0x41, 0x7F, 0x41, 0x00 },
        ['J'] = new byte[] { 0x20, 0x40, 0x41, 0x3F, 0x01 },
        ['K'] = new byte[] { 0x7F, 0x08, 0x14, 0x22, 0x41 },
        ['L'] = new byte[] { 0x7F, 0x40, 0x40, 0x40, 0x40 },
        ['M'] = new byte[] { 0x7F, 0x02, 0x0C, 0x02, 0x7F },
        ['N'] = new byte[] { 0x7F, 0x04, 0x08, 0x10, 0x7F },
        ['O'] = new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x3E },
        ['P'] = new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x06 },
        ['Q'] = new byte[] { 0x3E, 0x41, 0x51, 0x21, 0x5E },
        ['R'] = new byte[] { 0x7F, 0x09, 0x19, 0x29, 0x46 },
        ['S'] = new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 },
        ['T'] = new byte[] { 0x01, 0x01, 0x7F, 0x01, 0x01 },
        ['U'] = new byte[] { 0x3F, 0x40, 0x40, 0x40, 0x3F },
        ['V'] = new byte[] { 0x1F, 0x20, 0x40, 0x20, 0x1F },
        ['W'] = new byte[] { 0x7F, 0x20, 0x18, 0x20, 0x7F },
        ['X'] = new byte[] { 0x63, 0x14, 0x08, 0x14, 0x63 },
        ['Y'] = new byte[] { 0x03, 0x04, 0x78, 0x04, 0x03 },
        ['Z'] = new byte[] { 0x61, 0x51, 0x49, 0x45, 0x43 },

        [':'] = new byte[] { 0x00, 0x36, 0x36, 0x00, 0x00 },
        ['.'] = new byte[] { 0x00, 0x60, 0x60, 0x00, 0x00 },
        ['-'] = new byte[] { 0x08, 0x08, 0x08, 0x08, 0x08 },
    };

    // -- camera --
    static Vector3 camPos = new Vector3(0, 0, -5);
    static float camRotX = 0;
    static float camRotY = 0;
    static float camRotZ = 0;

    static HashSet<int> keys = new HashSet<int>();

    // -- lights --
    //static Vector3 lightDir = Normalize(new Vector3(0f, 0f, 1f));
    static float lightFOV = 90f;

    static Vector3 lightPos = new Vector3(0, 0, -5);
    static float lightRotX = 0.0f;
    static float lightRotY = 0.0f;

    static int SHADOW_W = 4096*4;
    static int SHADOW_H = 4096*4;

    static float[] shadowMap = new float[SHADOW_W * SHADOW_H];


    // -- object state ---
    static float rotX_accum = 0;
    static float rotY_accum = 0;
    static float rotZ_accum = 0;

    static float prevRotX = 0;
    static float prevRotY = 0;
    static float prevRotZ = 0;

    // --- framebuffer ---
    static int[] buffer = new int[WIDTH * HEIGHT];

    static float[] zbuffer = new float[WIDTH * HEIGHT];

    static (float, float)[] samples = new (float, float)[]
    {
        (0.25f, 0.25f),
        (0.75f, 0.25f),
        (0.25f, 0.75f),
        (0.75f, 0.75f)
    };

    // --- WinAPI ---
    const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    const int CW_USEDEFAULT = unchecked((int)0x80000000);
    const int WM_DESTROY = 0x0002;
    const int WM_MOUSEMOVE = 0x0200;
    const int WM_LBUTTONDOWN = 0x0201;
    const int WM_LBUTTONUP = 0x0202;

    static int mouseX, mouseY;
    static bool mouseDown;
    static bool mousePressed;   // только в этот кадр
    static bool mouseReleased;

    static WndProcDelegate wndProcDelegate;

    [StructLayout(LayoutKind.Sequential)]
    struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;

        public string lpstrFile; // <-- ВАЖНО: теперь pointer
        public int nMaxFile;

        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;

        public string lpstrInitialDir;
        public string lpstrTitle;

        public int Flags;

        public short nFileOffset;
        public short nFileExtension;

        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
    }

    public struct Vector3
    {
        public float X, Y, Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    struct ScreenVertex
    {
        public float X, Y, Z;

        public ScreenVertex(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    class Button
    {
        public int X, Y, W, H;
        public string Text;
        public Action OnClick;
    }

    class Slider
    {
        public int X, Y, W, H;
        public float Min, Max;
        public float Value;

        public bool Dragging = false;
    }

    class Checkbox
    {
        public int X, Y, Size;
        public bool Checked;
    }

    struct UV
    {
        public float U, V;
        public UV(float u, float v)
        {
            U = u;
            V = v;
        }
    }

    class Material
    {
        public string Name;

        public uint DiffuseColor = 0xFFFFFFFF; // fallback white
        public int[] Texture;
        public int TexW, TexH;

        public bool HasTexture => Texture != null;
    }

    static List<UV> uvs = new();
    static List<(int, int, int)> triangleUVs = new();

    static int[] texture;
    static int texW, texH;

    delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll")]
    static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage(ref MSG lpMsg);


    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
    static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("user32.dll")]
    static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("gdi32.dll")]
    static extern int StretchDIBits(
        IntPtr hdc,
        int xDest, int yDest, int DestWidth, int DestHeight,
        int xSrc, int ySrc, int SrcWidth, int SrcHeight,
        int[] lpBits,
        ref BITMAPINFO lpbmi,
        uint iUsage,
        uint dwRop
    );

    const uint SRCCOPY = 0x00CC0020;
    const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    static BITMAPINFO bmi;

    static IntPtr hwnd;
    static IntPtr hdc;

    // --- цвета ---
    const int BLACK = unchecked((int)0xFF000000);
    const int GREEN = unchecked((int)0xFF00FF00);

    static bool KeyDown(int key)
    {
        return (GetAsyncKeyState(key) & 0x8000) != 0;
    }

    static string OpenFile(string filter)
    {
        var ofn = new OPENFILENAME();

        ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
        ofn.lpstrFile = new string('\0', 1024);
        ofn.nMaxFile = ofn.lpstrFile.Length;

        ofn.lpstrFilter = filter;
        ofn.lpstrTitle = "Select file";
        ofn.Flags = 0x00080000 | 0x00001000 | 0x00000008;

        if (GetOpenFileName(ref ofn))
        {
            // 🔥 ВАЖНО: обрезаем по первому \0
            return ofn.lpstrFile.Split('\0')[0];
        }

        return null;
    }

    static private void Main()
    {
        // bitmap info
        bmi = new BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        bmi.bmiHeader.biWidth = WIDTH;
        bmi.bmiHeader.biHeight = -HEIGHT; // top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0;

        var wc = new WNDCLASS();
        wndProcDelegate = new WndProcDelegate(WndProc);
        wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
        wc.lpszClassName = "SoftwareRenderer";

        RegisterClass(ref wc);

        hwnd = CreateWindowEx(
            0, wc.lpszClassName, "3DEngine",
            WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT, CW_USEDEFAULT,
            WIDTH, HEIGHT,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero
        );

        //Parse(@"C://Users/tuktu/source/repos/3D-Engine/model.obj", out var originalVertices, out var edges, out var triangles);
        //LoadTexture("C://Users/tuktu/source/repos/3D-Engine/texture.bmp");

        ShowWindow(hwnd, 1);
        UpdateWindow(hwnd);

        hdc = GetDC(hwnd);

        // --- main loop ---
        MSG msg;

        Stopwatch timer = Stopwatch.StartNew();
        int frames = 0;
        double lastTime = 0;
        double lastFrameTime = 0;


        while (true)
        {
            float dX = rotX.Value - prevRotX;
            float dY = rotY.Value - prevRotY;
            float dZ = rotZ.Value - prevRotZ;

            prevRotX = rotX.Value;
            prevRotY = rotY.Value;
            prevRotZ = rotZ.Value;

            rotX_accum += dX;
            rotY_accum += dY;
            rotZ_accum += dZ;

            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, 1))
            {
                if (msg.message == WM_DESTROY) return;
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            float deltaTime = (float)(timer.Elapsed.TotalSeconds - lastFrameTime);
            lastFrameTime = timer.Elapsed.TotalSeconds;

            var world = new List<Vector3>(originalVertices);
            world = scale(scaleX.Value, scaleY.Value, scaleZ.Value, world);
            world = rotate(rotX_accum, rotY_accum, rotZ_accum, world, 0, 0, 0);
            world = offset(
                posX.Value / SCALE,
                posY.Value / SCALE,
                posZ.Value / SCALE,
                world
            );

            // копия для рендера
            var view = ApplyCamera(world);

            float speed = 1f * deltaTime;
            float rotSpeed = -0.5f * deltaTime;

            var forward = GetForward();
            var right = GetRight();

            Vector3 move = new Vector3(0, 0, 0);

            if (KeyDown(0x57)) move = Add(move, forward);
            if (KeyDown(0x53)) move = Sub(move, forward);
            if (KeyDown(0x44)) move = Add(move, right);
            if (KeyDown(0x41)) move = Sub(move, right);

            if (move.X != 0 || move.Y != 0 || move.Z != 0)
            {
                move = Normalize(move);

                camPos.X += move.X * speed;
                camPos.Y += move.Y * speed;
                camPos.Z += move.Z * speed;
            }

            // --- движение ---
            if (KeyDown(0x57)) // W
            {
                camPos.X += forward.X * speed;
                camPos.Y += forward.Y * speed;
                camPos.Z += forward.Z * speed;
            }

            if (KeyDown(0x53)) // S
            {
                camPos.X -= forward.X * speed;
                camPos.Y -= forward.Y * speed;
                camPos.Z -= forward.Z * speed;
            }

            if (KeyDown(0x44)) // D
            {
                camPos.X += right.X * speed;
                camPos.Z += right.Z * speed;
            }

            if (KeyDown(0x41)) // A
            {
                camPos.X -= right.X * speed;
                camPos.Z -= right.Z * speed;
            }

            // вверх/вниз
            if (KeyDown(0x20)) camPos.Y += speed; // SPACE
            if (KeyDown(0x11)) camPos.Y -= speed; // CTRL

            if (KeyDown(0x26)) camRotX += rotSpeed; // ↑
            if (KeyDown(0x28)) camRotX -= rotSpeed; // ↓
            if (KeyDown(0x25)) camRotY += rotSpeed; // ←
            if (KeyDown(0x27)) camRotY -= rotSpeed; // →

            Render(world, view, edges, triangles, (int)(1 / deltaTime));

            keys.Clear();

            frames++;

            double currentTime = timer.Elapsed.TotalSeconds;

            if (currentTime - lastTime >= 1.0)
            {
                if ((currentTime - lastTime) != 0)
                {
                    double fps = frames / (currentTime - lastTime);

                    if (double.IsNaN(fps) || double.IsInfinity(fps))
                        fps = 0;
                    if (double.IsNaN(currentTime) || double.IsInfinity(currentTime))
                        throw new Exception("TIME CORRUPTION");

                    string title = $"3DEngine | FPS: {fps:F1}";
                    SetWindowText(hwnd, title);

                    if (frames < 0 || frames > 1_000_000)
                        throw new Exception("FRAME COUNTER CORRUPTED");

                    frames = 0;
                    lastTime = currentTime;
                }
            }
        }
    }

    static Vector3 Add(Vector3 a, Vector3 b)
    => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    static Vector3 Sub(Vector3 a, Vector3 b)
        => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    static Vector3 GetLightDir()
    {
        float cosX = (float)Math.Cos(lightRotX);
        float sinX = (float)Math.Sin(lightRotX);

        float cosY = (float)Math.Cos(lightRotY);
        float sinY = (float)Math.Sin(lightRotY);

        Vector3 dir = new Vector3(0, 0, 1);

        // rotate Y
        float x1 = dir.X * cosY + dir.Z * sinY;
        float z1 = -dir.X * sinY + dir.Z * cosY;

        // rotate X
        float y2 = dir.Y * cosX - z1 * sinX;
        float z2 = dir.Y * sinX + z1 * cosX;

        return Normalize(new Vector3(x1, y2, z2));
    }

    static void Resize(int w, int h)
    {
        WIDTH = w;
        HEIGHT = h;

        tilesX = (WIDTH + TILE - 1) / TILE;
        tilesY = (HEIGHT + TILE - 1) / TILE;

        buffer = new int[WIDTH * HEIGHT];
        zbuffer = new float[WIDTH * HEIGHT];

        bmi.bmiHeader.biWidth = WIDTH;
        bmi.bmiHeader.biHeight = -HEIGHT;
    }

    static IntPtr WndProc(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_DESTROY)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        if (msg == WM_SIZE)
        {
            int w = (short)(lParam.ToInt32() & 0xffff);
            int h = (short)((lParam.ToInt32() >> 16) & 0xffff);

            if (w > 0 && h > 0)
            {
                Resize(w, h);
            }
        }

        if (msg == WM_MOUSEMOVE)
        {
            mouseX = (short)(lParam.ToInt32() & 0xffff);
            mouseY = (short)((lParam.ToInt32() >> 16) & 0xffff);
        }

        if (msg == WM_LBUTTONDOWN)
        {
            mouseDown = true;
            mousePressed = true;
        }

        if (msg == WM_LBUTTONUP)
        {
            mouseDown = false;
            mouseReleased = true;
        }

        // ВСЁ остальное — системе
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // --- ENGINE API ---

    static void LoadMTL(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (!File.Exists(path))
        {
            Console.WriteLine($"[MTL] File not found: {path}");
            return;
        }

        Material mat = null;

        foreach (var lineRaw in File.ReadLines(path))
        {
            var line = lineRaw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "newmtl":
                    mat = new Material { Name = parts[1] };
                    materials[mat.Name] = mat;
                    break;

                case "Kd":
                    if (mat != null)
                    {
                        float r = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float g = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float b = float.Parse(parts[3], CultureInfo.InvariantCulture);

                        mat.DiffuseColor = (uint)((255 << 24) | ((int)(r * 255) << 16) | ((int)(g * 255) << 8) | (int)(b * 255));
                    }
                    break;

                case "map_Kd":
                    if (mat != null)
                    {
                        string texPath = line.Substring(7).Trim(); // вместо parts[1]

                        if (!Path.IsPathRooted(texPath))
                            texPath = Path.Combine(Path.GetDirectoryName(path), texPath);

                        texPath = Path.GetFullPath(texPath);

                        Console.WriteLine($"Loading texture: {texPath}");

                        LoadMaterialTexture(mat, texPath);
                    }
                    break;
            }
        }
    }

    static void LoadMaterialTexture(Material mat, string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[ERROR] File not found: {path}");
                return;
            }

            using var bmp = new System.Drawing.Bitmap(path);

            mat.TexW = bmp.Width;
            mat.TexH = bmp.Height;
            mat.Texture = new int[mat.TexW * mat.TexH];

            for (int y = 0; y < mat.TexH; y++)
                for (int x = 0; x < mat.TexW; x++)
                {
                    var c = bmp.GetPixel(x, y);

                    mat.Texture[y * mat.TexW + x] =
                        (255 << 24) |
                        (c.R << 16) |
                        (c.G << 8) |
                        c.B;
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEXTURE LOAD FAILED] {path}");
            Console.WriteLine(ex.Message);
        }
    }

    static void LoadTexture(string path)
    {
        using var bmp = new System.Drawing.Bitmap(path);

        texture = null;
        texW = 0;
        texH = 0;

        texW = bmp.Width;
        texH = bmp.Height;

        texture = new int[texW * texH];

        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                var c = bmp.GetPixel(x, y);

                texture[y * texW + x] =
                    (255 << 24) |
                    (c.R << 16) |
                    (c.G << 8) |
                    c.B;
            }
        }
    }

    static Vector3 GetForward()
    {
        float cosPitch = (float)Math.Cos(camRotX);
        float sinPitch = (float)Math.Sin(camRotX);
        float cosYaw = (float)Math.Cos(camRotY);
        float sinYaw = (float)Math.Sin(camRotY);

        return new Vector3(
            sinYaw * cosPitch,
            -sinPitch,
            cosYaw * cosPitch
        );
    }

    static Vector3 GetRight()
    {
        float cosYaw = (float)Math.Cos(camRotY);
        float sinYaw = (float)Math.Sin(camRotY);

        return new Vector3(
            cosYaw,
            0,
            -sinYaw
        );
    }

    static (int x, int y, float z) Project(Vector3 v)
    {
        float f = GetFocalLength();

        int x = (int)MathF.Round(v.X * f / v.Z) + WIDTH / 2;
        int y = (int)MathF.Round(-v.Y * f / v.Z) + HEIGHT / 2;

        return (x, y, v.Z);
    }

    static float Edge(float ax, float ay, float bx, float by, float cx, float cy)
    {
        return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
    }

    static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }

    static Vector3 Normalize(Vector3 v)
    {
        float len = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return new Vector3(v.X / len, v.Y / len, v.Z / len);
    }

    static void DrawCheckbox(Checkbox c)
    {
        DrawRect(c.X, c.Y, c.Size, c.Size, 0xFF555555);

        if (c.Checked)
        {
            DrawRect(c.X + 4, c.Y + 4, c.Size - 8, c.Size - 8, 0xFFFFFFFF);
        }

        bool hover = PointInRect(mouseX, mouseY, c.X, c.Y, c.Size, c.Size);

        if (mousePressed && hover)
        {
            c.Checked = !c.Checked;
        }
    }

    static void DrawSlider(Slider s)
    {
        // фон
        DrawRect(s.X, s.Y, s.W, s.H, 0xFF444444);

        float t = (s.Value - s.Min) / (s.Max - s.Min);
        int knobX = s.X + (int)(t * s.W);

        // ручка
        DrawRect(knobX - 3, s.Y, 6, s.H, 0xFFFFFFFF);

        bool hover = PointInRect(mouseX, mouseY, s.X, s.Y, s.W, s.H);

        if (mousePressed && hover)
            s.Dragging = true;

        if (!mouseDown)
            s.Dragging = false;

        if (s.Dragging)
        {
            float newT = (mouseX - s.X) / (float)s.W;
            newT = Math.Clamp(newT, 0f, 1f);
            s.Value = s.Min + newT * (s.Max - s.Min);
        }
    }

    static void DrawChar(char c, int x, int y, int scale = 2)
    {
        if (!font.TryGetValue(c, out var glyph))
            return;

        for (int col = 0; col < 5; col++)
        {
            byte line = glyph[col];

            for (int row = 0; row < 7; row++)
            {
                if ((line & (1 << row)) != 0)
                {
                    DrawRect(
                        x + col * scale,
                        y + row * scale,
                        scale,
                        scale,
                        0xFFFFFFFF
                    );
                }
            }
        }
    }

    static void DrawButton(Button b)
    {
        bool hover = PointInRect(mouseX, mouseY, b.X, b.Y, b.W, b.H);

        uint color = hover ? 0xFF777777 : 0xFF444444;

        DrawRect(b.X, b.Y, b.W, b.H, color);
        DrawText(b.Text, b.X + 5, b.Y + 5, 2);

        if (mousePressed && hover)
        {
            b.OnClick?.Invoke();
        }
    }

    static void DrawText(string text, int x, int y, int scale = 2)
    {
        int cursor = x;

        foreach (char c in text)
        {
            DrawChar(c, cursor, y, scale);
            cursor += (6 * scale); // 5 + 1 spacing
        }
    }

    static List<Vector3> ApplyLightCamera(List<Vector3> verts)
    {
        var result = new List<Vector3>(verts.Count);

        float cosX = (float)Math.Cos(-lightRotX);
        float sinX = (float)Math.Sin(-lightRotX);

        float cosY = (float)Math.Cos(-lightRotY);
        float sinY = (float)Math.Sin(-lightRotY);

        foreach (var v in verts)
        {
            float x = v.X - lightPos.X;
            float y = v.Y - lightPos.Y;
            float z = v.Z - lightPos.Z;

            // Y
            float x1 = x * cosY + z * sinY;
            float z1 = -x * sinY + z * cosY;

            // X
            float y2 = y * cosX - z1 * sinX;
            float z2 = y * sinX + z1 * cosX;

            result.Add(new Vector3(x1, y2, z2));
        }

        return result;
    }

    static (float x, float y, float z) ProjectShadow(Vector3 v)
    {
        float f = GetLightFocalLength();

        float x = v.X * f / v.Z + SHADOW_W / 2f;
        float y = -v.Y * f / v.Z + SHADOW_H / 2f;

        return (x, y, v.Z);
    }

    static float ShadowFactor(Vector3 worldPos, float bias)
    {
        // в пространство света
        var v = ApplyLightCamera(new List<Vector3> { worldPos })[0];

        if (v.Z <= 0) return 1f;

        float f = GetLightFocalLength();

        float sx = v.X * f / v.Z + SHADOW_W / 2f;
        float sy = -v.Y * f / v.Z + SHADOW_H / 2f;

        int x = (int)sx;
        int y = (int)sy;

        if (sx < 0 || sx >= SHADOW_W || sy < 0 || sy >= SHADOW_H)
            return 1f;

        float depth = shadowMap[y * SHADOW_W + x];

        if (depth == float.MaxValue)
            return 0f;

        //Console.WriteLine(depth);

        if (v.Z > depth + bias)
            return 0.3f; // в тени

        return 1f; // освещён
    }

    static void RenderShadowMap(List<Vector3> worldVerts, List<(int, int, int)> tris)
    {
        Parallel.For(0, SHADOW_H, y =>
        {
            for (int x = 0; x < SHADOW_W; x++)
            {
                shadowMap[y * SHADOW_W + x] = float.MaxValue;
            }
        });

        var lightVerts = ApplyLightCamera(worldVerts);

        float f = GetLightFocalLength(); // можно отдельный FOV для света

        foreach (var (i1, i2, i3) in tris)
        {
            if (i1 <= 0 || i2 <= 0 || i3 <= 0) continue;
            if (i1 > lightVerts.Count || i2 > lightVerts.Count || i3 > lightVerts.Count)
                continue;

            var v1 = lightVerts[i1 - 1];
            var v2 = lightVerts[i2 - 1];
            var v3 = lightVerts[i3 - 1];

            var e1 = new Vector3(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
            var e2 = new Vector3(v3.X - v1.X, v3.Y - v1.Y, v3.Z - v1.Z);
            var normal = Cross(e1, e2);

            if (normal.Z >= 0) continue;

            if (v1.Z <= 0 || v2.Z <= 0 || v3.Z <= 0)
                continue;

            var p1 = ProjectShadow(v1);
            var p2 = ProjectShadow(v2);
            var p3 = ProjectShadow(v3);

            RasterizeShadowTriangle(p1, p2, p3);
        }
    }

    static void RasterizeShadowTriangle(
    (float x, float y, float z) p1,
    (float x, float y, float z) p2,
    (float x, float y, float z) p3)
    {
        int minX = Math.Clamp((int)MathF.Min(p1.x, MathF.Min(p2.x, p3.x)), 0, SHADOW_W - 1);
        int maxX = Math.Clamp((int)MathF.Max(p1.x, MathF.Max(p2.x, p3.x)), 0, SHADOW_W - 1);

        int minY = Math.Clamp((int)MathF.Min(p1.y, MathF.Min(p2.y, p3.y)), 0, SHADOW_H - 1);
        int maxY = Math.Clamp((int)MathF.Max(p1.y, MathF.Max(p2.y, p3.y)), 0, SHADOW_H - 1);

        float area = Edge(p1.x, p1.y, p2.x, p2.y, p3.x, p3.y);
        if (Math.Abs(area) < 1e-6f) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float w1 = Edge(p2.x, p2.y, p3.x, p3.y, px, py);
                float w2 = Edge(p3.x, p3.y, p1.x, p1.y, px, py);
                float w3 = Edge(p1.x, p1.y, p2.x, p2.y, px, py);

                if (!((w1 >= 0 && w2 >= 0 && w3 >= 0) ||
                      (w1 <= 0 && w2 <= 0 && w3 <= 0)))
                    continue;

                w1 /= area;
                w2 /= area;
                w3 /= area;

                float z = w1 * p1.z + w2 * p2.z + w3 * p3.z;

                int idx = y * SHADOW_W + x;

                if (z < shadowMap[idx])
                    shadowMap[idx] = z;
            }
        }
    }

    static float ComputeLight(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        var e1 = new Vector3(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
        var e2 = new Vector3(v3.X - v1.X, v3.Y - v1.Y, v3.Z - v1.Z);

        var normal = Normalize(Cross(e2, e1));

        var lightDir = GetLightDir();

        // ВАЖНО: проверка относительно света
        float dotNL =
            normal.X * lightDir.X +
            normal.Y * lightDir.Y +
            normal.Z * lightDir.Z;

        return Math.Max(0f, dotNL);
    }

    static bool IsTopLeft(float x0, float y0, float x1, float y1)
    {
        return (y0 == y1 && x0 < x1) || (y0 > y1);
    }

    static bool PointInRect(int px, int py, int x, int y, int w, int h)
    {
        return px >= x && px < x + w && py >= y && py < y + h;
    }

    static void DrawRect(int x, int y, int w, int h, uint color)
    {
        for (int iy = y; iy < y + h; iy++)
        {
            if (iy < 0 || iy >= HEIGHT) continue;

            for (int ix = x; ix < x + w; ix++)
            {
                if (ix < 0 || ix >= WIDTH) continue;

                buffer[iy * WIDTH + ix] = (int)color;
            }
        }
    }

    static void DrawTriangle(
    ScreenVertex p1, ScreenVertex p2, ScreenVertex p3,
    Vector3 v1, Vector3 v2, Vector3 v3,
    UV uv1, UV uv2, UV uv3)
    {
        if (p1.Z <= 0 || p2.Z <= 0 || p3.Z <= 0)
            return;

        float area = Edge(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y);
        if (Math.Abs(area) < 1e-6f) return;

        float invArea = 1.0f / area;

        int minX = Math.Clamp((int)MathF.Floor(MathF.Min(p1.X, MathF.Min(p2.X, p3.X))), 0, WIDTH - 1);
        int maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(p1.X, MathF.Max(p2.X, p3.X))), 0, WIDTH - 1);

        int minY = Math.Clamp((int)MathF.Floor(MathF.Min(p1.Y, MathF.Min(p2.Y, p3.Y))), 0, HEIGHT - 1);
        int maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(p1.Y, MathF.Max(p2.Y, p3.Y))), 0, HEIGHT - 1);

        // --- lighting (вынесено из пиксельного цикла) ---
        float diffuse = ComputeLight(v1, v2, v3);
        float ambient = 0.15f;

        float specular = 0f;
        float bias = 0.00001f;

        if (diffuse > 0)
        {
            var viewDir = Normalize(new Vector3(-v1.X, -v1.Y, -v1.Z));

            var e1 = new Vector3(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
            var e2 = new Vector3(v3.X - v1.X, v3.Y - v1.Y, v3.Z - v1.Z);
            var normal = Normalize(Cross(e2, e1));
            var lightDir = GetLightDir();

            float dotNL =
                normal.X * lightDir.X +
                normal.Y * lightDir.Y +
                normal.Z * lightDir.Z;

            var reflect = new Vector3(
                2 * dotNL * normal.X - lightDir.X,
                2 * dotNL * normal.Y - lightDir.Y,
                2 * dotNL * normal.Z - lightDir.Z
            );

            float spec = Math.Max(0f,
                reflect.X * viewDir.X +
                reflect.Y * viewDir.Y +
                reflect.Z * viewDir.Z
            );

            specular = (float)Math.Pow(spec, 16);
            bias = Math.Max(0.002f, 0.02f * (1f - dotNL));
        }

        diffuse = Math.Max(diffuse, 0.1f);
        float intensity = (ambient + diffuse * 0.8f + specular * 0.25f) * light_int;
        intensity = Math.Clamp(intensity, 0f, 1f);

        // --- tile loop ---
        for (int ty = minY; ty <= maxY; ty += TILE_SIZE)
        {
            for (int tx = minX; tx <= maxX; tx += TILE_SIZE)
            {
                int tileMaxX = Math.Min(tx + TILE_SIZE - 1, maxX);
                int tileMaxY = Math.Min(ty + TILE_SIZE - 1, maxY);

                // --- можно добавить trivial reject (ускорение) ---
                bool inside = false;

                for (int cy = ty; cy <= tileMaxY && !inside; cy++)
                {
                    for (int cx = tx; cx <= tileMaxX; cx++)
                    {
                        float w1 = Edge(p2.X, p2.Y, p3.X, p3.Y, cx, cy);
                        float w2 = Edge(p3.X, p3.Y, p1.X, p1.Y, cx, cy);
                        float w3 = Edge(p1.X, p1.Y, p2.X, p2.Y, cx, cy);

                        if ((w1 >= 0 && w2 >= 0 && w3 >= 0) ||
                            (w1 <= 0 && w2 <= 0 && w3 <= 0))
                        {
                            inside = true;
                            break;
                        }
                    }
                }

                if (!inside) continue;

                // --- raster tile ---
                for (int y = ty; y <= tileMaxY; y++)
                {
                    int row = y * WIDTH;

                    for (int x = tx; x <= tileMaxX; x++)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;

                        float w1 = Edge(p2.X, p2.Y, p3.X, p3.Y, px, py);
                        float w2 = Edge(p3.X, p3.Y, p1.X, p1.Y, px, py);
                        float w3 = Edge(p1.X, p1.Y, p2.X, p2.Y, px, py);

                        if (!((w1 >= 0 && w2 >= 0 && w3 >= 0) ||
                              (w1 <= 0 && w2 <= 0 && w3 <= 0)))
                            continue;

                        w1 *= invArea;
                        w2 *= invArea;
                        w3 *= invArea;

                        float invZ =
                            w1 / p1.Z +
                            w2 / p2.Z +
                            w3 / p3.Z;

                        float u =
                            (w1 * uv1.U / p1.Z +
                             w2 * uv2.U / p2.Z +
                             w3 * uv3.U / p3.Z) / invZ;

                        float v =
                            (w1 * uv1.V / p1.Z +
                             w2 * uv2.V / p2.Z +
                             w3 * uv3.V / p3.Z) / invZ;

                        float z = 1.0f / invZ;

                        int idx = row + x;

                        if (z >= zbuffer[idx]) continue;

                        zbuffer[idx] = z;

                        int txi = (int)(u * (texW - 1));
                        int tyi = (int)((1 - v) * (texH - 1));

                        int texColor;

                        if (texture != null)
                        {
                            txi = Math.Clamp(txi, 0, texW - 1);
                            tyi = Math.Clamp(tyi, 0, texH - 1);
                            texColor = texture[tyi * texW + txi];
                        }
                        else
                        {
                            texColor = FallbackMaterial(intensity, z);
                        }

                        float wx = w1 * v1.X + w2 * v2.X + w3 * v3.X;
                        float wy = w1 * v1.Y + w2 * v2.Y + w3 * v3.Y;
                        float wz = w1 * v1.Z + w2 * v2.Z + w3 * v3.Z;

                        float shadow = ShadowFactor(new Vector3(wx, wy, wz), bias);

                        int r = (int)(((texColor >> 16) & 255) * intensity * shadow);
                        int g = (int)(((texColor >> 8) & 255) * intensity * shadow);
                        int b = (int)((texColor & 255) * intensity * shadow);

                        buffer[idx] =
                            (255 << 24) |
                            (r << 16) |
                            (g << 8) |
                            b;
                    }
                }
            }
        }
    }

    static List<Vector3> ApplyCamera(List<Vector3> verts)
    {
        var result = new List<Vector3>(verts.Count);

        float cosX = (float)Math.Cos(-camRotX);
        float sinX = (float)Math.Sin(-camRotX);

        float cosY = (float)Math.Cos(-camRotY);
        float sinY = (float)Math.Sin(-camRotY);

        float cosZ = (float)Math.Cos(-camRotZ);
        float sinZ = (float)Math.Sin(-camRotZ);

        foreach (var v in verts)
        {
            // --- translate world relative to camera ---
            float x = v.X - camPos.X;
            float y = v.Y - camPos.Y;
            float z = v.Z - camPos.Z;

            // --- rotate Z ---
            float x1 = x * cosZ - y * sinZ;
            float y1 = x * sinZ + y * cosZ;
            float z1 = z;

            // --- rotate Y ---
            float x2 = x1 * cosY + z1 * sinY;
            float z2 = -x1 * sinY + z1 * cosY;
            float y2 = y1;

            // --- rotate X ---
            float y3 = y2 * cosX - z2 * sinX;
            float z3 = y2 * sinX + z2 * cosX;
            float x3 = x2;

            result.Add(new Vector3(x3, y3, z3));
        }

        return result;
    }

    static void refresh()
    {
        Array.Fill(buffer, BLACK);
        Array.Fill(zbuffer, float.MaxValue);
    }

    static void printTriangles(
List<(int, int, int)> tris,
List<Vector3> vertices,
List<ScreenVertex> projected)
    {
        int triCount = Math.Min(tris.Count, triangleUVs.Count);

        for (int i = 0; i < triCount; i++)
        {
            var (i1, i2, i3) = tris[i];
            var (t1, t2, t3) = triangleUVs[i];

            // safety clamp (CRITICAL)
            if (i1 <= 0 || i2 <= 0 || i3 <= 0) continue;
            if (i1 > projected.Count || i2 > projected.Count || i3 > projected.Count)
                continue;

            UV uv1 = (t1 > 0 && t1 <= uvs.Count) ? uvs[t1 - 1] : new UV(0, 0);
            UV uv2 = (t2 > 0 && t2 <= uvs.Count) ? uvs[t2 - 1] : new UV(0, 0);
            UV uv3 = (t3 > 0 && t3 <= uvs.Count) ? uvs[t3 - 1] : new UV(0, 0);

            DrawTriangle(
                projected[i1 - 1],
                projected[i2 - 1],
                projected[i3 - 1],

                vertices[i1 - 1],
                vertices[i2 - 1],
                vertices[i3 - 1],

                uv1, uv2, uv3
            );
        }
    }

    static void printPoint(float x, float y)
    {
        if (x < 0 || x >= WIDTH || y < 0 || y >= HEIGHT) return;
        buffer[(int)Math.Round(y * WIDTH + x)] = GREEN;
    }

    // Bresenham
    static void printLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            printPoint(x0, y0);

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;

            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    // --- вывод буфера ---
    static void Present()
    {
        StretchDIBits(
            hdc,
            0, 0, WIDTH, HEIGHT,
            0, 0, WIDTH, HEIGHT,
            buffer,
            ref bmi,
            DIB_RGB_COLORS,
            SRCCOPY
        );
    }

    static int FallbackMaterial(float intensity, float z)
    {
        float shade = intensity * (0.6f + 0.4f * (1f / (1f + z * 0.1f)));

        int r = (int)(90 * shade);
        int g = (int)(160 * shade);
        int b = (int)(200 * shade);

        return (255 << 24) | (r << 16) | (g << 8) | b;
    }

    static void AddEdge(HashSet<(int, int)> set, int a, int b)
    {
        if (a > b) (a, b) = (b, a);
        set.Add((a, b));
    }

    public static void Parse(
    string path,
    out List<Vector3> vertices,
    out List<(int, int)> edges,
    out List<(int, int, int)> triangles)
    {
        string mtlPath = null;

        originalVertices.Clear();
        uvs.Clear();
        triangleUVs.Clear();
        triangleMaterials.Clear();

        vertices = new List<Vector3>();
        triangles = new List<(int, int, int)>();
        var edgeSet = new HashSet<(int, int)>();

        currentMaterial = "default";

        foreach (var lineRaw in File.ReadLines(path))
        {
            var line = lineRaw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            // --- MATERIAL LIB ---
            if (line.StartsWith("mtllib "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                mtlPath = Path.Combine(Path.GetDirectoryName(path), parts[1]);

                LoadMTL(mtlPath);
            }

            // --- USE MATERIAL ---
            else if (line.StartsWith("usemtl "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                currentMaterial = parts[1];
            }

            // --- VERTICES ---
            else if (line.StartsWith("v "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                float z = float.Parse(parts[3], CultureInfo.InvariantCulture);

                vertices.Add(new Vector3(x, y, z));
            }

            // --- UV ---
            else if (line.StartsWith("vt "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                float u = float.Parse(parts[1], CultureInfo.InvariantCulture);
                float v = float.Parse(parts[2], CultureInfo.InvariantCulture);

                uvs.Add(new UV(u, v));
            }

            // --- FACES ---
            else if (line.StartsWith("f "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                var vIndices = new List<int>();
                var uvIndices = new List<int>();

                for (int i = 1; i < parts.Length; i++)
                {
                    var split = parts[i].Split('/');

                    int vIdx = int.Parse(split[0]); // OBJ is 1-based
                    int uvIdx = (split.Length > 1 && split[1] != "")
                        ? int.Parse(split[1])
                        : 0;

                    vIndices.Add(vIdx);
                    uvIndices.Add(uvIdx);
                }

                // triangulation (fan)
                for (int i = 1; i < vIndices.Count - 1; i++)
                {
                    triangles.Add((vIndices[0], vIndices[i], vIndices[i + 1]));

                    triangleUVs.Add((
                        uvIndices[0],
                        uvIndices[i],
                        uvIndices[i + 1]
                    ));

                    triangleMaterials.Add(currentMaterial);

                    // edges per triangle
                    AddEdge(edgeSet, vIndices[0], vIndices[i]);
                    AddEdge(edgeSet, vIndices[i], vIndices[i + 1]);
                    AddEdge(edgeSet, vIndices[i + 1], vIndices[0]);
                }
            }
        }

        edges = new List<(int, int)>(edgeSet);
    }

    static void printPoints(List<Vector3> vertices)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            float f = GetFocalLength();

            int x = (int)MathF.Round(v.X * f / v.Z) + WIDTH / 2;
            int y = (int)MathF.Round(-v.Y * f / v.Z) + HEIGHT / 2;
            if (v.Z > 0) { printPoint(x, y); }
        }
    }

    static void printEdges(List<(int, int)> edges, List<Vector3> vertices, List<ScreenVertex> projected)
    {
        foreach (var (i1, i2) in edges)
        {
            var v1 = vertices[i1 - 1];
            var v2 = vertices[i2 - 1];

            // 1. Z clipping
            if (v1.Z <= 0 || v2.Z <= 0)
                continue;

            // 2. Проекция
            float f = GetFocalLength();

            float x1 = v1.X * f / v1.Z + WIDTH / 2f;
            float y1 = -v1.Y * f / v1.Z + HEIGHT / 2f;

            float x2 = v2.X * f / v2.Z + WIDTH / 2f;
            float y2 = -v2.Y * f / v2.Z + HEIGHT / 2f;

            // 3. trivial accept
            bool inside1 = x1 >= 0 && x1 < WIDTH && y1 >= 0 && y1 < HEIGHT;
            bool inside2 = x2 >= 0 && x2 < WIDTH && y2 >= 0 && y2 < HEIGHT;

            if (inside1 && inside2)
            {
                printLine((int)x1, (int)y1, (int)x2, (int)y2);
                continue;
            }

            // 4. Уравнение прямой y = kx + b
            float dx = x2 - x1;
            float dy = y2 - y1;

            float k = 0;
            float b = 0;

            bool vertical = Math.Abs(dx) < 0.0001f;

            if (!vertical)
            {
                k = dy / dx;
                b = y1 - k * x1;
            }

            // Функция клиппинга одной точки
            bool Clip(ref float x, ref float y)
            {
                if (x < 0)
                {
                    if (!vertical)
                    {
                        y = (int)(k * 0 + b);
                        x = 0;
                    }
                    else return false;
                }
                else if (x >= WIDTH)
                {
                    if (!vertical)
                    {
                        y = (int)(k * (WIDTH - 1) + b);
                        x = WIDTH - 1;
                    }
                    else return false;
                }

                if (y < 0)
                {
                    if (vertical)
                    {
                        x = x1;
                        y = 0;
                    }
                    else
                    {
                        x = (int)((0 - b) / k);
                        y = 0;
                    }
                }
                else if (y >= HEIGHT)
                {
                    if (vertical)
                    {
                        x = x1;
                        y = HEIGHT - 1;
                    }
                    else
                    {
                        x = (int)((HEIGHT - 1 - b) / k);
                        y = HEIGHT - 1;
                    }
                }

                // после пересчёта проверяем
                return x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT;
            }

            bool ok1 = Clip(ref x1, ref y1);
            bool ok2 = Clip(ref x2, ref y2);

            if (ok1 && ok2)
            {
                printLine((int)x1, (int)y1, (int)x2, (int)y2);
            }
        }
    }

    static float GetLightFocalLength()
    {
        float fovRad = lightFOV * (float)Math.PI / 180f;
        return (SHADOW_W / 2f) / (float)Math.Tan(fovRad / 2f);
    }

    static float GetFocalLength()
    {
        float fovRad = FOV * (float)Math.PI / 180f;
        return (WIDTH / 2f) / (float)Math.Tan(fovRad / 2f);
    }

    static List<Vector3> scale(float sx, float sy, float sz, List<Vector3> verts)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            verts[i] = new Vector3(
                v.X * sx,
                v.Y * sy,
                v.Z * sz
            );
        }
        return verts;
    }

    static List<Vector3> rotate(float x, float y, float z, List<Vector3> verts, float xc, float yc, float zc)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];

            float cosX = (float)Math.Cos(x);
            float sinX = (float)Math.Sin(x);
            float cosY = (float)Math.Cos(y);
            float sinY = (float)Math.Sin(y);
            float cosZ = (float)Math.Cos(z);
            float sinZ = (float)Math.Sin(z);

            // Поворот вокруг X
            float xRel = v.X - xc;
            float yRel = v.Y - yc;
            float zRel = v.Z - zc;

            // X rotation
            float y1 = yRel * cosX - zRel * sinX;
            float z1 = yRel * sinX + zRel * cosX;

            // Y rotation
            float x2 = xRel * cosY + z1 * sinY;
            float z2 = -xRel * sinY + z1 * cosY;

            // Z rotation
            float x3 = x2 * cosZ - y1 * sinZ;
            float y3 = x2 * sinZ + y1 * cosZ;

            verts[i] = new Vector3(x3 + xc, y3 + yc, z2 + zc);
        }
        return verts;
    }

    static List<Vector3> offset(float x, float y, float z, List<Vector3> verts)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            verts[i] = new Vector3(v.X + x * SCALE, v.Y + y * SCALE, v.Z + z * SCALE);
        }
        return verts;
    }

    static void RenderTile(
    int minX, int minY, int maxX, int maxY,
    List<Vector3> world,
    List<Vector3> view,
    List<(int, int, int)> tris)
    {
        for (int i = 0; i < tris.Count; i++)
        {
            var (i1, i2, i3) = tris[i];

            var p1 = Project(view[i1 - 1]);
            var p2 = Project(view[i2 - 1]);
            var p3 = Project(view[i3 - 1]);

            // AABB треугольника
            int triMinX = Math.Min(p1.x, Math.Min(p2.x, p3.x));
            int triMaxX = Math.Max(p1.x, Math.Max(p2.x, p3.x));
            int triMinY = Math.Min(p1.y, Math.Min(p2.y, p3.y));
            int triMaxY = Math.Max(p1.y, Math.Max(p2.y, p3.y));

            // если не пересекается с тайлом — skip
            if (triMaxX < minX || triMinX >= maxX ||
                triMaxY < minY || triMinY >= maxY)
                continue;

            var v1 = world[i1 - 1];
            var v2 = world[i2 - 1];
            var v3 = world[i3 - 1];

            // UV
            var (t1, t2, t3) = triangleUVs[i];

            UV uv1 = (t1 > 0 && t1 <= uvs.Count) ? uvs[t1 - 1] : new UV(0, 0);
            UV uv2 = (t2 > 0 && t2 <= uvs.Count) ? uvs[t2 - 1] : new UV(0, 0);
            UV uv3 = (t3 > 0 && t3 <= uvs.Count) ? uvs[t3 - 1] : new UV(0, 0);

            // ВЫЗОВ
            DrawTriangle(
                new ScreenVertex(p1.x, p1.y, p1.z),
                new ScreenVertex(p2.x, p2.y, p2.z),
                new ScreenVertex(p3.x, p3.y, p3.z),

                v1, v2, v3,
                uv1, uv2, uv3
            );
        }
    }


    // --- кадр ---
    static void Render(
    List<Vector3> worldVertices,
    List<Vector3> viewVertices,
    List<(int, int)> edges,
    List<(int, int, int)> triangles,
    int fps)
    {
        refresh();

        RenderShadowMap(worldVertices, triangles);

        var projected = ProjectAll(viewVertices);

        if (!noFillToggle.Checked)
            Parallel.For(0, tilesY, ty =>
            {
                for (int tx = 0; tx < tilesX; tx++)
                {
                    int startX = tx * TILE;
                    int startY = ty * TILE;

                    int endX = Math.Min(startX + TILE, WIDTH);
                    int endY = Math.Min(startY + TILE, HEIGHT);

                    RenderTile(startX, startY, endX, endY,
                        worldVertices, viewVertices, triangles);
                }
            });

        if (wireframeToggle.Checked)
            printEdges(edges, viewVertices, projected);

        //printTriangles(triangles, vertices, projected);
        //printEdges(edges, vertices, projected);

        DrawText($"FPS: {fps:F1}", 10, 10);

        DrawText("X-OFFSET", posX.X + posX.W + 10, posX.Y);
        DrawText("Y-OFFSET", posY.X + posY.W + 10, posY.Y);
        DrawText("Z-OFFSET", posZ.X + posZ.W + 10, posZ.Y);

        DrawText("X-ROTATION", rotX.X + rotX.W + 10, rotX.Y);
        DrawText("Y-ROTATION", rotY.X + rotY.W + 10, rotY.Y);
        DrawText("Z-ROTATION", rotZ.X + rotZ.W + 10, rotZ.Y);

        DrawText("SHOW WIREFRAME", wireframeToggle.X + wireframeToggle.Size + 10, wireframeToggle.Y);
        DrawText("HIDE MESH", noFillToggle.X + noFillToggle.Size + 10, noFillToggle.Y);

        DrawText("X-SCALE", scaleX.X + scaleX.W + 10, scaleX.Y);
        DrawText("Y-SCALE", scaleY.X + scaleY.W + 10, scaleY.Y);
        DrawText("Z-SCALE", scaleZ.X + scaleZ.W + 10, scaleZ.Y);


        DrawSlider(posX);
        DrawSlider(posY);
        DrawSlider(posZ);

        DrawSlider(rotX);
        DrawSlider(rotY);
        DrawSlider(rotZ);

        DrawCheckbox(wireframeToggle);
        DrawCheckbox(noFillToggle);

        DrawButton(loadObjBtn);
        DrawButton(loadTexBtn);

        DrawSlider(scaleX);
        DrawSlider(scaleY);
        DrawSlider(scaleZ);

        DrawButton(resetOffsetBtn);
        DrawButton(resetRotBtn);
        DrawButton(resetTexBtn);

        mousePressed = false;
        mouseReleased = false;
        Present();
    }
}