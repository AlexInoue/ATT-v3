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

// O modelo de item de Página em Branco está documentado em https://go.microsoft.com/fwlink/?LinkId=234238

namespace ATT {
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
                BrokenLineStyle = LineStyle.Solid,
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
                BrokenLineStyle = LineStyle.Solid,
                MarkerStroke = OxyColor.FromRgb(0, 1, 1),
                LineStyle = LineStyle.Solid,
                Title = "X2"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(1, 0, 0),
                LineStyle = LineStyle.Solid,
                Title = "Y2"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 1, 0),
                LineStyle = LineStyle.Solid,
                Title = "Z2"
            });
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
        }

        public PlotModel MyModel { get; private set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LineGraph : Page {

        // private IMetaWearBoard metawear;
        private ISensorFusionBosch[] sensorFusions;

        int numBoards = 1;
        //private IntPtr cppBoard;
        private IMetaWearBoard[] metawears;
        bool startNext = true;
        bool isRunning = false;
        bool[] centered = { false, false };
        bool[] shouldCenter = { false, false };
        bool record = false;
        bool angleMode = false;
        Stopwatch myStopWatch = new Stopwatch();
        List<DataPoint>[] dataPoints = { new List<DataPoint>(), new List<DataPoint>() };
        int[] freq = { 0, 0 };
        Quaternion[] centerQuats = new Quaternion[2];
        PlotModel model;
        int[] samples = { 0, 0 };
        int secs = 0;
        StringBuilder[] csv = { new StringBuilder(), new StringBuilder() };
        TextBlock[] textblocks = new TextBlock[3];
        private System.Threading.Timer timer1;
        ISensorFusionBosch sensorFusion;
        ISensorFusionBosch sensorFusion2;

        public LineGraph() {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            print(e.Parameter.ToString());
            var devices = e.Parameter as BluetoothLEDevice[];
            numBoards = devices.Length;
            metawears = new IMetaWearBoard[numBoards];

            for (var i = 0; i < numBoards; i++) {
                // Initialize boards and enable high frequency streaming
                metawears[i] = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(devices[i]);
                var settings = metawears[i].GetModule<ISettings>();
                settings.EditBleConnParams(maxConnInterval: 7.5f);
            }

            textblocks[0] = DataTextBlock1;
            textblocks[1] = DataTextBlock2;
            textblocks[2] = DataTextBlock3;

            InitBatteryTimer();

            if (numBoards == 1) {
                removeBoardTwoFormatting();
            }

            model = (DataContext as MainViewModel).MyModel;
            // Settings settings = metawears[0].getModule(Settings.class);
        }

        public void removeBoardTwoFormatting() {
            dataGrid.Children.RemoveAt(1);
            dataGrid.ColumnDefinitions.RemoveAt(1);

            controlGrid.Children.Remove(FrequencyTextBlock2);
            controlGrid.Children.Remove(BatteryTextBlock2);
            controlGrid.ColumnDefinitions.RemoveAt(1);
        }

        public void InitFreqTimer() {
            timer1 = new System.Threading.Timer(displaySampleFreq, null, 0, 1000);
        }

        public void InitBatteryTimer() {
            timer1 = new System.Threading.Timer(displayBatteryLevel, null, 0, 10000);
        }

        public async void displayBatteryLevel(Object state) {
            TextBlock[] textblocks = { BatteryTextBlock1, BatteryTextBlock2 };
            for (var i = 0; i < numBoards; i++) {
                byte battery = await metawears[i].ReadBatteryLevelAsync();
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    textblocks[i].Text = battery.ToString() + " %";
                });
            }
        }

        private async void displaySampleFreq(Object state) {
            secs += 1;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                FrequencyTextBlock1.Text = freq[0] + " Hz";
                FrequencyTextBlock2.Text = freq[1] + " Hz";
                AverageFrequencyTextBlock.Text = (samples[0] / secs).ToString() + " Hz";
            });

            freq[0] = 0;
            freq[1] = 0;
        }

        private async void back_Click(object sender, RoutedEventArgs e) {
            for (var i = 0; i < numBoards; i++) {
                if (!metawears[i].InMetaBootMode) {
                    metawears[i].TearDown();
                    await metawears[i].GetModule<IDebug>().DisconnectAsync();
                }
            }
            Frame.GoBack();
        }

        void setText(String s, int sensorNumber) {
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { textblocks[sensorNumber].Text = s; });
        }

        private void updateAccelerometer(IData data) {
            var accel = data.Value<Acceleration>();
            setText(accel.ToString(), 0);

        }

        private async void streamSwitch_Toggled(object sender, RoutedEventArgs e) {
            if (streamSwitch.IsOn) {
                myStopWatch.Start();
                isRunning = true;

                Clear_Click(null, null);
                samples[0] = 0;
                samples[1] = 0;

                sensorFusions = new ISensorFusionBosch[2];

                sensorFusion = metawears[0].GetModule<ISensorFusionBosch>();
                sensorFusion.Configure();  // default settings is NDoF mode with +/-16g acc range and 2000dps gyro range
                await sensorFusion.LinearAcceleration.AddRouteAsync(source => source.Stream(async data => updateAccelerometer(data)));
                await sensorFusion.Quaternion.AddRouteAsync(source => source.Stream(async data =>
                {
                    if (isRunning) {
                        var quat = data.Value<Quaternion>();
                        var time = data.FormattedTimestamp.ToString();

                        var year = time.Substring(0, 4);
                        var month = time.Substring(5, 2);
                        var day = time.Substring(8, 2);
                        var hour = time.Substring(11, 2);
                        var minute = time.Substring(14, 2);
                        var second = time.Substring(17, 2);
                        var milli = time.Substring(20, 3);
                        //print(time.ToString());
                        //print(time[0].ToString());
                        // Store data point
                        if (record) {
                            String newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}{12}", samples[0], year, month, day, hour, minute, second, milli, quat.W, quat.X, quat.Y, quat.Z, Environment.NewLine);
                            addPoint(newLine, 0);
                        }
                        //var secs = myStopWatch.ElapsedMilliseconds * 0.001;

                        // Update counters
                        samples[0]++;
                        freq[0]++;

                        // Save reference quaternion
                        if (shouldCenter[0]) {
                            centerQuats[0] = quat;
                            shouldCenter[0] = false;
                            centered[0] = true;
                        }

                        double angle = 0;
                        double denom = 1;

                        if (centered[0]) {
                            WindowsQuaternion a = convertToWindowsQuaternion(centerQuats[0]);
                            WindowsQuaternion b = convertToWindowsQuaternion(quat);

                            quat = centerData(centerQuats[0], quat);
                            angle = (angleMode) ? 2 * Math.Acos(WindowsQuaternion.Dot(a, b) / (a.Length() * b.Length())) * (180 / Math.PI) : 0;
                        }
                        else if (angleMode) {
                            angle = 2 * Math.Acos(quat.W) * (180 / Math.PI);
                            angle = (angle > 250) ? 360 - angle : angle;
                            denom = Math.Sqrt(1 - Math.Pow(quat.W, 2));
                            denom = (denom < 0.001) ? 1 : denom;  // avoid divide by zero type errors
                        }

                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            // Add values to plot
                            if ((bool)wCheckbox.IsChecked) {
                                (model.Series[0] as LineSeries).Points.Add(new DataPoint(samples[0], (angleMode) ? angle : quat.W));
                            }
                            if ((bool)xyzCheckbox.IsChecked) {
                                (model.Series[1] as LineSeries).Points.Add(new DataPoint(samples[0], quat.X / denom));
                                (model.Series[2] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Y / denom));
                                (model.Series[3] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Z / denom));
                            }

                            // Display values numerically
                            double[] values = { angleMode ? angle : quat.W, (quat.X / denom), (quat.Y / denom), (quat.Z / denom) };
                            String[] labels = { angleMode ? "Angle: " : "W: ", "\nX: ", "\nY: ", "\nY: " };
                            setText(calculateEulerAngles(quat), 2);
                            String s = createOrientationText(labels, values);
                            setText(s, 0);

                            // Reset axes as needed
                            if ((bool)wCheckbox.IsChecked || (bool)xyzCheckbox.IsChecked) {
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
                        });
                    }
                }));
                sensorFusion.Quaternion.Start();
                sensorFusion.LinearAcceleration.Start();
                sensorFusion.Start();

                if (numBoards == 2) {
                    sensorFusion2 = metawears[1].GetModule<ISensorFusionBosch>();
                    sensorFusion2.Configure();  // default settings is NDoF mode with +/-16g acc range and 2000dps gyro range

                    await sensorFusion2.Quaternion.AddRouteAsync(source => source.Stream(async data =>
                    {
                        var quat = data.Value<Quaternion>();
                        // Store data point
                        if (record) {
                            String newLine = string.Format("{0},{1},{2},{3},{4}{5}", samples[0], quat.W, quat.X, quat.Y, quat.Z, Environment.NewLine);
                            addPoint(newLine, 1);
                        }

                        if (isRunning) {
                            // Update counters
                            samples[1]++;
                            freq[1]++;

                            // Center quaternion if needed
                            if (shouldCenter[1]) {
                                centerQuats[1] = quat;
                                shouldCenter[1] = false;
                                centered[1] = true;
                            }

                            double angle;

                            if (centered[1]) {
                                WindowsQuaternion a = convertToWindowsQuaternion(centerQuats[1]);
                                WindowsQuaternion b = convertToWindowsQuaternion(quat);

                                angle = 2 * Math.Acos(WindowsQuaternion.Dot(a, b) / (a.Length() * b.Length())) * (180 / Math.PI); ;

                                quat = centerData(centerQuats[1], quat);
                                //System.Diagnostics.Debug.WriteLine(finalQuat.w.ToString() + "   " + finalQuat.x.ToString() + "   " + finalQuat.y.ToString() + "   " + finalQuat.z.ToString());
                            }
                            else {
                                angle = 2 * Math.Acos(quat.W) * (180 / Math.PI);
                            }

                            double denom = Math.Sqrt(1 - Math.Pow(quat.W, 2));

                            if (denom < 0.001) {
                                denom = 1;
                            }
                            if (angle > 250) {
                                angle = 360 - angle;
                            }
                            //var secs = myStopWatch.ElapsedMilliseconds * 0.001;
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                // Display quaternion
                                String s;
                                // return;
                                if (angleSwitch.IsOn) {
                                    if ((bool)wCheckbox.IsChecked) {
                                        (model.Series[4] as LineSeries).Points.Add(new DataPoint(samples[0], angle));
                                    }
                                    if ((bool)xyzCheckbox.IsChecked) {
                                        (model.Series[5] as LineSeries).Points.Add(new DataPoint(samples[0], quat.X / denom));
                                        (model.Series[6] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Y / denom));
                                        (model.Series[7] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Z / denom));
                                    }

                                    s = "Angle: " + angle.ToString() + "\nX: " + (quat.X / denom).ToString() + "\nY: " + (quat.Y / denom).ToString() + "\nZ: " + (quat.Z / denom).ToString();
                                }
                                else {
                                    if ((bool)wCheckbox.IsChecked) {
                                        (model.Series[4] as LineSeries).Points.Add(new DataPoint(samples[0], quat.W));
                                    }
                                    if ((bool)xyzCheckbox.IsChecked) {
                                        (model.Series[5] as LineSeries).Points.Add(new DataPoint(samples[0], quat.X));
                                        (model.Series[6] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Y));
                                        (model.Series[7] as LineSeries).Points.Add(new DataPoint(samples[0], quat.Z));
                                    }

                                    s = "W: " + quat.W.ToString() + "\nX: " + quat.X.ToString() + "\nY: " + quat.Y.ToString() + "\nZ: " + quat.Z.ToString();
                                }
                                setText(s, 1);
                            });
                        }
                    }));
                    sensorFusion2.Quaternion.Start();
                    sensorFusion2.Start();
                    print("Sensor fusion should be running!");
                }

                InitFreqTimer();
                Clear.Background = new SolidColorBrush(Windows.UI.Colors.Red);
            }
            else {
                isRunning = false;
                sensorFusions[0] = sensorFusion;
                sensorFusions[1] = sensorFusion2;
                for (var i = 0; i < numBoards; i++) {
                    sensorFusions[i].Stop();
                    sensorFusions[i].Quaternion.Stop();
                    metawears[i].TearDown();

                    freq[i] = 0;
                }

                timer1.Dispose();
                myStopWatch.Stop();
                myStopWatch.Reset();
            }
        }

        private String calculateEulerAngles(Quaternion quat) {
            double roll, pitch, yaw;
            // roll (x-axis rotation)
            double sinr = +2.0 * (quat.W * quat.X + quat.Y * quat.Z);
            double cosr = +1.0 - 2.0 * (quat.X * quat.X + quat.Y * quat.Y);
            roll = Math.Atan2(sinr, cosr) * 180 / (Math.PI);

            // pitch (y-axis rotation)
            double sinp = +2.0 * (quat.W * quat.Y - quat.Z * quat.X);
            if (Math.Abs(sinp) >= 1)
                pitch = (Math.PI / 2 * sinp / Math.Abs(sinp)); // use 90 degrees if out of range
            else
                pitch = Math.Asin(sinp);
            pitch = pitch * 180 / (Math.PI);
            // yaw (z-axis rotation)
            double siny = +2.0 * (quat.W * quat.Z + quat.X * quat.Y);
            double cosy = +1.0 - 2.0 * (quat.Y * quat.Y + quat.Z * quat.Z);
            yaw = Math.Atan2(siny, cosy) * 180 / (Math.PI);
            return "x: " + roll.ToString() + "°\ny: " + pitch.ToString() + "°\nz: " + yaw.ToString() + "°";
        }

        public String createOrientationText(String[] labels, double[] values) {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < values.Length; i++) {
                s.Append(labels[i]);
                s.Append(values[i]);
            }
            return s.ToString();
        }

        public async void angleSwitch_Toggled(Object sender, RoutedEventArgs e) {
            resetYAxis();
            angleMode = angleSwitch.IsOn;
        }

        public async void recordSwitch_Toggled(Object sender, RoutedEventArgs e) {
            record = recordSwitch.IsOn;
        }

        public async void wChecked(Object sender, RoutedEventArgs e) {
            resetYAxis();
        }

        public async void xyzChecked(Object sender, RoutedEventArgs e) {
            resetYAxis();
        }

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

        Quaternion centerData(Quaternion q1, Quaternion q2) {
            WindowsQuaternion q1w = convertToWindowsQuaternion(q1);
            WindowsQuaternion q2w = convertToWindowsQuaternion(q2);

            WindowsQuaternion conj = WindowsQuaternion.Conjugate(q1w);
            WindowsQuaternion center = WindowsQuaternion.Multiply(conj, q2w);

            return convertToQuaternion(center);
        }

        WindowsQuaternion convertToWindowsQuaternion(Quaternion q) {
            WindowsQuaternion qw = new WindowsQuaternion(q.W, q.X, q.Y, q.Z);
            return qw;
        }

        Quaternion convertToQuaternion(WindowsQuaternion wq) {
            Quaternion quat = new Quaternion(wq.W, wq.X, wq.Y, wq.Z);
            return quat;
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            saveData();
        }

        private async Task saveData(int sensorNumber = 1) {
            print("save initiated for sensor: ");
            print(sensorNumber.ToString());

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "New Document";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null) {
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                // write to file
                await Windows.Storage.FileIO.WriteTextAsync(file, csv[sensorNumber - 1].ToString());
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete) {
                    if (sensorNumber == 1 && numBoards == 2) {
                        saveData(2);
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
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                // Clear all data series
                foreach (var series in model.Series) {
                    var lseries = series as LineSeries;
                    lseries.Points.Clear();
                }

                // Reset plot
                model.Axes[1].Reset();
                model.Axes[1].Maximum = 0;
                model.Axes[1].Minimum = MainViewModel.MAX_DATA_SAMPLES;
                model.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
                model.InvalidatePlot(true);
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

        private void print(String s) {
            System.Diagnostics.Debug.WriteLine(s);
        }
    }
}
