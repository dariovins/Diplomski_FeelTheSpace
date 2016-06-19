using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
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
        public int numberOfSensors = 3;

        public RfcommDeviceService _service;
        public StreamSocket _socket;
        public DataReader dataReaderObject;
        ObservableCollection<PairedDeviceInfo> _pairedDevices;

        MediaElement active = new MediaElement();

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
            sounds.Add(D_blize);
            sounds.Add(D_dalje);
            sounds.Add(C_blize);
            sounds.Add(C_dalje);
            sounds.Add(L_blize);
            sounds.Add(L_dalje);
        }

        private async void InitializeRfcommDeviceService()
        {
            try
            {
                //Pronadji sve uredjaje koji su dostupni putem bluetooth-a
                DeviceInformationCollection DeviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));


                var numDevices = DeviceInfoCollection.Count();

                //kreiraj praznu kolekciju uparenih uredjaja
                _pairedDevices = new ObservableCollection<PairedDeviceInfo>();
                _pairedDevices.Clear();

                if (numDevices == 0)
                {
                    Debug.WriteLine("InitializeRfcommDeviceService: No paired devices found.");
                }
                else
                {
                    // Nadjeni upareni uredjaji
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

            //Prodji kroz uparene uredjaje i probaj naci HC-05 bluetooth modul
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
                //Kreiraj servis koristeci ID HC-05 modula (ako je uspjesno nadjen)
                _service = await RfcommDeviceService.FromIdAsync(DeviceInfo.Id);

                if (_socket != null)
                {
                    // Ako vec postoji nekih stvari u socket-u, prvo ocisti sve resurse
                    _socket.Dispose();
                }

                _socket = new StreamSocket();
                try
                {
                    // Ako je bilo koji od dva parametra null ili prazan, ovaj poziv ce baciti izuzetak
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
                    Listen();
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
                    // Nastavi citati serial input
                    while (true)
                    {
                        while (dataReaderObject.UnconsumedBufferLength != 0)
                        {
                            await Task.Delay(200);
                            Debug.WriteLine(dataReaderObject.UnconsumedBufferLength);
                        }
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

            uint ReadBufferLength = 5;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.ReadAhead;


            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);


            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;



            string[] value;
            char side;

            if (bytesRead > 0)
            {
                try
                {
                    string recvdtxt = dataReaderObject.ReadString(bytesRead);
                    Debug.WriteLine(recvdtxt);
                    //this.textBoxRecvdText.Text += recvdtxt;

                    value = Regex.Split(recvdtxt, @"\D+");
                    side = recvdtxt[0];
                    await playSound(side, value[1]);


                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ReadAsync: " + ex.Message);
                }

            }


        }

        private async Task playSound(char side, string v)
        {
            if (L_blize.CurrentState == MediaElementState.Playing || L_dalje.CurrentState == MediaElementState.Playing || D_blize.CurrentState == MediaElementState.Playing || D_dalje.CurrentState == MediaElementState.Playing || C_blize.CurrentState == MediaElementState.Playing || C_dalje.CurrentState == MediaElementState.Playing)
                return;
            if (side == 'l')
            {
                if (Convert.ToInt16(v) <= 60)
                    L_blize.Play();
                else L_dalje.Play();
            }
            else if (side == 'c')
            {
                if (Convert.ToInt16(v) <= 60)
                    C_blize.Play();
                else C_dalje.Play();
            }
            else if (side == 'r')
            {
                if (Convert.ToInt16(v) <= 60)
                    D_blize.Play();
                else D_dalje.Play();
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
