using Microsoft.Kinect;
using System.Collections;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GestureRecoginzerWithDTW
{
    public partial class MainWindow : Window
    {
        KinectSensor sensor;
        BodyFrameReader bodyFrameReader;
        private Body[] bodies = null;
        int screenWidth, screenHeight;
        private bool start = false;
        private ArrayList points;


        public MainWindow()
        {
            InitializeComponent();
            points = new ArrayList();
            sensor = KinectSensor.GetDefault();
            //open the reader for body frames
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            //bodyFrameReader.FrameArrived += bodyFrameReader_frameArrived;

            // get screen with and height
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;

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
                    // get various skeletal positions
                    CameraSpacePoint handLeft = body.Joints[JointType.HandLeft].Position;
                    CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                    HandState leftHandState = body.HandLeftState;
                    HandState rightHandState = body.HandRightState;
                    textBlock2.Text = rightHandState.ToString();

                    float x = 0;
                    float y = 0;
                    HandState selectHandState = HandState.Unknown;

                    if (primeHand)
                    {
                        x = handRight.X + 0.1f;
                        y = handRight.Y + 0.4f;
                        selectHandState = rightHandState;
                    }
                    else
                    {
                        x = handLeft.X - 0.1f;
                        y = handLeft.Y + 0.4f;
                        selectHandState = leftHandState;
                    }
                    //reverse y
                    y = 1 - y;

                    if (selectHandState == HandState.Open)
                    {
                        if (start)
                        {
                            mouse_up();
                            start = false;
                        }
                    }
                    else if (selectHandState == HandState.Closed)
                    {
                        if (start)
                        {
                            mouse_move(x, y);
                        }
                        else
                        {
                            mouse_down(x, y);
                            start = true;
                        }
                    }
                    else
                    {
                        if (start)
                        {
                            mouse_move(x, y);
                        }
                    }
                }
            }
        }
    }
}
