using Microsoft.Kinect;
using System.Collections;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Text;

namespace GestureRecoginzerWithDTW
{
    public partial class MainWindow : Window
    {
        KinectSensor sensor;
        BodyFrameReader bodyFrameReader;
        private Body[] bodies = null;
        int screenWidth, screenHeight;
        private bool start = false;
        private List<int[]> points;
        private List<List<int[]>> templates = null;
        // the grid number
        private const int gridNum = 64;

        // 控制采集数据或者进行识别
        private bool recognize = false;

        public MainWindow()
        {
            InitializeComponent();
            points = new List<int[]>();
            sensor = KinectSensor.GetDefault();
            //open the reader for body frames
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

            // get screen with and height
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            // read data
            ReadFile("../../recordFiles.txt");
            // open sensor
            sensor.Open();
        }

        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;
            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }
            if (!dataReceived)
            {
                return;
            }

            foreach (Body body in this.bodies)
            {
                // get first tracked body only, notice there's a break below.
                if (body.IsTracked)
                {
                    CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                    HandState rightHandState = body.HandRightState;
                    textBlock2.Text = rightHandState.ToString();

                    float x = handRight.X + 0.1f;
                    float y = 1-(handRight.Y + 0.4f);
                    int[] encode = CalculateEncodeNumber(body);

                    if (rightHandState == HandState.Open)
                    {
                        if (start)
                        {
                            actionEnd();
                            start = false;
                        }
                    }
                    else if (rightHandState == HandState.Closed)
                    {
                        if (start)
                        {
                            actionInProgress(x, y, encode);
                        }
                        else
                        {
                            actionStart(x,y,encode);
                            start = true;
                        }
                    }
                    else
                    {
                        if (start)
                        {
                            actionInProgress(x, y, encode);
                        }
                    }
                }
            }
        }

        private void actionStart(float x, float y, int[] encode)
        {
            //Console.WriteLine("action start");
            points.Clear();
            m_canvas.Children.Clear();
            draw(x, y);
            points.Add(encode);
        }

        private void actionInProgress(float x, float y, int[] encode)
        {
            //Console.WriteLine("action in progress");
            draw(x, y);
            points.Add(encode);
        }

        private void actionEnd()
        {
            //Console.WriteLine("action end");
            if (points.Count >= 5)
            {
                if (recognize)
                {
                    // 识别
                    String result = dtwRecognizer();
                    select_posture(result);
                }
                else
                {
                    foreach(int[] point in points)
                    {
                        Console.Write(point[0] + ":" + point[1] + " ");
                        Console.WriteLine("--------------------");
                    }
                }
            }
        }

        private void draw(float x, float y)
        {
            Ellipse ellipse = new Ellipse();
            ellipse.Fill = new SolidColorBrush(Colors.Red);
            ellipse.Width = 4;
            ellipse.Height = 4;
            Canvas.SetLeft(ellipse, x * m_canvas.Width);
            Canvas.SetTop(ellipse, y * m_canvas.Height);

            ellipse.Visibility = Visibility.Visible;
            m_canvas.Children.Add(ellipse);
        }

        private int[] CalculateEncodeNumber(Body body)
        {
            // 获取需要的点坐标
            CameraSpacePoint handLeft = body.Joints[JointType.HandLeft].Position;
            CameraSpacePoint spinebase = body.Joints[JointType.SpineBase].Position;
            CameraSpacePoint spineShoulder = body.Joints[JointType.SpineShoulder].Position;

            // 坐标平移
            handLeft.X -= spinebase.X;
            handLeft.Y -= spinebase.Y;
            // TODO:比例变化
            // 编码
            int[] encode = new int[2];
            double gridTotalHeight = (spineShoulder.Y - spinebase.Y) / 0.364 * 1.5;
            double gridTotalWidth = (spineShoulder.Y - spinebase.Y) / 0.364 * 1.2;
            double gridHeight = gridTotalHeight / gridNum;
            double gridWidth = gridTotalWidth / gridNum;
            encode[0] = encodeOnePoint(gridWidth, handLeft.X);
            encode[1] = encodeOnePoint(gridWidth, handLeft.Y);
            return encode;
        }

        private int encodeOnePoint(double gridLength, double point)
        {
            double start = 0;
            if (point >= 0)
            {
                for (int i = 0; i < gridNum / 2; i++)
                {
                    if (point >= start && point < start + gridLength)
                    {
                        return i;
                    }
                    start += gridLength;
                }
                return gridNum / 2;
            }
            else
            {
                for (int i = 1; i < gridNum / 2; i++)
                {
                    if (point <= start && point > start - gridLength)
                    {
                        return -i;
                    }
                    start -= gridLength;
                }
                return -gridNum / 2;
            }
        }

        //用于显示的数据
        private void select_posture(String name)
        {
            foreach (var a in GestureCollection.Children)
            {
                if (name.StartsWith(((Grid)a).Name))
                {
                    ((Grid)a).Background = new SolidColorBrush(Colors.LightBlue);
                }
                else
                {
                    ((Grid)a).Background = new SolidColorBrush(Colors.White);
                }
            }
        }

        private string dtwRecognizer()
        {
            int m = points.Count + 1;
            int result = 0;
            double minValue = Double.MaxValue;
            int index = -1;
            foreach(List<int[]> template in templates){
                index++;
                int n = template.Count + 1;
                double[,] matrix = new double[m,n];
                for(int i = 1; i < m; i++)
                {
                    for(int j = 1; j < n; j++)
                    {
                        double value = getDistance(points[i - 1][0], points[i - 1][1], template[j - 1][0], template[j - 1][1]);
                        matrix[i, j] = value + Math.Min(Math.Min(matrix[i - 1, j], matrix[i, j - 1]), matrix[i - 1, j - 1]);
                    }
                }
                if(matrix[m-1,n-1] < minValue)
                {
                    result = index;
                    minValue = matrix[m - 1, n - 1];
                }
            }

            switch (result)
            {
                case 0: return "Caret";
                case 1: return "Check";
                case 2: return "Circle";
                case 3: return "Delete";
                case 4: return "Lightning";
                case 5: return "Pigtail";
                case 6: return "Question_mark";
                case 7: return "Rectangle";
                case 8: return "Triangle";
                case 9: return "X";
                default: return "";
            }
        }

        private double getDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
        }

        private void ReadFile(String path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader streamReader = new StreamReader(fileStream, Encoding.Default);
            String str;
            while((str = streamReader.ReadLine()) != null)
            {
                List<int[]> list = new List<int[]>();
                str.Trim();
                String[] strs = str.Split(' ');
                foreach (String s in strs)
                {
                    int[] point = new int[2];
                    String[] ss = s.Split(':');
                    point[0] = Convert.ToInt32(ss[0]);
                    point[1] = Convert.ToInt32(ss[1]);
                    list.Add(point);
                }
                templates.Add(list);
            }
        }
    }
}
