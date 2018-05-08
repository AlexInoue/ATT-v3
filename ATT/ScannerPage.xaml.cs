using MbientLab.MetaWear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Runtime;


namespace ATT {

    /// <summary>
    /// Scanner page will be in charge of scanning new MetaWear devices before proceeding to the LineGraph page.
    /// </summary>
    public sealed partial class ScannerPage : Page {

        #region fields
        private BluetoothLEAdvertisementWatcher btleWatcher;
        private HashSet<ulong> seenDevices = new HashSet<ulong>();
        private ScanConfig config;
        private Timer timer;
        #endregion

        #region Public Methods and Constructor
        public ScannerPage() {
            InitializeComponent();

            btleWatcher = new BluetoothLEAdvertisementWatcher {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            btleWatcher.Received += async (w, btAdv) =>
            {
                if (!seenDevices.Contains(btAdv.BluetoothAddress) &&
                        config.ServiceUuids.Aggregate(true, (acc, e) => acc & btAdv.Advertisement.ServiceUuids.Contains(e))) {
                    seenDevices.Add(btAdv.BluetoothAddress);
                    var device = await BluetoothLEDevice.FromBluetoothAddressAsync(btAdv.BluetoothAddress);
                    if (device != null) {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => pairedDevices.Items.Add(device));
                    }
                }
            };
        }
        #endregion

        #region Protected Methods
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            config = e.Parameter as ScanConfig;
            refreshDevices_Click(null, null);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Callback for the refresh button which populates the devices list
        /// </summary>
        private void refreshDevices_Click(object sender, RoutedEventArgs args) {
            if (timer != null) {
                timer.Dispose();
                timer = null;
            }
            btleWatcher.Stop();

            var connected = pairedDevices.Items.Where(e => (e as BluetoothLEDevice).ConnectionStatus == BluetoothConnectionStatus.Connected);

            seenDevices.Clear();
            pairedDevices.Items.Clear();

            foreach (var it in connected) {
                seenDevices.Add((it as BluetoothLEDevice).BluetoothAddress);
                pairedDevices.Items.Add(it);
            }

            btleWatcher.Start();
            timer = new Timer(e => btleWatcher.Stop(), null, config.Duration, Timeout.Infinite);
        }

        /// <summary>
        /// onClick handler for the continue button. In case of a positive selection of one or more devices, forwards the user to the LineGraph page
        /// In case of a selection of no devices, shows an error message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void continue_Click(Object sender, RoutedEventArgs e) {
            try {
                var devices = pairedDevices.SelectedItems;
                BluetoothLEDevice[] passDevices = new BluetoothLEDevice[devices.Count];

                //TODO: enable more than 2 devices, like 3
                if (devices.Count == 0 || devices.Count > 2) {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, async () =>
                    {
                        await new ContentDialog() {
                            Title = "Invalid Number of Devices",
                        //TODO: correct the 
                        Content = "Please select 1 or 2 sensors from the list of connected sensors to proceed.",
                            PrimaryButtonText = "OK"
                        }.ShowAsync();
                    });
                    return;
                }

                btleWatcher.Stop();
                // var item = ((ListView)sender).SelectedItem as BluetoothLEDevice;
                var i = 1;
                foreach (BluetoothLEDevice item in devices) {
                    if (item != null) {
                        passDevices[i ] = item;
                        ContentDialog initPopup = new ContentDialog() {
                            Title = "Initializing API",
                            Content = "Please wait while the app initializes the API"
                        };

                        initPopup.ShowAsync();
                        var board = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(item);
                        await board.InitializeAsync();
                        initPopup.Hide();

                        if (i == devices.Count) {
                            Frame.Navigate(config.NextPageType, passDevices);
                        }
                        i += 1;
                    }
                }
            }
            catch (Exception ex) {
                ContentDialog errorPopup = new ContentDialog() {
                    Title = "Timeout error",
                    Content = "A timeout error happened when initializing boards.",
                    PrimaryButtonText = "Try Again",
                    PrimaryButtonCommand = { },
                    CloseButtonText = "Close"
                };
                await errorPopup.ShowAsync();
            }
        }
        #endregion
    }


    #region complimentary classes

    /// <summary>
    /// Class containing scanning configurations used to scan new devices. Duration and ServiceUuids are currently set as default
    /// </summary>
    public sealed class ScanConfig {
        internal int Duration { get; }
        internal Type NextPageType { get; }
        internal List<Guid> ServiceUuids { get; }

        public ScanConfig(Type nextPageType, int duration = 10000, List<Guid> serviceUuids = null) {
            Duration = duration;
            NextPageType = nextPageType;
            ServiceUuids = serviceUuids == null ? new List<Guid>(new Guid[] { Constants.METAWEAR_GATT_SERVICE }) : serviceUuids;
        }
    }
    /// <summary>
    /// Class used to convert ulong representation in hexa of a MACAddress to an address separated by ":"s. Back convertion not implemented
    /// </summary>
    public sealed class MacAddressHexString : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            string hexString = ((ulong)value).ToString("X");
            return hexString.Insert(2, ":").Insert(5, ":").Insert(8, ":").Insert(11, ":").Insert(14, ":");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Class developed to highlight selected scanned devices.
    /// </summary>
    public sealed class ConnectionStateColor : IValueConverter {
        public SolidColorBrush ConnectedColor { get; set; }
        public SolidColorBrush DisconnectedColor { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language) {
            switch ((BluetoothConnectionStatus)value) {
                case BluetoothConnectionStatus.Connected:
                    return ConnectedColor;
                case BluetoothConnectionStatus.Disconnected:
                    return DisconnectedColor;
                default:
                    throw new MissingMemberException("Unrecognized connection status: " + value.ToString());
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }

    #endregion





}
