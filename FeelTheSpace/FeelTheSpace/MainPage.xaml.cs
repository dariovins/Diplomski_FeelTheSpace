using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FeelTheSpace
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public List<MediaElement> sounds = new List<MediaElement>();
        public int numberOfSensors = 1;

        public RfcommDeviceService _service;
        public StreamSocket _socket;
        public DataReader dataReaderObject;
        ObservableCollection<PairedDeviceInfo> _pairedDevices;

        public CancellationTokenSource ReadCancellationTokenSource;


        public MainPage()
        {
            this.InitializeComponent();

            //Metoda koja sve zvukove stavi u prethodno kreiranu listu zvukova
            InitializeSounds();

            //Metoda koja pronalazi sve dostupne uparene Bluetooth module
            //i stavlja ih u kolekciju potencijalnih uredjaja za uparivanje
            InitializeRfcommDeviceService();

          

            
        }

       

       

        private void InitializeSounds()
        {
            sounds.Add(zvuk1);
            sounds.Add(zvuk2);
            sounds.Add(zvuk3);
            sounds.Add(zvuk4);
            sounds.Add(zvuk5);
            sounds.Add(zvuk6);
        }

        private async void InitializeRfcommDeviceService()
        {
            try
            {
                DeviceInformationCollection DeviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));


                var numDevices = DeviceInfoCollection.Count();

                // By clearing the backing data, we are effectively clearing the ListBox
                _pairedDevices = new ObservableCollection<PairedDeviceInfo>();
                _pairedDevices.Clear();

                if (numDevices == 0)
                {
                    Debug.WriteLine("InitializeRfcommDeviceService: No paired devices found.");
                }
                else
                {
                    // Found paired devices.
                    foreach (var deviceInfo in DeviceInfoCollection)
                    {
                        _pairedDevices.Add(new PairedDeviceInfo(deviceInfo));
                    }
                }
                PairedDevices.Source = _pairedDevices;
                Debug.WriteLine(_pairedDevices[0].Name);

                //Metoda koja iz prethodno napunjene kolekcije potencijalnih konekcija
                //pokusava uspostaviti konekciju sa nasim uredjajem
                InitializeHC05Connection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InitializeRfcommDeviceService: " + ex.Message);
            }
          
        }

        private async void InitializeHC05Connection()
        {
            DeviceInformation DeviceInfo = null;
            PairedDeviceInfo pairedDevice = null;

            for (int i = 0; i < _pairedDevices.Count(); i++)
            {
                if (_pairedDevices[i].Name == "HC-05")
                {
                    pairedDevice = _pairedDevices[i];
                    DeviceInfo = pairedDevice.DeviceInfo;
                    break;
                }
            }


            bool success = true;
            try
            {
                _service = await RfcommDeviceService.FromIdAsync(DeviceInfo.Id);

                if (_socket != null)
                {
                    // Disposing the socket with close it and release all resources associated with the socket
                    _socket.Dispose();
                }

                _socket = new StreamSocket();
                try
                {
                    // Note: If either parameter is null or empty, the call will throw an exception
                    await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);
                }
                catch (Exception ex)
                {
                    success = false;
                    Debug.WriteLine("Connect:" + ex.Message);
                }
                // If the connection was successful, the RemoteAddress field will be populated
                if (success)
                {
                    
                    string msg = String.Format("Connected to {0}!", _socket.Information.RemoteAddress.DisplayName);
                    Debug.WriteLine(msg);
                    //Metoda koja aktivira slusanje
                    //Listen();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Overall Connect: " + ex.Message);
                _socket.Dispose();
                _socket = null;
            }
        }

        private async void Listen()
        {
            try
            {
                ReadCancellationTokenSource = new CancellationTokenSource();
                if (_socket.InputStream != null)
                {
                    dataReaderObject = new DataReader(_socket.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
              
                this.textBlockBTName.Text = "";
                this.TxtBlock_SelectedID.Text = "";
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    Debug.WriteLine("Listen: Reading task was cancelled, closing device and cleaning up");
                }
                else
                {
                    Debug.WriteLine("Listen: " + ex.Message);
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }


        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            List<List<int>> sensorValues = new List<List<int>>();

            int[] allSensorValues;

            for (int i = 0; i < numberOfSensors; i++)
            {
                sensorValues.Add(new List<int>());
            }

            int sensorCounter = 0;


            if (bytesRead > 0)
            {
                try
                {
                    string recvdtxt = dataReaderObject.ReadString(bytesRead);
                    Debug.WriteLine(recvdtxt);
                    this.textBoxRecvdText.Text += recvdtxt;

                    allSensorValues = recvdtxt.Split(' ').Select(n => Convert.ToInt32(n)).ToArray();

                    if (allSensorValues.Length == 150)
                    {
                        for (int i = 0; i < 150; i++)
                        {
                            sensorValues[sensorCounter].Add(allSensorValues[i]);
                            sensorCounter++;
                            if (sensorCounter == numberOfSensors)
                                sensorCounter = 0;

                        }
                        Array.Clear(allSensorValues, 0, 149);

                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ReadAsync: " + ex.Message);
                }

            }


        }


        //ako zatreba cancel za primanje
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }


    }
}
