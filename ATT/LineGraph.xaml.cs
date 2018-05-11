using OxyPlot;
using OxyPlot.Series;
using MbientLab.MetaWear;
using MbientLab.MetaWear.Core;
using MbientLab.MetaWear.Data;
using MbientLab.MetaWear.Sensor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using OxyPlot.Axes;

using WindowsQuaternion = System.Numerics.Quaternion;
using System.Diagnostics;
using Windows.UI.Xaml.Media;
using MbientLab.MetaWear.Core.Settings;
using System.Threading.Tasks;
using System.Text;
using MbientLab.MetaWear.Peripheral;
using MbientLab.MetaWear.Peripheral.Gpio;



// O modelo de item de Página em Branco está documentado em https://go.microsoft.com/fwlink/?LinkId=234238

namespace ATT {

    #region complimentary class: Graphic plotter
    public class MainViewModel {
        public const int MAX_DATA_SAMPLES = 960;
        public const int MAX_SECONDS = 10;
        public MainViewModel() {
            MyModel = new PlotModel {
                Title = "Angles",
                IsLegendVisible = true
            };
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 0, 1),
                LineStyle = LineStyle.Solid,
                Title = "W1"
        });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 0, 0),
                LineStyle = LineStyle.Solid,
                Title = "X1"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 1, 0),
                LineStyle = LineStyle.Solid,
                Title = "Y1"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 0, 1),
                LineStyle = LineStyle.Solid,
                Title = "Z1"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 1, 0),
                LineStyle = LineStyle.Solid,
                Title = "W2"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 1, 1),
                LineStyle = LineStyle.Solid,
                Title = "X2"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 0, 0),
                LineStyle = LineStyle.Solid,
                Title = "Y2"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 1, 1),
                LineStyle = LineStyle.Solid,
                Title = "Z2"
            });
            MyModel.Series.Add(new AreaSeries()); //Area series for highlighting data marking
            MyModel.Axes.Add(new LinearAxis {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                // AbsoluteMinimum = -1f,
                // AbsoluteMaximum = 1f,
                Minimum = -1f,
                Maximum = 1f,
                Title = "Value"
            });
            MyModel.Axes.Add(new LinearAxis {
                IsPanEnabled = true,
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                AbsoluteMinimum = 0,
                Minimum = 0,
                Maximum = MAX_DATA_SAMPLES
            });

            //Euler Plot
            MyModelEuler = new PlotModel {
                Title = "EulerAngles",
                IsLegendVisible = true
            };
            MyModelEuler.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 0, 1),
                LineStyle = LineStyle.Solid,
                Title = "roll (x1-axis)"
            });
            MyModelEuler.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 0, 0),
                LineStyle = LineStyle.Solid,
                Title = "pitch (y1-axis)"
            });
            MyModelEuler.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 0, 1),
                LineStyle = LineStyle.Solid,
                Title = "yaw (z1-axis)"
            });
            MyModelEuler.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 1, 1),
                LineStyle = LineStyle.Solid,
                Title = "roll (x2-axis)"
            });
            MyModelEuler.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 0, 0),
                LineStyle = LineStyle.Solid,
                Title = "pitch (y2-axis)"
            });
            MyModelEuler.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 1, 0),
                LineStyle = LineStyle.Solid,
                Title = "yaw (z2-axis)"
            });
            MyModelEuler.Axes.Add(new LinearAxis {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                // AbsoluteMinimum = -1f,
                // AbsoluteMaximum = 1f,
                Minimum = -180,
                Maximum = 180,
                Title = "Value"
            });
            MyModelEuler.Axes.Add(new LinearAxis {
                IsPanEnabled = true,
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                AbsoluteMinimum = 0,
                Minimum = 0,
                Maximum = MAX_DATA_SAMPLES
            });
        }

        public PlotModel MyModel { get; private set; }
        public PlotModel MyModelEuler { get; private set; }
    }
    #endregion

    /// <summary>
    /// Main page of the application. Displays device information and processed and collected data.
    /// </summary>
    public sealed partial class LineGraph : Page {

        #region fields
        // private IMetaWearBoard metawear;
        private ISensorFusionBosch[] sensorFusions;

        int numBoards = 1;
        //private IntPtr cppBoard;
        private IMetaWearBoard[] metawears; // board storage
        bool startNext = true;
        bool isRunning = false; // avoids weird timing errors with switching streaming on and off
        bool[] centered = { false, false }; // sensor has ben centered
        bool[] shouldCenter = { false, false }; // take reference quaternion
        bool record = false; // keeps track of if record switch is on -- avoids threading error when actually accessing switch
        bool angleMode = false; // ^ same
        int[] freq = { 0, 0 };  // stores number of samples received, reset every second
        Quaternion[] refQuats = new Quaternion[2]; // reference quaternions
        EulerAngles[] refEuler = new EulerAngles[2];
        PlotModel model;
        PlotModel modelEuler;
        int[] samples = { 0, 0 }; // stores number of samples received 
        int secs = 0;
        StringBuilder[] csv = { new StringBuilder(), new StringBuilder() }; // data storage, more efficient than string concatenation
        StringBuilder[] csvEuler = { new StringBuilder(), new StringBuilder() }; // data storage, more efficient than string concatenation
        TextBlock[] textblocks = new TextBlock[4];
        private System.Threading.Timer timer1; // used for triggering UI updates every second
        private System.Threading.Timer timer2; //used for triggering pressure sensor read every 200ms
        private System.Threading.Timer timer3; // used for triggering UI updates every 10 seconds

        private IGpio[] gpio;
        private ushort[] pressurePin;
        

        //marking flag
        private bool changeBackGroundColor = false;
        private bool renew = false;

        private ushort MINIMUM_PRESSURE_TO_SIGNALIZE_PULLING_BABY = 1000; //Value must be calibrated. Max Value = 1023. Min Value = 0
        private bool RECORD_EVERYTHING_MODE = true; 
        #endregion


        public LineGraph() {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            var devices = e.Parameter as BluetoothLEDevice[];

            numBoards = devices.Length;
            metawears = new IMetaWearBoard[numBoards];
            dateTextBox.Text = DateTime.Now.ToString("yyMMdd");
            AverageFrequencyTextBlock.Text = "";
            TextBlock[] Macs = { Mac1, Mac2 };

            for (var i = 0; i < numBoards; i++) {
                // Initialize boards and enable high frequency streaming
                metawears[i] = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(devices[i]);
                var settings = metawears[i].GetModule<ISettings>();
                settings.EditBleConnParams(maxConnInterval: 7.5f);

                Macs[i].Text = metawears[i].MacAddress.ToString(); // update UI to show mac addresses of sensors
            }

            //TODO: make this not hardcoded
            textblocks[0] = DataTextBlock1;
            textblocks[1] = DataTextBlock2;

            InitBatteryTimer();

            if (numBoards == 1) {
                removeBoardTwoFormatting();
            }

            model = (DataContext as MainViewModel).MyModel;
            modelEuler = (DataContext as MainViewModel).MyModelEuler; // graph will be shown programatically but is already set
        }

        public void removeBoardTwoFormatting() {
            dataGrid.Children.Remove(Mac2);
            dataGrid.Children.Remove(DataTextBlock2);
            dataGrid.Children.Remove(Name2);
            dataGrid.ColumnDefinitions.RemoveAt(1);


            controlGrid.Children.Remove(FrequencyTextBlock2);
            controlGrid.Children.Remove(BatteryTextBlock2);
            controlGrid.ColumnDefinitions.RemoveAt(1);
        }

        // Initialize timer that causes displaySampleFreq() to be called every second.
        public void InitFreqTimer() {
            timer1 = new System.Threading.Timer(displaySampleFreq, null, 0, 1000);
        }
        //Initialize timer that calls a read from the pressure sensor every 200ms
        public void InitPressureTimer() {
            timer2 = new System.Threading.Timer(readPressureSensor, null, 0, 200);
        }

        // Initialize timer that causes displayBatteryLevel() to be called every 10 seconds.
        public void InitBatteryTimer() {
            timer3 = new System.Threading.Timer(displayBatteryLevel, null, 0, 10000);
        }

        // Display the battery level for each sensor as a percent.
        public async void displayBatteryLevel(Object state) {
            TextBlock[] textblocks = { BatteryTextBlock1, BatteryTextBlock2 };
            for (var i = 0; i < numBoards; i++) {
                byte battery = await metawears[i].ReadBatteryLevelAsync();
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    textblocks[i].Text = battery.ToString() + " %";
                });
            }
        }

        // Display the sample frequency for each sensor in Hz.
        private async void displaySampleFreq(Object state) {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                FrequencyTextBlock1.Text = freq[0] + " Hz";
                FrequencyTextBlock2.Text = freq[1] + " Hz";
                //AverageFrequencyTextBlock.Text = (samples[0] / secs).ToString() + " Hz";
            });

            freq[0] = 0;
            freq[1] = 0;

        }

        private async void readPressureSensor(Object state) {
            //reads the pressure value from both IO ports of only one board
            gpio[0].Pins[0].Adc.Read();
            gpio[0].Pins[1].Adc.Read();

        }

        // Go back to the main page (sensor selection)
        private async void back_Click(object sender, RoutedEventArgs e) {
            for (var i = 0; i < numBoards; i++) {
                if (!metawears[i].InMetaBootMode) {
                    metawears[i].TearDown();
                    await metawears[i].GetModule<IDebug>().DisconnectAsync();
                }
            }
            timer3.Dispose(); // disposes battery check timer, otherwise the software will try to read the battery when the board has already been disposed
            Frame.GoBack();
        }

        // Display quaternion information for each sensor in text form.
        private void setText(String s, int sensorNumber) {
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { textblocks[sensorNumber].Text = s; });
        }


        //accelerometer tests
        //private void updateAccelerometer(IData data) {
        //    var accel = data.Value<Acceleration>();
        //    setText(accel.ToString(), 0);

        //}

        /// <summary>
        ///  Routine used for collection of Quaternions data of Sensors. In the end, calls a new routine to plot processed Data.
        /// </summary>
        /// <param name="data"> Quaternion data collected by the sensor</param>
        /// <param name="sensorNumber"> number of the sensor used in project, starting by 0 for 1, 1 for 2 and etc </param>
        private async Task quaternionStreamRoutine(IData data, int sensorNumber) {
            if (isRunning) {
                var quat = data.Value<Quaternion>();
                var eulerAngles = calculateEulerAngles(quat);                                
                var time = data.FormattedTimestamp.ToString();

                var year = time.Substring(0, 4); var month = time.Substring(5, 2); var day = time.Substring(8, 2);
                var hour = time.Substring(11, 2); var minute = time.Substring(14, 2); var second = time.Substring(17, 2);
                var milli = time.Substring(20, 3);

                // Store data point
                if (record) {
                    String newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}{12}", samples[0], year, month, day, hour, minute, second, milli, quat.W, quat.X, quat.Y, quat.Z, Environment.NewLine);
                    addPoint(newLine, sensorNumber);
                    newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}", samples[0], year, month, day, hour, minute, second, milli, eulerAngles.roll, eulerAngles.pitch, eulerAngles.yaw, Environment.NewLine);
                    addPointEuler(newLine, sensorNumber);
                }
                // Update counters
                samples[sensorNumber]++;
                freq[sensorNumber]++;

                // Save reference quaternion
                if (shouldCenter[sensorNumber]) {
                    refEuler[sensorNumber] = eulerAngles;
                    refQuats[sensorNumber] = quat;
                    shouldCenter[sensorNumber] = false;
                    centered[sensorNumber] = true;
                }

                double angle = 0;
                double denom = 1;

                if (centered[sensorNumber]) {
                    WindowsQuaternion a = convertToWindowsQuaternion(refQuats[sensorNumber]);
                    WindowsQuaternion b = convertToWindowsQuaternion(quat);

                    quat = centerData(refQuats[sensorNumber], quat);
                    eulerAngles = centerData(refEuler[sensorNumber], eulerAngles);
                    angle = (angleMode) ? 2 * Math.Acos(WindowsQuaternion.Dot(a, b) / (a.Length() * b.Length())) * (180 / Math.PI) : 0;
                }
                else if (angleMode) {
                    angle = 2 * Math.Acos(quat.W) * (180 / Math.PI);
                    denom = Math.Sqrt(1 - Math.Pow(quat.W, 2));
                    denom = (denom < 0.001) ? 1 : denom;  // avoid divide by zero type errors
                }
                angle = (angle > 180) ? 360 - angle : angle;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    plotValues(angle, quat, eulerAngles, denom, sensorNumber);
                });
            }
        }

        /// <summary>
        ///  Plots the data optained by the sensor.
        /// </summary>
        /// <param name="angle"> Quaternion angle, obtained by the W component</param>
        /// <param name="quat"> Quaternion vector measured</param>
        /// <param name="denom"> Denom value calculated</param>
        /// <param name="sensorNumber"> Number of the sensor used</param>
        private void plotValues(double angle, Quaternion quat, EulerAngles eulAng, double denom, int sensorNumber) {
            if ((bool)ToggleButtonEulerGraph.IsChecked)
                plotEulerValues(angle, eulAng, denom, sensorNumber);
            else
                plotQuaternionValues(angle, quat, denom, sensorNumber);
        }

        /// <summary>
        ///  Plots the quaternions.
        /// </summary>
        /// <param name="angle"> Quaternion angle, obtained by the W component</param>
        /// <param name="quat"> Quaternion vector measured</param>
        /// <param name="denom"> Denom value calculated</param>
        /// <param name="sensorNumber"> Number of the sensor used</param>
        private void plotQuaternionValues(double angle, Quaternion quat, double denom, int sensorNumber) {
            // Add values to plot
            if ((bool)wCheckbox.IsChecked) {
                (model.Series[4 * sensorNumber] as LineSeries).Points.Add(new DataPoint(samples[0], (angleMode) ? angle : quat.W));
            }
            if ((bool)xyzCheckbox.IsChecked) {
                (model.Series[4 * sensorNumber + 1] as LineSeries).Points.Add(new DataPoint(samples[0], quat.X / denom));
                (model.Series[4 * sensorNumber + 2] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Y / denom));
                (model.Series[4 * sensorNumber + 3] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Z / denom));
            }

            setFlags(model);

            // Display values numerically
            double[] values = { angleMode ? angle : quat.W, (quat.X / denom), (quat.Y / denom), (quat.Z / denom) };
            String[] labels = { angleMode ? "Angle: " : "W: ", "\nX: ", "\nY: ", "\nZ: " };
            String s = createOrientationText(labels, values);
            setText(s, sensorNumber);

            if ((bool)wCheckbox.IsChecked || (bool)xyzCheckbox.IsChecked) {
                resetModel(model);
            }
        }

        /// <summary>
        ///  Plots the Euler angles values.
        /// </summary>
        /// <param name="angle"> Quaternion angle, obtained by the W component</param>
        /// <param name="eulerAngles"> Measured EulerAngles </param>
        /// <param name="denom"> Denom value calculated</param>
        /// <param name="sensorNumber"> Number of the sensor used</param>
        private void plotEulerValues(double angle, EulerAngles eulerAngles, double denom, int sensorNumber) {
            double[] values = { eulerAngles.roll, eulerAngles.pitch, eulerAngles.yaw };
            String[] labels = { "X: ", "\nY: ", "\nZ: " };
            String s = createOrientationText(labels, values);
            setText(eulerAngles.ToString(), sensorNumber);

            if ((bool)eulerCheckbox.IsChecked) {
                (modelEuler.Series[3 * sensorNumber ] as LineSeries).Points.Add(new DataPoint(samples[0], eulerAngles.roll));
                (modelEuler.Series[3 * sensorNumber + 1] as LineSeries).Points.Add(new DataPoint(samples[0], eulerAngles.pitch));
                (modelEuler.Series[3 * sensorNumber + 2] as LineSeries).Points.Add(new DataPoint(samples[0], eulerAngles.yaw));
            }

            setFlags(modelEuler);

            if ((bool)eulerCheckbox.IsChecked) {
                resetModel(modelEuler);
            }
        }

        private void setFlags(PlotModel model) {
            if (renew) {
                //model.Series.RemoveAt;
                (model.Series).Add(new AreaSeries { Color = OxyColors.LightGray });
                renew = false;
                changeBackGroundColor = true;
            }
            if (changeBackGroundColor) {
                (model.Series[model.Series.Count - 1] as AreaSeries).Points.Add(new DataPoint(samples[0], 400));
                (model.Series[model.Series.Count - 1] as AreaSeries).Points2.Add(new DataPoint(samples[0], -400));
            }
        }

        private void resetModel(PlotModel model) {
            model.InvalidatePlot(true);
            //if (secs > MainViewModel.MAX_SECONDS)
            if (samples.Max() > MainViewModel.MAX_DATA_SAMPLES) {
                model.Axes[1].Reset();
                //model.Axes[1].Maximum = secs;
                //model.Axes[1].Minimum = secs - MainViewModel.MAX_SECONDS;
                model.Axes[1].Maximum = samples.Max();
                model.Axes[1].Minimum = (samples.Max() - MainViewModel.MAX_DATA_SAMPLES);
                model.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
            }
        }

        private async void streamSwitch_Toggled(object sender, RoutedEventArgs e) {
            if (streamSwitch.IsOn) {
                isRunning = true;
                Clear_Click(null, null);
                samples[0] = 0;
                samples[1] = 0;

                sensorFusions = new ISensorFusionBosch[numBoards];
                gpio = new IGpio[numBoards];
                pressurePin = new ushort[2]; //2 gpio pins being used for the pressure sensors
                pressurePin[0] = 1023; pressurePin[1] = 1023;
                for (var j = 0; j < numBoards; j++) {
                    var i = j;
                    //test with GPIO
                    gpio[i] = metawears[i].GetModule<IGpio>();
                    // output 0V on pin 0 and 1
                    gpio[i].Pins[0].ClearOutput();
                    gpio[i].Pins[1].ClearOutput();
                    // -> sets pins 0 and 1 to high (~1023). Once pressed, it will go to a lower value
                    gpio[i].Pins[0].SetPullMode(PullMode.Up); 
                    gpio[i].Pins[1].SetPullMode(PullMode.Up); 

                    // Get producer for analog adc data on pin 0
                    IAnalogDataProducer adc0 = gpio[i].Pins[0].Adc;

                    await adc0.AddRouteAsync(source =>
                        source.Stream((data =>
                        {
                            verifyBabyGripPressure(data, 0);
                        }
                        )
                    ));
                    await gpio[i].Pins[1].Adc.AddRouteAsync(source =>
                        source.Stream((data =>
                        {
                            verifyBabyGripPressure(data, 1);
                        }
                        )
                    ));
                    sensorFusions[i] = metawears[i].GetModule<ISensorFusionBosch>();
                    sensorFusions[i].Configure(); // default settings is NDoF mode with +/-16g acc range and 2000dps gyro range
                    await sensorFusions[i].Quaternion.AddRouteAsync(source => source.Stream(async data => await quaternionStreamRoutine(data, i)));
                    sensorFusions[i].Quaternion.Start();
                    sensorFusions[i].Start();
                }
                print("Sensor fusion should be running!");
                InitFreqTimer();
                InitPressureTimer();
                Clear.Background = new SolidColorBrush(Windows.UI.Colors.Red);
            }
            else {
                isRunning = false;
                for (var i = 0; i < numBoards; i++) {
                    sensorFusions[i].Stop();
                    sensorFusions[i].Quaternion.Stop();
                    metawears[i].TearDown();
                    freq[i] = 0;
                }
                timer1.Dispose();
                timer2.Dispose();
            }
        }

        private void verifyBabyGripPressure(IData data, ushort gpioPin) {
            pressurePin[gpioPin] = data.Value<ushort>();
            print("adc" + gpioPin + "= " + pressurePin[gpioPin]);
            //verifies if either the pressure from one gpioPin or the other signalizes baby grip
            if (pressurePin[0] < MINIMUM_PRESSURE_TO_SIGNALIZE_PULLING_BABY || pressurePin[1] < MINIMUM_PRESSURE_TO_SIGNALIZE_PULLING_BABY) {
                //will center everytime small force is applied and wasn't changing color
                if (!changeBackGroundColor && !renew) { //state verification
                    Center_Click(null, null);
                    if (!RECORD_EVERYTHING_MODE) 
                        Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () 
                            => recordSwitch.IsOn = true);
                    else if (record){
                        for (int i = 0; i < numBoards; i++)
                            addPoint(Environment.NewLine + "+++" + Environment.NewLine, i);
                    }
                    renew = true;
                }
            }
            //if it stops applying force, the graph will stop being highlighted. Since the thread is called twice by each pressure sensor, the renew flag is
            //checked to ensure the right state
            else if(!renew) { 
                if (!RECORD_EVERYTHING_MODE)
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ()
                        => recordSwitch.IsOn = false);
                else if (record && changeBackGroundColor) {
                    for (int i = 0; i < numBoards; i++)
                        addPoint("---" + Environment.NewLine + Environment.NewLine, i);
                }
                changeBackGroundColor = false;
            }
        }

        //Function that calculate axes rotations by using quaternions
        private EulerAngles calculateEulerAngles(Quaternion quat) {
            EulerAngles ret = new EulerAngles();
            double roll, pitch, yaw;
            // roll (x-axis rotation)
            double sinr = +2.0 * (quat.W * quat.X + quat.Y * quat.Z);
            double cosr = +1.0 - 2.0 * (quat.X * quat.X + quat.Y * quat.Y);
            ret.roll = Math.Atan2(sinr, cosr) * 180 / (Math.PI);

            // pitch (y-axis rotation)
            double sinp = +2.0 * (quat.W * quat.Y - quat.Z * quat.X);
            if (Math.Abs(sinp) >= 1)
                pitch = (Math.PI / 2 * sinp / Math.Abs(sinp)); // use 90 degrees if out of range
            pitch = Math.Asin(sinp);
            ret.pitch = pitch * 180 / (Math.PI);
            // yaw (z-axis rotation)
            double siny = +2.0 * (quat.W * quat.Z + quat.X * quat.Y);
            double cosy = +1.0 - 2.0 * (quat.Y * quat.Y + quat.Z * quat.Z);
            ret.yaw = Math.Atan2(siny, cosy) * 180 / (Math.PI);
            return ret;
        }

        // silly function used to make the live orientation text
        public String createOrientationText(String[] labels, double[] values) {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < values.Length; i++) {
                s.Append(labels[i]);
                s.Append(values[i]);
            }
            return s.ToString();
        }
        // silly function used to make the live orientation text
        public String createEulerText(String[] labels, double[] values) {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < values.Length; i++) {
                s.Append(labels[i]);
                s.Append(values[i]);
            }
            return s.ToString();
        }


        // Reset y axis and store angleSwitch state.
        public async void angleSwitch_Toggled(Object sender, RoutedEventArgs e) {
            resetYAxis();
            angleMode = angleSwitch.IsOn;
        }

        // Store recordSwitch state.
        public async void recordSwitch_Toggled(Object sender, RoutedEventArgs e) {
            record = recordSwitch.IsOn;
            print("Record Switch toggled!");
        }


        public async void wChecked(Object sender, RoutedEventArgs e) {
            resetYAxis();
        }

        public async void xyzChecked(Object sender, RoutedEventArgs e) {
            resetYAxis();
        }

        public async void eulerChecked(Object sender, RoutedEventArgs e) {
            //resets euler graph y axis
            modelEuler.InvalidatePlot(true);
            modelEuler.Axes[0].Reset();
            modelEuler.Axes[0].Maximum = 180;    
            modelEuler.Axes[0].Minimum = -180;
            modelEuler.Axes[0].Zoom(modelEuler.Axes[0].Minimum, modelEuler.Axes[0].Maximum);
        }

        public async void eulerGraphToggled(Object sender, RoutedEventArgs e) {
            if (!((bool)ToggleButtonEulerGraph.IsChecked)) {
                ToggleButtonEulerGraph.IsChecked = true;
            }
            ToggleButtonGraph.IsChecked = false;
        }
        public async void regularGraphToggled(Object sender, RoutedEventArgs e) {
            if (!((bool)ToggleButtonGraph.IsChecked)) {
                ToggleButtonGraph.IsChecked = true;
            }
            ToggleButtonEulerGraph.IsChecked = false;
        }

        // Change axes to adjust for new maximum values.
        public void resetYAxis() {
            model.InvalidatePlot(true);
            model.Axes[0].Reset();
            if (angleSwitch.IsOn && (bool)wCheckbox.IsChecked) {
                model.Axes[0].Maximum = 180;
            }
            else {
                model.Axes[0].Maximum = 1;
            }
            model.Axes[0].Zoom(model.Axes[0].Minimum, model.Axes[0].Maximum);
        }

        // Center quaternion q2 with q1 as reference.
        Quaternion centerData(Quaternion q1, Quaternion q2) {
            WindowsQuaternion q1w = convertToWindowsQuaternion(q1);
            WindowsQuaternion q2w = convertToWindowsQuaternion(q2);

            WindowsQuaternion conj = WindowsQuaternion.Conjugate(q1w);
            WindowsQuaternion center = WindowsQuaternion.Multiply(conj, q2w);

            return convertToQuaternion(center);
        }

        EulerAngles centerData(EulerAngles eRef, EulerAngles e) {
            EulerAngles ret = new EulerAngles();
            ret.roll = fitIn360(e.roll - eRef.roll);
            ret.pitch = finIn180(e.pitch - eRef.pitch);
            ret.yaw = fitIn360(e.yaw - eRef.yaw);
            return ret;
        }

        private double fitIn360(double result) {
            if (result < -180)
                return 360 + result;
            else if (result > 180)
                return 360 - result;
            return result;
        }
        private double finIn180(double result) {
            if (result < -90)
                return 180 + result;
            else if (result > 90)
                return 180 - result;
            return result;
        }

        // Converts mbientlab quaternion object to Windows quaternion object.
        WindowsQuaternion convertToWindowsQuaternion(Quaternion q) {
            WindowsQuaternion qw = new WindowsQuaternion(q.W, q.X, q.Y, q.Z);
            return qw;
        }

        // Converts Windows quaternion object to mbientlab quaternion object.
        Quaternion convertToQuaternion(WindowsQuaternion wq) {
            Quaternion quat = new Quaternion(wq.W, wq.X, wq.Y, wq.Z);
            return quat;
        }

        // Save stored data and record and stream switches
        private void Save_Click(object sender, RoutedEventArgs e) {
            saveData();
        }

        private void Save_ClickEuler(object sender, RoutedEventArgs e) {
            saveEulerData();
        }

        //Save all recorded Euler Data
        private async Task saveEulerData(int sensorNumber = 1) {
            print("save of Euler angles data initiated for sensor: ");
            print(sensorNumber.ToString());

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.FileTypeChoices.Add("PLain Text", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = dateTextBox.Text + "_expEul" + numberTextBox.Text + "_" + ((sensorNumber == 1) ? Name1.Text : Name2.Text);
            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null) {
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                //write to file
                await Windows.Storage.FileIO.WriteTextAsync(file, csvEuler[sensorNumber - 1].ToString());
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete) {
                    if (sensorNumber == numBoards) {
                        numberTextBox.Text = (Int32.Parse(numberTextBox.Text) + 1).ToString();
                    }
                    else {
                        saveEulerData(sensorNumber + 1);
                    }
                }
                else {
                    //this.textBlock.Text = "File " + file.Name + " couldn't be saved.";
                }
            }
            else {
                //this.textBlock.Text = "Operation cancelled.";
            }
        }


        // Save all recorded data.
        private async Task saveData(int sensorNumber = 1) {
            print("save initiated for sensor: ");
            print(sensorNumber.ToString());

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = dateTextBox.Text + "_exp" + numberTextBox.Text + "_" + ((sensorNumber == 1) ? Name1.Text : Name2.Text);
            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null) {
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                // write to file
                await Windows.Storage.FileIO.WriteTextAsync(file, csv[sensorNumber - 1].ToString());
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete) {
                    if (sensorNumber == numBoards) {
                        numberTextBox.Text = (Int32.Parse(numberTextBox.Text) + 1).ToString();
                    }
                    else {
                        saveData(sensorNumber + 1);
                    }
                }
                else {
                    //this.textBlock.Text = "File " + file.Name + " couldn't be saved.";
                }
            }
            else {
                //this.textBlock.Text = "Operation cancelled.";
            }
        }

        private async void Clear_Click(object sender, RoutedEventArgs e) {
            for (int i = 0; i < numBoards; i++) {
                centered[i] = false;
                csv[i] = new StringBuilder();
                csvEuler[i] = new StringBuilder();
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                // Clear all data series
                foreach (var series in model.Series) {
                    var lseries = series as LineSeries;
                    lseries.Points.Clear();
                }
                foreach (var series in modelEuler.Series) {
                    var lseries = series as LineSeries;
                    lseries.Points.Clear();
                }

                // Reset plot
                model.Axes[1].Reset();
                model.Axes[1].Maximum = 0;
                model.Axes[1].Minimum = MainViewModel.MAX_DATA_SAMPLES;
                model.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
                model.InvalidatePlot(true);

                // Reset plot
                modelEuler.Axes[1].Reset();
                modelEuler.Axes[1].Maximum = 0;
                modelEuler.Axes[1].Minimum = MainViewModel.MAX_DATA_SAMPLES;
                modelEuler.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
                modelEuler.InvalidatePlot(true);

            });

            if (!isRunning) {
                Clear.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
            }
        }

        private void Center_Click(object sender, RoutedEventArgs e) {
            shouldCenter[0] = true;
            shouldCenter[1] = true;
        }

        void addPoint(String s, int sensorNumber) {
            if (isRunning) {
                csv[sensorNumber].Append(s);
            }
        }

        void addPointEuler(String s, int sensorNumber) {
            if (isRunning) {
                csvEuler[sensorNumber].Append(s);
            }
        }

        private void print(String s) {
            System.Diagnostics.Debug.WriteLine(s);
        }
    }

    sealed class EulerAngles {

        public double roll { get; set; }
        public double pitch { get; set; }
        public double yaw { get; set; }
        public EulerAngles() {
            roll = 0.0;
            pitch = 0.0;
            yaw = 0.0;
        }

        public override string ToString() {
            return "x: " + roll.ToString() + "°\ny: " + pitch.ToString() + "°\nz: " + yaw.ToString() + "°";
        }

    }
}
